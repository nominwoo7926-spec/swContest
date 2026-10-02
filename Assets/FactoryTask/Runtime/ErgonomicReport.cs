using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FactoryTask
{
    [DefaultExecutionOrder(200)]
    public sealed class ErgonomicReport : MonoBehaviour
    {
        [Header("References")]
        public BodyLoadEstimator estimator;
        public VRSessionRecorder recorder;
        public XRGrabTaskTracker task;
        public XRTrackingProvider tracking;

        [Header("Manual trigger (Right B button hold)")]
        [Tooltip("B 버튼을 이 시간(초) 이상 누르면 보고서를 생성합니다.")]
        public float holdSeconds = 2f;

        public string LastReportPath { get; private set; }

        // BodyLoadVisualizer가 B버튼 진행률을 표시하는 데 사용
        public float HoldProgress => holdSeconds > 0 ? Mathf.Clamp01(holdTimer / holdSeconds) : 0f;

        bool wasRecording;
        float holdTimer;
        bool generated;

        void Start()
        {
            if (recorder == null)
                recorder = GetComponentInParent<VRSessionRecorder>() ?? FindFirstObjectByType<VRSessionRecorder>();
            if (tracking == null)
                tracking = GetComponentInParent<XRTrackingProvider>() ?? FindFirstObjectByType<XRTrackingProvider>();
        }

        void Update()
        {
            // ── 자동: 세션 녹화가 끝나는 순간 생성 ──────────────────────
            if (recorder != null)
            {
                bool isRecording = recorder.IsRecording;
                if (wasRecording && !isRecording && !generated)
                {
                    generated = true;
                    GenerateReport();
                }
                wasRecording = isRecording;
                if (isRecording) generated = false;
            }

            // ── 수동: 오른쪽 B 버튼 2초 홀드 ──────────────────────────
            if (tracking != null && tracking.ReportButton)
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= holdSeconds)
                {
                    holdTimer = 0;
                    GenerateReport();
                }
            }
            else
            {
                holdTimer = 0;
            }
        }

        void OnApplicationQuit()
        {
            if (!generated && estimator != null && task != null && estimator.SessionSeconds > 5f)
                GenerateReport();
        }

        static readonly string[] RegionNames =
        {
            "왼쪽 어깨", "오른쪽 어깨", "왼쪽 팔", "오른쪽 팔",
            "왼쪽 손목", "오른쪽 손목", "몸통"
        };

        // ── Public API ──────────────────────────────────────────────────────

        public string GenerateReport()
        {
            // SVG 좌표에 로케일 의존 소수점 구분자(,)가 들어가면 파싱 오류 발생
            var prevCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                float[] scores = new float[7];
                for (int i = 0; i < 7; i++) scores[i] = estimator.SessionAvgScore(i);
                float sessionAvg = estimator.SessionAverage;

                // 기준 데이터가 없으면 현재 세션을 기준으로 저장 (비교 없음)
                float[] baseline = LoadBaseline();
                if (baseline == null) SaveBaseline(scores, sessionAvg);

#if UNITY_EDITOR
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports"));
#else
                string dir = Path.Combine(Application.persistentDataPath, "Reports");
#endif
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "ErgoReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html");

                float duration = recorder != null ? recorder.RecordingDuration : estimator.SessionSeconds;
                File.WriteAllText(path, BuildHtml(scores, duration, task.CompletedTotal, sessionAvg, baseline), Encoding.UTF8);
                LastReportPath = path;
                Debug.Log($"[ErgonomicReport] 보고서 저장 완료: {path}");

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start(path);
#endif
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ErgonomicReport] 보고서 생성 오류: {ex}");
                return null;
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prevCulture;
            }
        }

        // ── Baseline 저장/로드 ───────────────────────────────────────────────

        [Serializable]
        class BaselineData { public float[] scores; public float avg; public string timestamp; }

        static string BaselinePath => Path.Combine(Application.persistentDataPath, "ergo_baseline.json");

        static float[] LoadBaseline()
        {
            try
            {
                if (!File.Exists(BaselinePath)) return null;
                var d = JsonUtility.FromJson<BaselineData>(File.ReadAllText(BaselinePath));
                return d?.scores;
            }
            catch { return null; }
        }

        static void SaveBaseline(float[] scores, float avg)
        {
            try
            {
                var d = new BaselineData { scores = scores, avg = avg, timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                File.WriteAllText(BaselinePath, JsonUtility.ToJson(d));
                Debug.Log("[ErgonomicReport] 기준 데이터 저장 완료 (고정 설비 기준)");
            }
            catch { }
        }

        /// <summary>
        /// 고정 설비 비교 기준을 초기화합니다. 다음 보고서 생성 시 현재 세션이 새 기준이 됩니다.
        /// </summary>
        public static void ResetBaseline()
        {
            if (File.Exists(BaselinePath)) File.Delete(BaselinePath);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Smart Factory/Quest Task/Reset Ergo Baseline")]
        static void MenuResetBaseline()
        {
            ResetBaseline();
            Debug.Log("[ErgonomicReport] 기준 데이터 초기화 완료 — 다음 보고서가 새 기준이 됩니다.");
        }
#endif

        // ── HTML 빌더 ────────────────────────────────────────────────────────

        static string RiskLabel(float score) =>
            score < 34f ? "낮음" : score < 67f ? "보통" : "높음";

        static string RiskColor(float score) =>
            score < 34f ? "#27ae60" : score < 67f ? "#e67e22" : "#e74c3c";

        // 7축 레이더(방사형) 차트 — 순수 SVG, JS 없음
        static string BuildRadarChart(float[] scores, float[] baseline)
        {
            const float cx = 200, cy = 210, r = 130;
            const int n = 7;
            string[] labels = { "왼어깨", "오른어깨", "왼팔", "오른팔", "왼손목", "오른손목", "몸통" };

            var sb = new StringBuilder();
            sb.Append("<svg viewBox='0 0 400 420' width='100%' style='max-width:380px' xmlns='http://www.w3.org/2000/svg'>");

            // 격자 폴리곤 (33 / 67 / 100%)
            string[] gridColors = { "#e8f5e9", "#fff3e0", "#fce4ec" };
            for (int level = 1; level <= 3; level++)
            {
                float fr = r * level / 3f;
                var pts = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    float angle = (-90f + i * 360f / n) * Mathf.Deg2Rad;
                    pts.Append(i == 0 ? "" : " ");
                    pts.Append($"{cx + fr * Mathf.Cos(angle):F1},{cy + fr * Mathf.Sin(angle):F1}");
                }
                sb.Append($"<polygon points='{pts}' fill='{gridColors[level-1]}' stroke='#ccc' stroke-width='1'/>");
            }

            // 축선 + 레이블
            for (int i = 0; i < n; i++)
            {
                float angle = (-90f + i * 360f / n) * Mathf.Deg2Rad;
                float ax = cx + r * Mathf.Cos(angle), ay = cy + r * Mathf.Sin(angle);
                sb.Append($"<line x1='{cx:F1}' y1='{cy:F1}' x2='{ax:F1}' y2='{ay:F1}' stroke='#bbb' stroke-width='1'/>");
                float lx = cx + (r + 22) * Mathf.Cos(angle), ly = cy + (r + 22) * Mathf.Sin(angle);
                string anchor = Mathf.Cos(angle) > 0.1f ? "start" : Mathf.Cos(angle) < -0.1f ? "end" : "middle";
                sb.Append($"<text x='{lx:F1}' y='{ly:F1}' text-anchor='{anchor}' dominant-baseline='middle' font-size='11' fill='#555'>{labels[i]}</text>");
            }

            // 기준(고정 설비) 폴리곤 — 빨간 점선
            if (baseline != null)
            {
                var bpts = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    float angle = (-90f + i * 360f / n) * Mathf.Deg2Rad;
                    float sr = r * Mathf.Clamp(baseline[i], 0, 100) / 100f;
                    bpts.Append(i == 0 ? "" : " ");
                    bpts.Append($"{cx + sr * Mathf.Cos(angle):F1},{cy + sr * Mathf.Sin(angle):F1}");
                }
                sb.Append($"<polygon points='{bpts}' fill='rgba(231,76,60,0.12)' stroke='#e74c3c' stroke-width='1.5' stroke-dasharray='5,3'/>");
            }

            // 현재 세션 폴리곤 — 파란 실선
            var spts = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                float angle = (-90f + i * 360f / n) * Mathf.Deg2Rad;
                float sr = r * Mathf.Clamp(scores[i], 0, 100) / 100f;
                spts.Append(i == 0 ? "" : " ");
                spts.Append($"{cx + sr * Mathf.Cos(angle):F1},{cy + sr * Mathf.Sin(angle):F1}");
            }
            sb.Append($"<polygon points='{spts}' fill='rgba(41,128,185,0.28)' stroke='#2980b9' stroke-width='2'/>");

            // 점수 점
            for (int i = 0; i < n; i++)
            {
                float angle = (-90f + i * 360f / n) * Mathf.Deg2Rad;
                float sr = r * Mathf.Clamp(scores[i], 0, 100) / 100f;
                float dx = cx + sr * Mathf.Cos(angle), dy = cy + sr * Mathf.Sin(angle);
                sb.Append($"<circle cx='{dx:F1}' cy='{dy:F1}' r='4' fill='#2980b9'/>");
            }

            // 범례
            if (baseline != null)
            {
                sb.Append("<line x1='10' y1='400' x2='22' y2='400' stroke='#e74c3c' stroke-width='1.5' stroke-dasharray='5,3'/>");
                sb.Append("<text x='28' y='400' font-size='10' fill='#555' dominant-baseline='middle'>고정 설비 기준</text>");
            }
            sb.Append("<circle cx='16' cy='413' r='4' fill='#2980b9'/>");
            sb.Append("<text x='28' y='413' font-size='10' fill='#555' dominant-baseline='middle'>현재 세션</text>");
            sb.Append("</svg>");
            return sb.ToString();
        }

        // 고정 설비 vs Agent 조정 비교 섹션
        static string BuildComparison(float[] scores, float avg, float[] baseline)
        {
            if (baseline == null) return "";

            float baselineAvg = 0;
            foreach (float s in baseline) baselineAvg += s;
            baselineAvg /= 7f;

            float improvement = baselineAvg > 0 ? (baselineAvg - avg) / baselineAvg * 100f : 0f;
            string improvColor = improvement >= 0 ? "#27ae60" : "#e74c3c";
            string improvArrow = improvement >= 0 ? "▼" : "▲";
            string improvLabel = improvement >= 0 ? $"<b style='color:{improvColor}'>{improvArrow} {Mathf.Abs(improvement):F1}% 감소</b>" : $"<b style='color:{improvColor}'>{improvArrow} {Mathf.Abs(improvement):F1}% 증가</b>";

            var sb = new StringBuilder();
            sb.Append("<h2>고정 설비 vs Agent 조정 비교</h2>");
            sb.Append("<div style='background:#f8f9fa;border-radius:8px;padding:18px;margin-bottom:28px'>");

            // 요약 수치
            sb.Append("<div style='display:flex;gap:20px;align-items:center;margin-bottom:16px'>");
            sb.Append($"<div style='flex:1;text-align:center'><div style='font-size:11px;color:#7f8c8d;text-transform:uppercase'>고정 설비 평균</div><div style='font-size:28px;font-weight:700;color:#e74c3c'>{baselineAvg:F1}</div></div>");
            sb.Append($"<div style='text-align:center;font-size:14px'>{improvLabel}</div>");
            sb.Append($"<div style='flex:1;text-align:center'><div style='font-size:11px;color:#7f8c8d;text-transform:uppercase'>Agent 조정 후 평균</div><div style='font-size:28px;font-weight:700;color:#2980b9'>{avg:F1}</div></div>");
            sb.Append("</div>");

            // 부위별 비교 테이블
            sb.Append("<table style='width:100%;border-collapse:collapse;font-size:12px'>");
            sb.Append("<tr style='background:#ecf0f1'><th style='padding:7px 10px;text-align:left'>부위</th><th style='padding:7px 10px'>고정 설비</th><th style='padding:7px 10px'>조정 후</th><th style='padding:7px 10px'>변화</th></tr>");
            for (int i = 0; i < 7; i++)
            {
                float delta = baseline[i] - scores[i];
                string dc = delta >= 0 ? "#27ae60" : "#e74c3c";
                string da = delta >= 0 ? "▼" : "▲";
                sb.Append($"<tr><td style='padding:6px 10px'>{RegionNames[i]}</td><td style='padding:6px 10px;text-align:center'>{baseline[i]:F1}</td><td style='padding:6px 10px;text-align:center'>{scores[i]:F1}</td><td style='padding:6px 10px;text-align:center;color:{dc}'>{da} {Mathf.Abs(delta):F1}</td></tr>");
            }
            sb.Append("</table></div>");
            return sb.ToString();
        }

        // 좌우 부하 불균형 분석
        static string BuildImbalance(float[] scores)
        {
            var sb = new StringBuilder();
            sb.Append("<h2>좌우 부하 불균형 분석</h2>");
            sb.Append("<table style='margin-bottom:28px'><thead><tr><th>부위</th><th>왼쪽</th><th>오른쪽</th><th>차이</th><th>평가</th></tr></thead><tbody>");

            void Row(string name, float left, float right)
            {
                float diff = Mathf.Abs(left - right);
                string eval = diff < 10 ? "<span style='color:#27ae60'>균형</span>"
                            : diff < 25 ? "<span style='color:#e67e22'>경미한 불균형</span>"
                                        : "<span style='color:#e74c3c'>불균형 주의</span>";
                sb.Append($"<tr><td>{name}</td><td style='text-align:center'>{left:F1}</td><td style='text-align:center'>{right:F1}</td><td style='text-align:center'>{diff:F1}</td><td>{eval}</td></tr>");
            }

            Row("어깨", scores[0], scores[1]);
            Row("팔",   scores[2], scores[3]);
            Row("손목", scores[4], scores[5]);

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string BuildRecommendations(float[] scores)
        {
            float shoulderAvg = (scores[0] + scores[1]) * 0.5f;
            float armAvg     = (scores[2] + scores[3]) * 0.5f;
            float wristAvg   = (scores[4] + scores[5]) * 0.5f;
            float torso      =  scores[6];
            float overall    = 0f;
            foreach (float s in scores) overall += s;
            overall /= 7f;

            var sb = new StringBuilder("<ul>");

            if (shoulderAvg >= 67f)
                sb.Append("<li><b>어깨 부하 높음:</b> 부품 공급 위치를 작업자 가까이 이동하여 팔 뻗기 거리를 줄이세요. 수평 도달 거리 0.5m 이내 유지를 권장합니다.</li>");
            else if (shoulderAvg >= 34f)
                sb.Append("<li><b>어깨 부하 보통:</b> 부품 공급 위치를 작업자 방향으로 약간 조정하면 어깨 부하를 줄일 수 있습니다.</li>");

            if (wristAvg >= 67f)
                sb.Append("<li><b>손목 부하 높음:</b> 부품 무게 분산(1 kg 이하 권장) 또는 작업 반복 횟수를 줄이세요. 손목 받침대 사용을 고려하세요.</li>");
            else if (wristAvg >= 34f)
                sb.Append("<li><b>손목 부하 보통:</b> 작업 사이 짧은 휴식으로 손목 부하를 관리하세요.</li>");

            if (torso >= 67f)
                sb.Append("<li><b>몸통 부하 높음:</b> 작업대 높이를 높여 허리 굽힘을 방지하세요. 권장 작업대 높이는 팔꿈치 높이 기준 ±5 cm입니다.</li>");
            else if (torso >= 34f)
                sb.Append("<li><b>몸통 부하 보통:</b> 작업대 높이를 소폭 조정하면 몸통 부하를 줄일 수 있습니다.</li>");

            if (armAvg >= 67f)
                sb.Append("<li><b>팔 부하 높음:</b> 작업 지속 시간을 단축하고 작업 간격을 늘리세요.</li>");

            if (overall < 34f)
                sb.Append("<li><b>종합 평가 ✓</b> 전반적인 부하 수준이 양호합니다. 현재 설비 배치를 유지하세요.</li>");
            else if (overall >= 67f)
                sb.Append("<li><b>종합 평가 !</b> 전반적인 부하 수준이 높습니다. 즉각적인 설비 재배치 검토를 권장합니다.</li>");

            sb.Append("</ul>");
            return sb.ToString();
        }

        static string BuildHtml(float[] scores, float duration, int completions, float avg, float[] baseline)
        {
            var ts  = TimeSpan.FromSeconds(duration);
            var now = DateTime.Now;

            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html lang='ko'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>인체공학 부하 평가 보고서</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Malgun Gothic',Arial,sans-serif;background:#f4f6f8;color:#222;padding:30px}
.card{background:#fff;max-width:820px;margin:0 auto;border-radius:10px;box-shadow:0 2px 12px rgba(0,0,0,.1);overflow:hidden}
.header{background:linear-gradient(135deg,#1a252f,#2980b9);color:#fff;padding:30px 35px}
.header h1{font-size:22px;margin-bottom:6px}
.header p{opacity:.75;font-size:13px}
.body{padding:30px 35px}
.meta{display:grid;grid-template-columns:1fr 1fr;gap:10px;background:#f0f4f8;border-radius:8px;padding:18px;margin-bottom:28px}
.meta-item label{font-size:11px;color:#7f8c8d;text-transform:uppercase;letter-spacing:.5px}
.meta-item span{display:block;font-size:18px;font-weight:700;color:#1a252f;margin-top:2px}
h2{font-size:15px;color:#2c3e50;margin-bottom:14px;padding-bottom:6px;border-bottom:2px solid #ecf0f1}
table{width:100%;border-collapse:collapse;margin-bottom:28px;font-size:13px}
th{background:#2980b9;color:#fff;padding:10px 12px;text-align:left;font-weight:600}
td{padding:10px 12px;border-bottom:1px solid #ecf0f1;vertical-align:middle}
tr:last-child td{border-bottom:none}
tr:hover td{background:#f8fbfe}
.bar-bg{background:#ecf0f1;border-radius:4px;height:10px;width:160px}
.bar-fill{height:10px;border-radius:4px}
.badge{display:inline-block;padding:3px 10px;border-radius:10px;color:#fff;font-size:11px;font-weight:700}
.rec{background:#eaf4fb;border-left:4px solid #2980b9;border-radius:4px;padding:16px 20px;font-size:13px}
.rec ul{padding-left:20px}
.rec li{margin:7px 0;line-height:1.6}
.footer{margin-top:28px;text-align:center;font-size:11px;color:#bdc3c7;border-top:1px solid #ecf0f1;padding-top:16px}
.print-btn{display:block;width:fit-content;margin:0 auto 20px;padding:10px 24px;background:#2980b9;color:#fff;border:none;border-radius:6px;cursor:pointer;font-size:13px;font-family:inherit}
.radar-wrap{display:flex;justify-content:center;margin-bottom:28px}
@media print{.print-btn{display:none}}
</style>
</head>
<body>
<div class='card'>
<div class='header'>
  <h1>인체공학 부하 평가 보고서</h1>
  <p>NOVA Smart Factory &nbsp;|&nbsp; Ergonomic Assessment Report</p>
</div>
<div class='body'>
<button class='print-btn' onclick='window.print()'>PDF로 저장 / 인쇄</button>
");
            // 메타 정보
            sb.Append($@"<div class='meta'>
  <div class='meta-item'><label>생성 일시</label><span style='font-size:14px'>{now:yyyy-MM-dd HH:mm:ss}</span></div>
  <div class='meta-item'><label>세션 시간</label><span>{ts.Minutes:D2}분 {ts.Seconds:D2}초</span></div>
  <div class='meta-item'><label>완료 부품 수</label><span>{completions} 개</span></div>
  <div class='meta-item'><label>전체 평균 부하</label><span>{avg:F1} <small style='font-size:12px;font-weight:400'>/ 100</small></span></div>
</div>
");
            // 레이더 차트
            sb.Append("<h2>부하 분포 레이더 차트</h2>");
            sb.Append($"<div class='radar-wrap'>{BuildRadarChart(scores, baseline)}</div>");

            // 고정 설비 vs Agent 조정 비교
            sb.Append(BuildComparison(scores, avg, baseline));

            // 신체 부위별 부하 테이블
            sb.Append("<h2>신체 부위별 부하 지수</h2>");
            sb.Append("<table><thead><tr><th>신체 부위</th><th>점수</th><th>부하 현황</th><th>위험 등급</th></tr></thead><tbody>");
            for (int i = 0; i < 7; i++)
            {
                float  s     = scores[i];
                string color = RiskColor(s);
                string risk  = RiskLabel(s);
                sb.Append($"<tr><td>{RegionNames[i]}</td><td><b>{s:F1}</b></td><td><div class='bar-bg'><div class='bar-fill' style='width:{s}%;background:{color}'></div></div></td><td><span class='badge' style='background:{color}'>{risk}</span></td></tr>");
            }
            sb.Append("</tbody></table>");

            // 좌우 불균형
            sb.Append(BuildImbalance(scores));

            // 개선 권고사항
            sb.Append("<h2>설비 배치 개선 권고사항</h2>");
            sb.Append($"<div class='rec'>{BuildRecommendations(scores)}</div>");

            sb.Append($"<div class='footer'>본 보고서는 NOVA Smart Factory VR 시스템에 의해 자동 생성되었습니다. &nbsp;|&nbsp; {now:yyyy-MM-dd HH:mm:ss}</div>");
            sb.Append("</div></div></body></html>");
            return sb.ToString();
        }
    }
}
