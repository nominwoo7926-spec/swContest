#!/usr/bin/env python3
"""Report adjacent same-worker before/after blocks without inventing effects."""
import csv
import statistics
import sys
from collections import defaultdict
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "Recordings/Quest/FactoryML/manufacturing_trials_v3.csv"
target = Path(sys.argv[2]) if len(sys.argv) > 2 else root / "Documentation/QuestTask/PairedInterventionReport.md"
if not source.exists():
    raise SystemExit(f"No labeled Quest trials: {source}")

groups = defaultdict(list)
with source.open(encoding="utf-8-sig", newline="") as stream:
    for line, row in enumerate(csv.DictReader(stream), 2):
        try:
            key = row["profile_id"], row["session"], int(row["trial"])
            for name in ("time", "belt_speed", "table_height", "queue_count", "completed",
                         "feedback", "load_l_shoulder", "load_r_shoulder", "load_l_arm",
                         "load_r_arm", "load_l_wrist", "load_r_wrist", "load_torso"):
                float(row[name])
            groups[key].append(row)
        except (KeyError, TypeError, ValueError) as error:
            raise SystemExit(f"Invalid CSV line {line}: {error}")


def value(row, name):
    return float(row[name])


blocks = {}
for key, rows in groups.items():
    rows.sort(key=lambda row: value(row, "time"))
    if len(rows) < 10 or len({row["feedback"] for row in rows}) != 1 or len({row["part_mode"] for row in rows}) != 1:
        continue
    speeds = [value(row, "belt_speed") for row in rows]
    heights = [value(row, "table_height") for row in rows]
    if max(speeds) - min(speeds) > .04 or max(heights) - min(heights) > .04:
        continue
    duration = value(rows[-1], "time") - value(rows[0], "time")
    if duration < 9:
        continue
    peak = [max(value(row, name) for name in (
        "load_l_shoulder", "load_r_shoulder", "load_l_arm", "load_r_arm",
        "load_l_wrist", "load_r_wrist", "load_torso")) for row in rows]
    blocks[key] = {"speed": statistics.fmean(speeds), "height": statistics.fmean(heights),
                   "load": statistics.fmean(peak), "feedback": int(rows[0]["feedback"]),
                   "throughput": max(0, value(rows[-1], "completed") - value(rows[0], "completed")) * 60 / duration,
                   "queue": statistics.fmean(value(row, "queue_count") for row in rows),
                   "mode": rows[0]["part_mode"]}

pairs = []
for (person, session, trial), before in sorted(blocks.items()):
    after = blocks.get((person, session, trial + 1))
    if after is None or after["mode"] != before["mode"]:
        continue
    speed_change = after["speed"] - before["speed"]
    height_change = after["height"] - before["height"]
    # Only one setting may change for an interpretable A/B comparison.
    if (abs(speed_change) >= .03) == (abs(height_change) >= .03):
        continue
    pairs.append((person, session, trial, before, after, "belt" if abs(speed_change) >= .03 else "table"))

lines = ["# VR 작업 전후 쌍 비교", "", f"원본: `{source}`", "",
         f"유효한 안정 작업 구간: {len(blocks)}개 / 한 설정만 바뀐 연속 쌍: {len(pairs)}개", "",
         "한 쌍은 같은 익명 참가자·세션·부품 종류에서 피드백을 남긴 연속 두 구간이다. "
         "두 구간 모두 10초 이상이고, 구간 안의 속도·높이가 안정적이어야 한다. "
         "자세 점수는 센서 추정치이며 실제 근력 측정이 아니다.", "",
         "| 참가자 | 세션 | 전→후 | 변경 | 불편감 | 평균 최고부하 | 처리량(개/분) | 평균 적체 |",
         "| --- | --- | ---: | --- | ---: | ---: | ---: | ---: |"]
for person, session, trial, before, after, setting in pairs:
    lines.append(f"| {person} | {session} | {trial}→{trial + 1} | {setting}: "
                 f"{before['speed' if setting == 'belt' else 'height']:.2f}→{after['speed' if setting == 'belt' else 'height']:.2f} | "
                 f"{before['feedback']}→{after['feedback']} | {before['load']:.1f}→{after['load']:.1f} | "
                 f"{before['throughput']:.2f}→{after['throughput']:.2f} | {before['queue']:.1f}→{after['queue']:.1f} |")
if not pairs:
    lines.append("| — | — | — | 비교 가능한 쌍 없음 | — | — | — | — |")
lines += ["", "## 해석 제한", "",
          "이 표는 관찰된 전후 차이이지 AI 조절의 인과적 효과 증명이 아니다. "
          "순서·피로·학습·개입 선택 편향이 남는다. 대회 효과 주장에는 참가자별 조건 순서 교차, "
          "같은 무게·시간, 여러 반복과 독립 평가가 필요하다.", ""]
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text("\n".join(lines), encoding="utf-8")
print(f"Wrote {target}: {len(pairs)} comparable adjacent pairs")
