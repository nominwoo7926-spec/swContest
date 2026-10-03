using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FactoryTask;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

// Replays a Quest movement recording into the existing third-person camera.
// The MP4 is made from rendered Unity frames, not from the first-person headset video.
[InitializeOnLoad]
public static class FactorySpectatorVideoExport
{
    const string PendingSession="FactoryAvatar.VideoSession";
    const string PendingOutput="FactoryAvatar.VideoOutput";
    const int Width=1280, Height=720, Rate=15;
    static VRSessionRecorder session;
    static SpectatorCameraController spectator;
    static FullBodyAvatarIK avatar;
    static RenderTexture renderTarget, previousTarget;
    static Texture2D image;
    static byte[] pixels;
    static Process encoder;
    static int frame, total;
    static string outputPath;
    static int pendingExit=-1;

    static FactorySpectatorVideoExport()
    {
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
    }

    [MenuItem("Tools/Smart Factory/Avatar Demo/Export Latest Quest Spectator MP4")]
    public static void ExportLatestQuest()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before exporting.");
        string directory=Path.GetFullPath("Recordings/Quest/FactorySessions");
        string source=Directory.GetFiles(directory,"*.fvr").OrderBy(path=>Path.GetFileName(path)).LastOrDefault();
        if(source==null)throw new FileNotFoundException("No Quest .fvr recording has been copied to the project.");
        ResolveFfmpeg();
        string destination=Path.GetFullPath("Recordings/Quest/Spectator");
        Directory.CreateDirectory(destination);
        outputPath=Path.Combine(destination,"Quest_3rdPerson_"+Path.GetFileNameWithoutExtension(source).Replace("Factory_","")+".mp4");
        if(File.Exists(outputPath))throw new IOException("Refusing to overwrite an existing MP4: "+outputPath);
        SessionState.SetString(PendingSession,source);
        SessionState.SetString(PendingOutput,outputPath);
        var xr=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        SessionState.SetBool("QuestTask.XrInitBeforeTest",xr.InitManagerOnStart);
        SessionState.SetBool("QuestTask.RestoreXr",true);
        xr.InitManagerOnStart=false;
        EditorUtility.SetDirty(xr);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(QuestTaskSetup.ScenePath);
        EditorApplication.isPlaying=true;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode&&pendingExit>=0)
        {
            int code=pendingExit;pendingExit=-1;
            EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
            return;
        }
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        string source=SessionState.GetString(PendingSession,"");
        if(string.IsNullOrEmpty(source))return;
        SessionState.SetString(PendingSession,"");
        try
        {
            outputPath=SessionState.GetString(PendingOutput,"");
            session=UnityEngine.Object.FindFirstObjectByType<VRSessionRecorder>();
            spectator=UnityEngine.Object.FindFirstObjectByType<SpectatorCameraController>();
            avatar=UnityEngine.Object.FindFirstObjectByType<FullBodyAvatarIK>();
            if(session==null||spectator==null||avatar==null)throw new InvalidOperationException("Replay scene is missing the session, avatar or spectator camera.");
            session.autoRecord=false;
            if(!session.BeginPlayback(source))throw new InvalidOperationException(session.LastError);
            if(!session.HasRecordedEquipment)
                throw new InvalidOperationException("This older recording has no table-height data. Record a new .fvr with the current APK.");
            Vector3 recorded=session.calibration.BaselineHead;
            Vector3 authored=session.calibration.standingPoint.position;
            if(Vector2.Distance(new Vector2(recorded.x,recorded.z),new Vector2(authored.x,authored.z))>.20f)
                throw new InvalidOperationException("This Quest recording uses an older work position. Record a new .fvr with the current APK before exporting a spectator MP4.");
            session.PlaybackPaused=true;
            total=Mathf.CeilToInt(session.Duration*Rate)+1;
            renderTarget=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);
            image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            pixels=new byte[Width*Height*3];
            previousTarget=spectator.spectatorCamera.targetTexture;
            spectator.spectatorCamera.targetTexture=renderTarget;
            var ffmpeg=ResolveFfmpeg();
            var start=new ProcessStartInfo(ffmpeg,
                "-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size "+Width+"x"+Height+
                " -framerate "+Rate+" -i pipe:0 -vf vflip -an -c:v libx264 -preset ultrafast -crf 22 -pix_fmt yuv420p -movflags +faststart \""+outputPath+"\"");
            start.UseShellExecute=false;start.RedirectStandardInput=true;start.CreateNoWindow=true;
            encoder=Process.Start(start);
            if(encoder==null)throw new InvalidOperationException("FFmpeg could not start.");
            frame=0;
            ShaderUtil.allowAsyncCompilation=false;
            EditorApplication.update+=WriteNextFrame;
            UnityEngine.Debug.Log("FACTORY_SPECTATOR_EXPORT_STARTED "+source+" frames="+total);
        }
        catch(Exception error){Finish(false,error.Message);}
    }

    static string ResolveFfmpeg()
    {
        string bundled=Path.GetFullPath(".video_tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe");
        if(File.Exists(bundled))return bundled;
        string configured=Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if(!string.IsNullOrEmpty(configured)&&File.Exists(configured))return configured;
        foreach(string directory in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
        {
            if(string.IsNullOrWhiteSpace(directory))continue;
            string candidate=Path.Combine(directory.Trim('"'),"ffmpeg.exe");
            if(File.Exists(candidate))return candidate;
        }
        throw new FileNotFoundException("FFmpeg is required. Install ffmpeg on PATH or set FFMPEG_PATH to ffmpeg.exe.");
    }

    static void WriteNextFrame()
    {
        try
        {
            if(!EditorApplication.isPlaying||session==null||!session.IsPlayback)throw new InvalidOperationException("Playback stopped before export finished.");
            if(frame>=total){Finish(true,null);return;}
            float wanted=Mathf.Min(session.Duration,frame/(float)Rate);
            session.PlaybackPaused=false;
            session.TickPlayback(Mathf.Max(0,wanted-session.PlaybackSeconds));
            session.PlaybackPaused=true;
            avatar.SolvePose(1f/Rate);
            Canvas.ForceUpdateCanvases();
            spectator.spectatorCamera.Render();
            var previous=RenderTexture.active;
            RenderTexture.active=renderTarget;
            image.ReadPixels(new Rect(0,0,Width,Height),0,0);
            RenderTexture.active=previous;
            NativeArray<byte> raw=image.GetRawTextureData<byte>();
            raw.CopyTo(pixels);
            encoder.StandardInput.BaseStream.Write(pixels,0,pixels.Length);
            frame++;
            if(frame%150==0)UnityEngine.Debug.Log("FACTORY_SPECTATOR_EXPORT_PROGRESS "+frame+"/"+total);
        }
        catch(Exception error){Finish(false,error.Message);}
    }

    static void Finish(bool success,string error)
    {
        EditorApplication.update-=WriteNextFrame;
        try
        {
            if(encoder!=null)
            {
                encoder.StandardInput.BaseStream.Flush();
                encoder.StandardInput.Close();
                if(!encoder.WaitForExit(30000))throw new TimeoutException("FFmpeg did not finish the MP4.");
                if(encoder.ExitCode!=0)throw new IOException("FFmpeg exited with code "+encoder.ExitCode);
            }
            if(success&&(!File.Exists(outputPath)||new FileInfo(outputPath).Length<10000))throw new IOException("The rendered MP4 is missing or empty.");
        }
        catch(Exception failure){success=false;error=failure.Message;}
        if(spectator!=null&&spectator.spectatorCamera!=null)spectator.spectatorCamera.targetTexture=previousTarget;
        if(renderTarget!=null){renderTarget.Release();UnityEngine.Object.DestroyImmediate(renderTarget);}
        if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        encoder?.Dispose();encoder=null;session=null;spectator=null;avatar=null;renderTarget=null;image=null;pixels=null;
        UnityEngine.Debug.Log(success?"FACTORY_SPECTATOR_EXPORT_OK "+outputPath:"FACTORY_SPECTATOR_EXPORT_FAILED "+error);
        SessionState.SetInt("QuestTask.TestExitCode",success?0:1);
        if(Application.isBatchMode)pendingExit=success?0:1;
        EditorApplication.isPlaying=false;
    }
}
