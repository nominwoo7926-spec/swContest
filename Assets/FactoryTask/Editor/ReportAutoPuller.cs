using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FactoryTask
{
    [InitializeOnLoad]
    public static class ReportAutoPuller
    {
        const string AdbPath = @"C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe";
        const string DevicePath = "/storage/emulated/0/Android/data/com.nova.smartfactory.questtask/files/Reports/";
        const string MenuToggle = "Tools/Smart Factory/Quest Task/Play 종료 시 자동 Pull";
        const string PrefKey = "FactoryTask.AutoPullOnStop";

        static bool AutoPullEnabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set { EditorPrefs.SetBool(PrefKey, value); Menu.SetChecked(MenuToggle, value); }
        }

        static ReportAutoPuller()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += () => Menu.SetChecked(MenuToggle, AutoPullEnabled);
        }

        [MenuItem(MenuToggle)]
        static void ToggleAutoPull()
        {
            AutoPullEnabled = !AutoPullEnabled;
            UnityEngine.Debug.Log($"[ReportAutoPuller] 자동 Pull {(AutoPullEnabled ? "활성화" : "비활성화")}");
        }

        [MenuItem(MenuToggle, true)]
        static bool ValidateToggle() { Menu.SetChecked(MenuToggle, AutoPullEnabled); return true; }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && AutoPullEnabled)
                PullNow();
        }

        [MenuItem("Tools/Smart Factory/Quest Task/지금 보고서 Pull")]
        public static void PullNow()
        {
            if (!File.Exists(AdbPath))
            {
                UnityEngine.Debug.LogWarning($"[ReportAutoPuller] adb 없음: {AdbPath}");
                return;
            }

            string dest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports"));
            Directory.CreateDirectory(dest);

            var psi = new ProcessStartInfo(AdbPath, $"pull \"{DevicePath}\" \"{dest}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                string error  = proc.StandardError.ReadToEnd();
                proc.WaitForExit(10000);

                if (proc.ExitCode == 0)
                {
                    UnityEngine.Debug.Log($"[ReportAutoPuller] Pull 완료 → {dest}\n{output.Trim()}");
                    OpenLatestReport(dest);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[ReportAutoPuller] Pull 실패 (Quest2 연결 확인)\n{error.Trim()}");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[ReportAutoPuller] 오류: {e.Message}");
            }
        }

        static void OpenLatestReport(string dir)
        {
            var files = Directory.GetFiles(dir, "*.html");
            if (files.Length == 0) return;
            Array.Sort(files);
            Process.Start(files[files.Length - 1]);
        }
    }
}
