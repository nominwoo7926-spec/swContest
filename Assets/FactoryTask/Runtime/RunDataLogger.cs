using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FactoryTask
{
    // Per-run load data for comparing the baseline (green) and AI (blue) modes.
    // One CSV per finished run (one row per part placed in the bin) plus one summary row per run in
    // Runs_Summary.csv. Only runs that reach the full quota are written; restarted runs are dropped.
    // Scores are the estimator's raw 0-100 values (not the heatmap colour mapping).
    // Files: <persistentDataPath>/LoadData (on Quest: /sdcard/Android/data/<package>/files/LoadData).
    public sealed class RunDataLogger : MonoBehaviour
    {
        public static string DataDirectory => Path.Combine(Application.persistentDataPath, "LoadData");
        [Tooltip("Optional folder instead of DataDirectory (used by the verification test).")]
        public string directoryOverride;
        string Folder => string.IsNullOrEmpty(directoryOverride) ? DataDirectory : directoryOverride;
        static readonly string[] Regions = { "왼어깨", "오른어깨", "왼팔", "오른팔", "왼손목", "오른손목", "허리" };

        public struct BoxRecord
        {
            public int index;
            public float completedAt, holdSeconds, beltSpeed, tableHeight, rulaMean, rulaMax;
            public float[] regionMean, regionMax;
        }

        public string LastWrittenFile { get; private set; }
        readonly List<BoxRecord> boxes = new List<BoxRecord>();
        string mode;
        DateTime startedAt;

        public void BeginRun(string modeName)
        {
            mode = modeName; startedAt = DateTime.Now; boxes.Clear();
        }

        public void AddBox(BoxRecord record) { boxes.Add(record); }

        // Called when a run reaches its quota. Returns false (and writes nothing) if it failed.
        public bool EndRun(float elapsedSeconds, float deadlineSeconds, float finalTableHeight)
        {
            if (boxes.Count == 0) return false;
            try
            {
                Directory.CreateDirectory(Folder);
                string stamp = startedAt.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(Folder, "Run_" + stamp + "_" + (mode.StartsWith("AI") ? "AI" : "Baseline") + ".csv");
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

                string summary = Path.Combine(Folder, "Runs_Summary.csv");
                bool header = !File.Exists(summary);
                var line = new StringBuilder();
                if (header)
                {
                    line.Append("시작시각,모드,완료개수,총소요시간(s),데드라인(200s±2)충족,평균벨트속도(m/s),최종작업대높이(m),RULA평균,RULA최대");
                    foreach (var r in Regions) line.Append(',').Append(r).Append("평균");
                    line.Append(",전체부위평균,상세파일").AppendLine();
                }
                float speed = 0, rulaMean = 0, rulaMax = 0; var regionMean = new float[Regions.Length];
                foreach (var b in boxes)
                {
                    speed += b.beltSpeed; rulaMean += b.rulaMean; rulaMax = Mathf.Max(rulaMax, b.rulaMax);
                    for (int i = 0; i < Regions.Length; i++) regionMean[i] += b.regionMean[i];
                }
                int n = boxes.Count; float overall = 0;
                line.Append(startedAt.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(mode).Append(',').Append(n)
                    .Append(',').Append(F(elapsedSeconds)).Append(',').Append(elapsedSeconds <= deadlineSeconds + 2 ? "O" : "X")
                    .Append(',').Append(F(speed / n, "0.000")).Append(',').Append(F(finalTableHeight, "0.000"))
                    .Append(',').Append(F(rulaMean / n)).Append(',').Append(F(rulaMax));
                for (int i = 0; i < Regions.Length; i++) { float m = regionMean[i] / n; overall += m; line.Append(',').Append(F(m)); }
                line.Append(',').Append(F(overall / Regions.Length)).Append(',').Append(Path.GetFileName(path)).AppendLine();
                File.AppendAllText(summary, line.ToString(), new UTF8Encoding(header));
                Debug.Log("[Data] Run saved: " + path);
                return true;
            }
            catch (Exception e) { Debug.LogError("Run data could not be saved: " + e.Message); return false; }
            finally { boxes.Clear(); }
        }

        // A restarted (unfinished) run is not saved.
        public void DiscardRun() { boxes.Clear(); }

        static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
