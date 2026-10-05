using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FactoryTask
{
    // Per-run load data for comparing the baseline (green) and AI (blue) modes.
    // Per finished run: Run_<stamp>_<mode>.csv (one row per part placed in the bin) and
    // Timeline_<stamp>_<mode>.csv (one row per second: belt speed, load, RULA, table height, AI status),
    // plus one summary row per run in Runs_Summary.csv. Only runs that reach the full quota are
    // written; restarted runs are dropped. Scores are the estimator's raw 0-100 values.
    // Files: <persistentDataPath>/LoadData (on Quest: /sdcard/Android/data/<package>/files/LoadData).
    public sealed class RunDataLogger : MonoBehaviour
    {
        public static string DataDirectory => Path.Combine(Application.persistentDataPath, "LoadData");
        [Tooltip("Optional folder instead of DataDirectory (used by the verification test).")]
        public string directoryOverride;
        [Tooltip("Load index (0-100) counted as high load in the summary.")]
        public float highLoad = 60;
        string Folder => string.IsNullOrEmpty(directoryOverride) ? DataDirectory : directoryOverride;
        static readonly string[] Regions = { "왼어깨", "오른어깨", "왼팔", "오른팔", "왼손목", "오른손목", "허리" };

        public struct BoxRecord
        {
            public int index;
            public float completedAt, holdSeconds, beltSpeed, tableHeight, rulaMean, rulaMax;
            public float[] regionMean, regionMax;
        }

        public struct TimelineSample
        {
            public float time, beltSpeed, targetSpeed, requiredSpeed, load, peakLoad, rula, tableHeight;
            public int completed;
            public bool paused;
            public string status;
        }

        // Time-based figures of a run (samples taken while the belt waits at the height prompt are excluded).
        public struct Metrics { public float meanLoad, peakLoad, highLoadShare, highPeakShare, cumulativeLoad; }

        public string LastWrittenFile { get; private set; }
        public string LastTimelineFile { get; private set; }
        readonly List<BoxRecord> boxes = new List<BoxRecord>();
        readonly List<TimelineSample> timeline = new List<TimelineSample>();
        string mode;
        DateTime startedAt;

        public void BeginRun(string modeName)
        {
            mode = modeName; startedAt = DateTime.Now; boxes.Clear(); timeline.Clear();
        }

        public void AddBox(BoxRecord record) { boxes.Add(record); }
        public void AddSample(TimelineSample sample) { if (mode != null) timeline.Add(sample); }

        public static Metrics Compute(IList<TimelineSample> samples, float threshold)
        {
            var m = new Metrics(); float time = 0, high = 0, highPeak = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (s.paused) continue;
                float dt = i > 0 ? Mathf.Clamp(s.time - samples[i - 1].time, 0, 2) : 1;
                time += dt; m.meanLoad += s.load * dt; m.cumulativeLoad += s.load * dt;
                m.peakLoad = Mathf.Max(m.peakLoad, s.peakLoad);
                if (s.load >= threshold) high += dt;
                if (s.peakLoad >= threshold) highPeak += dt;
            }
            if (time > 0) { m.meanLoad /= time; m.highLoadShare = high / time * 100; m.highPeakShare = highPeak / time * 100; }
            return m;
        }

        // Called when a run reaches its quota. Returns false (and writes nothing) if it failed.
        public bool EndRun(float elapsedSeconds, float deadlineSeconds, float finalTableHeight)
        {
            if (boxes.Count == 0) return false;
            try
            {
                Directory.CreateDirectory(Folder);
                string stamp = startedAt.ToString("yyyyMMdd_HHmmss"), tag = mode.StartsWith("AI") ? "AI" : "Baseline";
                string path = Path.Combine(Folder, "Run_" + stamp + "_" + tag + ".csv");
                var sb = new StringBuilder();
                sb.Append("모드,박스번호,완료시각(s),들고있던시간(s),벨트속도(m/s),작업대높이(m),RULA평균,RULA최대");
                foreach (var r in Regions) sb.Append(',').Append(r).Append("평균");
                foreach (var r in Regions) sb.Append(',').Append(r).Append("최대");
                sb.AppendLine();
                foreach (var b in boxes)
                {
                    sb.Append(mode).Append(',').Append(b.index).Append(',').Append(F(b.completedAt)).Append(',').Append(F(b.holdSeconds))
                      .Append(',').Append(F(b.beltSpeed, "0.000")).Append(',').Append(F(b.tableHeight, "0.000"))
                      .Append(',').Append(F(b.rulaMean)).Append(',').Append(F(b.rulaMax));
                    for (int i = 0; i < Regions.Length; i++) sb.Append(',').Append(F(b.regionMean[i]));
                    for (int i = 0; i < Regions.Length; i++) sb.Append(',').Append(F(b.regionMax[i]));
                    sb.AppendLine();
                }
                // UTF-8 with BOM so Excel shows the Korean headers correctly.
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                LastWrittenFile = path;

                string timelinePath = Path.Combine(Folder, "Timeline_" + stamp + "_" + tag + ".csv");
                var tl = new StringBuilder("모드,시간(s),벨트속도(m/s),목표속도(m/s),필요속도(m/s),전체부하,최대부위부하,RULA,작업대높이(m),완료개수,정지중,상태\n");
                foreach (var s in timeline)
                    tl.Append(mode).Append(',').Append(F(s.time, "0.0")).Append(',').Append(F(s.beltSpeed, "0.000")).Append(',').Append(F(s.targetSpeed, "0.000"))
                      .Append(',').Append(F(s.requiredSpeed, "0.000")).Append(',').Append(F(s.load)).Append(',').Append(F(s.peakLoad)).Append(',').Append(F(s.rula, "0.0"))
                      .Append(',').Append(F(s.tableHeight, "0.000")).Append(',').Append(s.completed).Append(',').Append(s.paused ? 1 : 0)
                      .Append(',').Append((s.status ?? "").Replace(',', ' ')).Append('\n');
                File.WriteAllText(timelinePath, tl.ToString(), new UTF8Encoding(true));
                LastTimelineFile = timelinePath;

                string summary = Path.Combine(Folder, "Runs_Summary.csv");
                var head = new StringBuilder("시작시각,모드,완료개수,총소요시간(s),데드라인(200s±2)충족,평균벨트속도(m/s),최종작업대높이(m),RULA평균,RULA최대");
                foreach (var r in Regions) head.Append(',').Append(r).Append("평균");
                head.Append(",전체부위평균,시간평균부하,최대부하,부하60이상시간(%),최대부위60이상시간(%),누적부하(점·s),상세파일,타임라인파일");
                // An older summary with different columns is kept aside instead of being mixed in.
                if (File.Exists(summary))
                {
                    string first;
                    using (var reader = new StreamReader(summary, Encoding.UTF8)) first = reader.ReadLine();
                    if (first != head.ToString()) File.Move(summary, Path.Combine(Folder, "Runs_Summary_old_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"));
                }
                bool header = !File.Exists(summary);
                var line = new StringBuilder();
                if (header) line.Append(head).AppendLine();
                float speed = 0, rulaMean = 0, rulaMax = 0; var regionMean = new float[Regions.Length];
                foreach (var b in boxes)
                {
                    speed += b.beltSpeed; rulaMean += b.rulaMean; rulaMax = Mathf.Max(rulaMax, b.rulaMax);
                    for (int i = 0; i < Regions.Length; i++) regionMean[i] += b.regionMean[i];
                }
                int n = boxes.Count; float overall = 0;
                var m = Compute(timeline, highLoad);
                line.Append(startedAt.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(mode).Append(',').Append(n)
                    .Append(',').Append(F(elapsedSeconds)).Append(',').Append(elapsedSeconds <= deadlineSeconds + 2 ? "O" : "X")
                    .Append(',').Append(F(speed / n, "0.000")).Append(',').Append(F(finalTableHeight, "0.000"))
                    .Append(',').Append(F(rulaMean / n)).Append(',').Append(F(rulaMax));
                for (int i = 0; i < Regions.Length; i++) { float r = regionMean[i] / n; overall += r; line.Append(',').Append(F(r)); }
                line.Append(',').Append(F(overall / Regions.Length)).Append(',').Append(F(m.meanLoad)).Append(',').Append(F(m.peakLoad))
                    .Append(',').Append(F(m.highLoadShare, "0.0")).Append(',').Append(F(m.highPeakShare, "0.0")).Append(',').Append(F(m.cumulativeLoad, "0"))
                    .Append(',').Append(Path.GetFileName(path)).Append(',').Append(Path.GetFileName(timelinePath)).AppendLine();
                File.AppendAllText(summary, line.ToString(), new UTF8Encoding(header));
                Debug.Log("[Data] Run saved: " + path);
                return true;
            }
            catch (Exception e) { Debug.LogError("Run data could not be saved: " + e.Message); return false; }
            finally { boxes.Clear(); timeline.Clear(); }
        }

        // A restarted (unfinished) run is not saved.
        public void DiscardRun() { boxes.Clear(); timeline.Clear(); }

        static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
