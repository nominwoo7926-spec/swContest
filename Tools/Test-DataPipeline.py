#!/usr/bin/env python3
"""Synthetic pipeline regression test. Never use these rows as study evidence."""
import csv
import json
import subprocess
import sys
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
headers = [
    "session", "profile_id", "height_cm", "reach_cm", "height_entered", "reach_entered",
    "trial", "time", "head_drop", "head_forward", "left_reach", "right_reach",
    "left_speed", "right_speed", "held_weight", "belt_speed", "table_height",
    "table_target", "pickup_hand_height", "pickup_head_drop", "queue_count", "part_mode",
    "load_l_shoulder", "load_r_shoulder", "load_l_arm", "load_r_arm", "load_l_wrist",
    "load_r_wrist", "load_torso", "load_average", "completed", "agent_auto",
    "agent_decision", "feedback",
]
with tempfile.TemporaryDirectory(prefix="quest_pipeline_test_") as directory:
    temporary = Path(directory)
    dataset = temporary / "synthetic.csv"
    with dataset.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=headers)
        writer.writeheader()
        for session_number in range(4):
            for trial in range(2):
                high = trial == 1
                for second in range(15):
                    load = (82 if high else 18) + second % 3
                    writer.writerow({
                        "session": f"synthetic_{session_number}",
                        "profile_id": f"synthetic_person_{session_number}",
                        "height_cm": 160 + session_number * 3, "reach_cm": 70 + session_number * 2,
                        "height_entered": 1, "reach_entered": 1, "trial": trial,
                        "time": trial * 15 + second, "head_drop": .2 if high else .02,
                        "head_forward": .2 if high else .02,
                        "left_reach": .7 if high else .2, "right_reach": .6 if high else .2,
                        "left_speed": .2, "right_speed": .2, "held_weight": 5 if high else 1,
                        "belt_speed": .38, "table_height": .75 if high else .99,
                        "table_target": .75 if high else .99, "pickup_hand_height": .9,
                        "pickup_head_drop": .2 if high else .02, "queue_count": 8 if high else 2,
                        "part_mode": 3, "load_l_shoulder": load, "load_r_shoulder": load,
                        "load_l_arm": load, "load_r_arm": load, "load_l_wrist": load,
                        "load_r_wrist": load, "load_torso": load, "load_average": load,
                        "completed": trial * 3 + second // 5, "agent_auto": int(not high),
                        "agent_decision": "SYNTHETIC_TEST_ONLY", "feedback": 5 if high else 2,
                    })
    model = temporary / "model.json"
    report = temporary / "report.md"
    training = subprocess.run([sys.executable, str(root / "Tools/Train-ErgonomicsModel.py"),
                               str(dataset), str(model)], text=True, capture_output=True)
    if training.returncode:
        raise SystemExit("Training regression failed:\n" + training.stdout + training.stderr)
    data = json.loads(model.read_text(encoding="utf-8"))
    if data["version"] != 3 or data["validationUnit"] != "person" or not data["trainingSamples"] or not data["validationSamples"]:
        raise SystemExit("Person-separated v3 split failed")
    analysis = subprocess.run([sys.executable, str(root / "Tools/Analyze-QuestExperiment.py"),
                               str(dataset), str(report)], text=True, capture_output=True)
    if analysis.returncode or "동일 앱 세션" not in report.read_text(encoding="utf-8"):
        raise SystemExit("Experiment report regression failed:\n" + analysis.stdout + analysis.stderr)
    paired = temporary / "paired.md"
    comparison = subprocess.run([sys.executable, str(root / "Tools/Analyze-PairedInterventions.py"),
                                 str(dataset), str(paired)], text=True, capture_output=True)
    if comparison.returncode or "연속 쌍: 4개" not in paired.read_text(encoding="utf-8"):
        raise SystemExit("Paired effect report regression failed:\n" + comparison.stdout + comparison.stderr)
    print("PASS: four-person synthetic holdout, feedback training and four paired pre/post blocks")
    print("Synthetic data and model were confined to a temporary directory and deleted.")
