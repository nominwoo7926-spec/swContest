using FactoryTask;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class FactoryReplayLauncher
{
    const string Pending="FactoryAvatar.ReplayPath";
    static double verifyAt;
    static FactoryReplayLauncher(){EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.update+=VerifyReplay;}
    [MenuItem("Tools/Smart Factory/Avatar Demo/Replay Session File (No Headset)")]
    public static void Open()
    {
        string path=EditorUtility.OpenFilePanel("Open Quest work recording","Recordings","fvr");
        if(string.IsNullOrEmpty(path))return;
        StartReplay(path);
    }
    static void StartReplay(string path)
    {
        if(EditorApplication.isPlaying)
        {
            var session=Object.FindFirstObjectByType<VRSessionRecorder>();
            if(session==null||!session.BeginPlayback(path))EditorUtility.DisplayDialog("Replay",session==null?"Open the Quest task scene first.":session.LastError,"OK");
            FactorySpectatorWindow.Open();return;
        }
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene(QuestTaskSetup.ScenePath);
        var general=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        SessionState.SetBool("QuestTask.XrInitBeforeTest",general.InitManagerOnStart);
        SessionState.SetBool("QuestTask.RestoreXr",true);
        general.InitManagerOnStart=false;EditorUtility.SetDirty(general);AssetDatabase.SaveAssets();
        SessionState.SetString(Pending,path);EditorApplication.isPlaying=true;
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;SessionState.SetString(Pending,"");
        var session=Object.FindFirstObjectByType<VRSessionRecorder>();session.autoRecord=false;
        if(!session.BeginPlayback(path))Debug.LogError("Replay failed: "+session.LastError);
        if(!Application.isBatchMode)FactorySpectatorWindow.Open();
        if(SessionState.GetBool("FactoryAvatar.VerifyReplay",false))verifyAt=EditorApplication.timeSinceStartup+2;
    }
    public static void VerifyHeadsetFreeReplayBatch()
    {
        string file=File.ReadAllText("Documentation/Avatar/LatestVerifiedSession.txt").Trim();
        SessionState.SetBool("FactoryAvatar.VerifyReplay",true);
        StartReplay(Path.GetFullPath(Path.Combine("Documentation/Avatar",file)));
    }
    static void VerifyReplay()
    {
        if(verifyAt==0||EditorApplication.timeSinceStartup<verifyAt||!EditorApplication.isPlaying)return;
        verifyAt=0;SessionState.SetBool("FactoryAvatar.VerifyReplay",false);
        var session=Object.FindFirstObjectByType<VRSessionRecorder>();
        bool valid=session!=null&&session.IsPlayback&&session.tracking.ExternalPlayback&&session.calibration.IsCalibrated&&!session.conveyor.enabled;
        File.WriteAllText("Documentation/Avatar/HeadsetFreeReplay.txt",valid?"PASSED: fresh Play mode starts recorded playback without an XR headset; live simulation disabled.":"FAILED");
        SessionState.SetInt("QuestTask.TestExitCode",valid?0:1);EditorApplication.isPlaying=false;
    }
}
