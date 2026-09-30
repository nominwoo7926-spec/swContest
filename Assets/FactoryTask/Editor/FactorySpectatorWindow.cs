using FactoryTask;
using UnityEditor;
using UnityEngine;

public sealed class FactorySpectatorWindow : EditorWindow
{
    SpectatorCameraController viewer;
    double nextFind;
    double nextRepaint;
    bool clean;
    [MenuItem("Tools/Smart Factory/Avatar Demo/Open Spectator Window")]
    public static void Open(){GetWindow<FactorySpectatorWindow>("Factory Spectator").Show();}
    void OnEnable(){EditorApplication.update+=RefreshFrame;}
    void OnDisable(){EditorApplication.update-=RefreshFrame;}
    void RefreshFrame(){if(EditorApplication.timeSinceStartup<nextRepaint)return;nextRepaint=EditorApplication.timeSinceStartup+1.0/30;Repaint();}
    void OnGUI()
    {
        if(viewer==null&&EditorApplication.timeSinceStartup>nextFind){viewer=Object.FindFirstObjectByType<SpectatorCameraController>();nextFind=EditorApplication.timeSinceStartup+1;}
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.F8){clean=!clean;Event.current.Use();}
        float toolbar=clean?0:26;
        if(!clean)
        {
            using(new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if(GUILayout.Button("Clean view (F8)",EditorStyles.toolbarButton))clean=true;
                using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying||viewer==null))
                {
                    var session=viewer!=null?viewer.session:null;
                    if(GUILayout.Button("Open recording",EditorStyles.toolbarButton))
                    {
                        string path=EditorUtility.OpenFilePanel("Replay Factory session",VRSessionRecorder.SessionDirectory,"fvr");
                        if(!string.IsNullOrEmpty(path)&&!session.BeginPlayback(path))EditorUtility.DisplayDialog("Replay",session.LastError,"OK");
                    }
                    if(session!=null&&session.IsPlayback)
                    {
                        if(GUILayout.Button(session.PlaybackPaused?"Play":"Pause",EditorStyles.toolbarButton))session.PlaybackPaused=!session.PlaybackPaused;
                        if(GUILayout.Button("Restart",EditorStyles.toolbarButton))session.RestartPlayback();
                        GUILayout.Label(session.PlaybackSeconds.ToString("F1")+" / "+session.Duration.ToString("F1")+" s",EditorStyles.miniLabel);
                    }
                    else if(session!=null)
                    {
                        if(GUILayout.Button(session.IsRecording?"Stop data recording":"Record data",EditorStyles.toolbarButton)){if(session.IsRecording)session.StopRecording();else session.StartRecording();}
                    }
                }
            }
        }
        if(viewer==null||viewer.output==null){EditorGUI.HelpBox(new Rect(12,40,position.width-24,55),"Open SmartFactory_QuestTask and enter Play mode. This window is the PC/OBS view.",MessageType.Info);return;}
        var area=new Rect(0,toolbar,position.width,position.height-toolbar);
        EditorGUI.DrawRect(area,Color.black);GUI.DrawTexture(area,viewer.output,ScaleMode.ScaleToFit,false);
        if(!clean&&!string.IsNullOrEmpty(viewer.session.LastError))GUI.Label(new Rect(12,position.height-28,position.width-24,25),viewer.session.LastError);
    }
}
