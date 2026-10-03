#!/usr/bin/env python3
"""Summarize real Quest trials; never manufacture a performance improvement."""
import csv
import statistics
import sys
from collections import defaultdict
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "Recordings/Quest/FactoryML/manufacturing_trials_v3.csv"
target = Path(sys.argv[2]) if len(sys.argv) > 2 else root / "Documentation/QuestTask/ExperimentReport.md"
if not source.exists():
    raise SystemExit(f"No real labeled Quest data found: {source}. No result report was generated.")

trials = defaultdict(list)
with source.open(encoding="utf-8-sig", newline="") as stream:
    for line, row in enumerate(csv.DictReader(stream), 2):
        try:
            key = row["session"], row["trial"]
            for name in ("time", "load_average", "load_l_shoulder", "load_r_shoulder",
                         "load_l_arm", "load_r_arm", "load_l_wrist", "load_r_wrist",
                         "load_torso", "queue_count", "belt_speed", "table_height",
                         "completed", "part_mode", "agent_auto", "feedback"):
                float(row[name])
            trials[key].append(row)
        except (KeyError, TypeError, ValueError) as error:
            raise SystemExit(f"Invalid CSV line {line}: {error}")

if not trials:
    raise SystemExit("The labeled dataset is empty. No result report was generated.")


def number(row, name):
    return float(row[name])


summaries = []
excluded = []
for (session, trial), rows in sorted(trials.items()):
    rows.sort(key=lambda row: number(row, "time"))
    modes = {row["part_mode"] for row in rows}
    automatic = {row["agent_auto"] for row in rows}
    feedback = {row["feedback"] for row in rows}
    if len(rows) < 10 or len(modes) != 1 or len(automatic) != 1 or len(feedback) != 1:
        excluded.append((session, trial, "fewer than 10 seconds or a condition changed mid-trial"))
        continue
    duration = number(rows[-1], "time") - number(rows[0], "time") + 1
    if duration < 10:
        excluded.append((session, trial, "duration below 10 seconds"))
        continue
    peak = [max(number(row, name) for name in (
        "load_l_shoulder", "load_r_shoulder", "load_l_arm", "load_r_arm",
        "load_l_wrist", "load_r_wrist", "load_torso")) for row in rows]
    completed = max(0, number(rows[-1], "completed") - number(rows[0], "completed"))
    summaries.append({
        "session": session, "trial": trial, "mode": "AUTO" if automatic == {"1"} else "MANUAL",
        "part": next(iter(modes)), "seconds": duration, "completed": completed,
        "parts_per_min": completed * 60 / duration,
        "mean_peak_load": statistics.fmean(peak),
        "high_load_pct": 100 * sum(value >= 65 for value in peak) / len(peak),
        "max_queue": max(number(row, "queue_count") for row in rows),
        "feedback": int(next(iter(feedback))),
    })

if not summaries:
    raise SystemExit("No valid constant-condition trials. No result report was generated.")

lines = [
    "# Quest 실측 세션 요약 — 자동 생성",
    "",
    f"원본: `{source}`  ",
    f"유효 시험 구간: {len(summaries)}개 / 제외: {len(excluded)}개  ",
    f"구분되는 앱 세션: {len({item['session'] for item in summaries})}개",
    "",
    "이 문서는 앱 로그의 **기술 통계**다. `load`는 자세 기반 추정치이며 의학적·생체역학적 측정값이 아니다. "
    "자동/수동 배정이 무작위이고 참가자·무게·시작 상태가 같다는 증거가 없으면 인과적 효과라고 해석하지 않는다.",
    "",
    "| 세션 | 구간 | 공급 모드 | 제어 | 시간(s) | 완료 | 개/분 | 평균 최고부하 | 65 이상 비율 | 최대 적체 | 불편함(1-5) |",
    "| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
]
for item in summaries:
    lines.append(f"| {item['session']} | {item['trial']} | {item['part']} | {item['mode']} | "
                 f"{item['seconds']:.0f} | {item['completed']:.0f} | {item['parts_per_min']:.2f} | "
                 f"{item['mean_peak_load']:.1f} | {item['high_load_pct']:.1f}% | "
                 f"{item['max_queue']:.0f} | {item['feedback']} |")

lines += ["", "## 비교 가능성 점검", ""]
by_condition = defaultdict(lambda: {"AUTO": [], "MANUAL": []})
for item in summaries:
    by_condition[(item["session"], item["part"])][item["mode"]].append(item)
matched = [pair for pair in by_condition.values() if pair["AUTO"] and pair["MANUAL"]]
if matched:
    lines.append(f"동일 앱 세션·공급 모드 안에 자동/수동 구간이 함께 있는 조합: {len(matched)}개. "
                 "같은 참가자인지는 앱 로그만으로 보장되지 않으며, 순서·피로도·시작 상태 차이도 통제해야 한다.")
    for metric, label, unit in (("parts_per_min", "처리량", "개/분"),
                                ("mean_peak_load", "평균 최고부하", "점"),
                                ("high_load_pct", "고부하 시간 비율", "%"),
                                ("feedback", "불편함", "점")):
        differences = [statistics.fmean(item[metric] for item in pair["AUTO"]) -
                       statistics.fmean(item[metric] for item in pair["MANUAL"]) for pair in matched]
        lines.append(f"- 자동−수동 {label}: {statistics.fmean(differences):+.2f}{unit} "
                     f"(조합 {len(differences)}개, 기술 통계)")
else:
    lines.append("같은 세션·공급 모드의 자동/수동 쌍이 없다. **효과 비교를 주장할 수 없다.**")
if excluded:
    lines += ["", "## 제외 구간", ""]
    lines += [f"- `{session}` / {trial}: {reason}" for session, trial, reason in excluded]
lines += ["", "## 제출 전 필수 확인", "",
          "- 참가자 동의와 익명 코드, 조건 순서, 시작 책상·벨트 값을 별도 실험 기록지에 남긴다.",
          "- 영상의 수치가 이 CSV와 일치하는지 확인한다. 실제로 수행하지 않은 비교는 쓰지 않는다.",
          "- 학습 모델의 예측 정확도와 자동 개입의 효과는 서로 다른 주장으로 분리한다.", ""]
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text("\n".join(lines), encoding="utf-8")
print(f"Wrote {target}: {len(summaries)} valid trials, {len(matched)} matched conditions, {len(excluded)} excluded")
