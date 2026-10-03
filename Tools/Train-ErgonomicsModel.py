#!/usr/bin/env python3
"""Train discomfort prediction on real 1-5 feedback, never rule outputs.

One stable labeled work block is one example. Hold out whole people when
possible, otherwise whole sessions. A model is withheld if validation fails.
This predicts discomfort, not the causal benefit of changing a setting.
"""
import csv
import itertools
import json
import math
import statistics
import sys
from collections import defaultdict
from pathlib import Path

BASE = ["peak_load", "load_average", "load_torso", "held_weight", "queue_ratio",
        "speed_norm", "left_reach", "right_reach", "table_height"]
PROFILE = ["height_cm", "reach_cm"]
THRESHOLD = .70


def sigmoid(value):
    return 1 / (1 + math.exp(-max(-20., min(20., value))))


root = Path(__file__).resolve().parents[1]
default_source = root / "Recordings/Quest/FactoryML/manufacturing_trials_v3.csv"
source = Path(sys.argv[1]) if len(sys.argv) > 1 else default_source
target = Path(sys.argv[2]) if len(sys.argv) > 2 else root / "Assets/StreamingAssets/ErgonomicsModel.json"
if not source.exists():
    raise SystemExit(f"No real labeled dataset found: {source}")

trials = defaultdict(list)
with source.open(encoding="utf-8-sig", newline="") as stream:
    reader = csv.DictReader(stream)
    personalized = reader.fieldnames is not None and all(name in reader.fieldnames for name in PROFILE + ["profile_id"])
    for line, record in enumerate(reader, 2):
        try:
            if source == default_source and record["session"].startswith("editor_synthetic_"):
                raise ValueError("Editor simulation cannot be used as real Quest training data")
            label = int(record["feedback"])
            if label not in range(1, 6):
                raise ValueError("feedback must be an entered 1-5 score")
            loads = [float(record[name]) for name in (
                "load_l_shoulder", "load_r_shoulder", "load_l_arm", "load_r_arm",
                "load_l_wrist", "load_r_wrist", "load_torso")]
            values = [max(loads), float(record["load_average"]), float(record["load_torso"]),
                      float(record["held_weight"]), float(record["queue_count"]) / 12,
                      (float(record["belt_speed"]) - .15) / .60,
                      float(record["left_reach"]), float(record["right_reach"]),
                      float(record["table_height"])]
            if personalized:
                values += [float(record["height_cm"]), float(record["reach_cm"])]
            if not all(math.isfinite(value) for value in values):
                raise ValueError("non-finite sensor/profile value")
            trials[(record["session"], record["trial"])].append(
                (record.get("profile_id", "NONE"), values, int(label >= 4), record["part_mode"]))
        except (KeyError, TypeError, ValueError) as error:
            raise SystemExit(f"Invalid data on CSV line {line}: {error}")

examples = []
sample_seconds = 0
for (session, _trial), samples in sorted(trials.items()):
    people = {sample[0] for sample in samples}
    labels = {sample[2] for sample in samples}
    modes = {sample[3] for sample in samples}
    if len(samples) < 10 or len(people) != 1 or len(labels) != 1 or len(modes) != 1:
        continue
    # Do not call a mixed-speed/mixed-height block one experimental condition.
    if any(max(sample[1][index] for sample in samples) - min(sample[1][index] for sample in samples) > limit
           for index, limit in ((5, .12), (8, .08))):
        continue
    if personalized and any(max(sample[1][index] for sample in samples) - min(sample[1][index] for sample in samples) > .1
                            for index in (9, 10)):
        continue
    vector = [statistics.fmean(sample[1][index] for sample in samples)
              for index in range(len(samples[0][1]))]
    examples.append((session, next(iter(people)), vector, next(iter(labels))))
    sample_seconds += len(samples)

sessions = {row[0] for row in examples}
people = {row[1] for row in examples if row[1] != "NONE"}
if sample_seconds < 100 or len(sessions) < 4 or len(examples) < 8:
    raise SystemExit("Need 100 labeled seconds, eight stable work blocks and four sessions before training.")
group_index = 1 if len(people) >= 4 else 0
groups = sorted({row[group_index] for row in examples})
held_count = max(1, min(len(groups) - 2, math.ceil(len(groups) * .25)))
split = None
for candidate in itertools.combinations(groups, held_count):
    held = set(candidate)
    train = [row for row in examples if row[group_index] not in held]
    valid = [row for row in examples if row[group_index] in held]
    if len(train) >= 4 and len(valid) >= 2 and {row[3] for row in train} == {0, 1} and {row[3] for row in valid} == {0, 1}:
        split = train, valid, held
        break
if split is None:
    raise SystemExit("No person/session-separated validation with both feedback classes. Collect balanced blocks.")
train, valid, held = split
features = BASE + (PROFILE if personalized else [])
mean = [statistics.fmean(row[2][index] for row in train) for index in range(len(features))]
std = [max(.0001, statistics.pstdev(row[2][index] for row in train)) for index in range(len(features))]


def normalize(values):
    return [(value - mean[index]) / std[index] for index, value in enumerate(values)]


weights = [0.] * len(features)
bias = 0.
train_data = [(normalize(row[2]), row[3]) for row in train]
for _ in range(2500):
    gradients = [0.] * len(features)
    bias_gradient = 0.
    for values, label in train_data:
        error = sigmoid(bias + sum(w * x for w, x in zip(weights, values))) - label
        bias_gradient += error
        for index, value in enumerate(values):
            gradients[index] += error * value
    bias -= .04 * bias_gradient / len(train_data)
    for index in range(len(weights)):
        weights[index] -= .04 * (gradients[index] / len(train_data) + .002 * weights[index])


def predict(values):
    return sigmoid(bias + sum(w * x for w, x in zip(weights, normalize(values))))


tp = fp = tn = fn = 0
for _, _, values, label in valid:
    positive = predict(values) >= THRESHOLD
    if positive and label:
        tp += 1
    elif positive:
        fp += 1
    elif label:
        fn += 1
    else:
        tn += 1
balanced = ((tp / max(1, tp + fn)) + (tn / max(1, tn + fp))) / 2
if balanced < .65 or tp == 0:
    raise SystemExit(f"Model NOT deployed: held-out balanced accuracy={balanced:.3f}, TP={tp} FP={fp} TN={tn} FN={fn}. Keep the rule fallback.")

model = {"version": 3 if personalized else 2, "features": features,
         "mean": mean, "std": std, "weights": weights, "bias": bias,
         "validationAccuracy": (tp + tn) / len(valid), "validationBalancedAccuracy": balanced,
         "trainingSamples": len(train), "validationSamples": len(valid),
         "validationUnit": "person" if group_index == 1 else "session"}
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(model, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Wrote {target}; {len(train)} training and {len(valid)} held-out work blocks")
print(f"Held-out {model['validationUnit']} groups={len(held)}; TP={tp} FP={fp} TN={tn} FN={fn}; balanced_accuracy={balanced:.3f}")
print("This predicts reported discomfort; it does NOT prove an intervention improves ergonomics.")
