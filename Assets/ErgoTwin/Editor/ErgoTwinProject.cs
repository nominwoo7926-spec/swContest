using System;
using System.IO;
using System.Linq;
using ErgoTwin;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.OculusQuestSupport;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

[InitializeOnLoad]
public static class ErgoTwinProject
{
    public const string ScenePath="Assets/Scenes/ErgoTwinLab.unity";
    const string CommandPath="Temp/ErgoTwin.command";
    static double nextPoll;
    static ErgoTwinProject(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        nextPoll=EditorApplication.timeSinceStartup+1;
        if(!File.Exists(CommandPath))return;
        string command=File.ReadAllText(CommandPath).Trim();File.Delete(CommandPath);
        try {
            switch(command) {
                case "create":Create();break;
                case "play":EditorApplication.isPlaying=true;break;
                case "stop":EditorApplication.isPlaying=false;break;
                case "capture":Capture();break;
                case "test":Test();break;
                case "android":BuildQuest();break;
                case "windows":BuildWindows();break;
                case "inspect":Inspect();break;
                case "runtime-test":var lab=UnityEngine.Object.FindFirstObjectByType<ErgoTwinLab>();lab.StartCoroutine(lab.VerifyRuntime());break;
            }
            File.WriteAllText("Temp/ErgoTwin.result",command+" OK "+DateTime.Now);
        }catch(Exception e){File.WriteAllText("Temp/ErgoTwin.result",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/Ergo Twin/Create new laboratory")]
    public static void Create()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before creating the laboratory.");
        Configure();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var lab=new GameObject("ERGO TWIN / Physical AI Laboratory").AddComponent<ErgoTwinLab>();
        lab.workerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ErgoTwin/Art/Characters/characterMedium.fbx");
        string path="Assets/ErgoTwin/Art/Characters/Worker.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.color=Color.white;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ErgoTwin/Art/Characters/skaterMaleA.png");
        material.SetFloat("_Smoothness",.25f);lab.workerMaterial=material;
        lab.BuildWorld();
        // Persist generated materials so the scene is fully inspectable outside Play mode.
        Directory.CreateDirectory("Assets/ErgoTwin/Art/Materials");
        foreach(var renderer in lab.GetComponentsInChildren<Renderer>())foreach(var mat in renderer.sharedMaterials) {
            if(mat==null||AssetDatabase.Contains(mat))continue;
            string safe=string.Concat(mat.name.Select(c=>char.IsLetterOrDigit(c)?c:'_'));
            string matPath="Assets/ErgoTwin/Art/Materials/"+safe+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(existing==null)AssetDatabase.CreateAsset(mat,matPath);
        }
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        foreach(string old in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/ErgoContestVrDemo.unity","Assets/Scenes/ErgoContestVrDemo 1.unity","Assets/Scenes/ErgoCellFactory.unity"})
            if(File.Exists(old))AssetDatabase.DeleteAsset(old);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject=lab.gameObject;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,.7f),Quaternion.Euler(20,-30,0),10);
        Directory.CreateDirectory("Documentation");
        File.WriteAllText("Documentation/scene-created.txt","Created "+ScenePath+"\nWorker imported: "+(lab.workerPrefab!=null)+"\n"+DateTime.Now);
        Debug.Log("ERGOTWIN: new laboratory ready.");
    }
    [MenuItem("Tools/Ergo Twin/Configure Quest")]
    public static void Configure()
    {
        PlayerSettings.companyName="ErgoTwin Research";PlayerSettings.productName="Ergo Twin - Adaptive Assembly";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.ergotwin.adaptiveassembly");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.GameActivity;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.runInBackground=true;
        var mobile=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if(mobile!=null){mobile.msaaSampleCount=4;mobile.renderScale=1;mobile.shadowDistance=25;EditorUtility.SetDirty(mobile);}
        var container=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        if(container==null)throw new InvalidOperationException("XR settings container is missing.");
        foreach(var group in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone}) {
            if(!container.HasSettingsForBuildTarget(group))container.CreateDefaultSettingsForBuildTarget(group);
            if(!container.HasManagerSettingsForBuildTarget(group))container.CreateDefaultManagerSettingsForBuildTarget(group);
            var settings=container.SettingsForBuildTarget(group);
            settings.InitManagerOnStart=group==BuildTargetGroup.Android;
            if(!XRPackageMetadataStore.AssignLoader(settings.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",group))throw new InvalidOperationException("OpenXR loader assignment failed.");
            EditorUtility.SetDirty(settings);EditorUtility.SetDirty(settings.Manager);
            var oxr=OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if(oxr!=null){
                var touch=oxr.GetFeature<OculusTouchControllerProfile>();if(touch!=null)touch.enabled=true;
                var legacy=oxr.GetFeature<OculusQuestFeature>();if(legacy!=null)legacy.enabled=false;
                var quest=oxr.GetFeature<MetaQuestFeature>();if(quest!=null&&group==BuildTargetGroup.Android)quest.enabled=true;
                oxr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;EditorUtility.SetDirty(oxr);
            }
        }
        EditorUtility.SetDirty(container);AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Ergo Twin/Build Quest APK")]
    public static void BuildQuest()
    {
        Configure();
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android) {
            if(!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))throw new InvalidOperationException("Could not activate Android build target.");
            File.WriteAllText(CommandPath,"android");nextPoll=EditorApplication.timeSinceStartup+10;return;
        }
        Build(BuildTarget.Android,"Builds/Quest/ErgoTwin.apk");
    }
    [MenuItem("Tools/Ergo Twin/Build Windows demo")]
    public static void BuildWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/Windows/ErgoTwin.exe");}
    static void Build(BuildTarget target,string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=target,options=BuildOptions.Development});
        File.WriteAllText("Documentation/build-"+target+".txt",result.summary.result+"\nErrors: "+result.summary.totalErrors+"\nBuild output bytes (including debug symbols): "+result.summary.totalSize+"\nDuration: "+result.summary.totalTime+"\n"+
            string.Join("\n",result.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content)));
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed: "+target);
    }
    static void Capture()
    {
        var lab=UnityEngine.Object.FindFirstObjectByType<ErgoTwinLab>();
        var cam=lab.View!=null?lab.View:lab.GetComponentInChildren<Camera>();
        var rt=new RenderTexture(1600,1000,24);cam.targetTexture=rt;cam.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
        Directory.CreateDirectory("Documentation");File.WriteAllBytes("Documentation/laboratory.png",tex.EncodeToPNG());
        cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Inspect()
    {
        var lab=UnityEngine.Object.FindFirstObjectByType<ErgoTwinLab>();
        File.WriteAllText("Documentation/runtime-status.txt",$"Playing: {EditorApplication.isPlaying}\nState: {lab.State}\nScore: {lab.Score}\nCycles: {lab.Cycles}\nTable: {lab.TableHeight}\n"+
            string.Join("\n",lab.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.bones).Select(t=>t.name)));
    }
    [MenuItem("Tools/Ergo Twin/Run policy verification")]
    public static void Test()
    {
        int checks=0;
        void Check(bool condition,string name){if(!condition)throw new Exception("FAILED: "+name);checks++;}
        var safe=ErgoPolicy.Estimate(new Vector3(0,1.65f,0),new Vector3(.18f,1.05f,.4f),new Vector3(0,.9f,0),3,true);
        var high=ErgoPolicy.Estimate(new Vector3(0,1.65f,0),new Vector3(.18f,1.7f,.9f),new Vector3(0,.9f,0),3,true);
        Check(high.score>safe.score+20,"high distant reach has greater load");
        Check(safe.score>=0&&high.score<=100,"score bounds");
        var best=ErgoPolicy.Optimize(1.42f);
        Check(best.x>=.85f&&best.x<=1.35f&&best.y>=.38f&&best.y<=.75f,"actuator bounds");
        Check(ErgoPolicy.Cost(best.x,best.y,1.42f)<ErgoPolicy.Cost(1.62f,.87f,1.42f),"optimizer reduces modeled cost");
        var sweep=new Bounds(Vector3.zero,Vector3.one);
        Check(!ErgoPolicy.Clear(Vector3.zero,Vector3.one,sweep,false,true),"left hand blocks travel");
        Check(!ErgoPolicy.Clear(Vector3.one,Vector3.zero,sweep,false,true),"right hand blocks travel");
        Check(!ErgoPolicy.Clear(Vector3.one,Vector3.one,sweep,true,true),"held part blocks travel");
        Check(!ErgoPolicy.Clear(Vector3.one,Vector3.one,sweep,false,false),"tracking loss blocks travel");
        Check(ErgoPolicy.Clear(Vector3.one,Vector3.one,sweep,false,true),"clear zone permits travel");
        Check(!ErgoPolicy.Clear(new Vector3(-.65f,1.76f,.62f),Vector3.back,ErgoPolicy.TravelVolume,false,true),"raised tray hand blocks travel");
        Check(!ErgoPolicy.Clear(Vector3.back,new Vector3(1.25f,1.3f,.7f),ErgoPolicy.TravelVolume,false,true),"outer worktop edge blocks travel");
        Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/policy-verification.txt",checks+" checks passed\n"+DateTime.Now);
        Debug.Log("ERGOTWIN: "+checks+" policy checks passed");
    }
}
