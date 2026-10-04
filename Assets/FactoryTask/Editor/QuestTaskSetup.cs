using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FactoryTask;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Object=UnityEngine.Object;

public static class QuestTaskSetup
{
    public const string ScenePath="Assets/Factory/Scenes/SmartFactory_QuestTask.unity";
    const string BaseScene="Assets/Factory/Scenes/SmartFactory.unity";
    const string Root="Assets/FactoryTask";
    static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();
    static Material dark,steel,blue,green,amber;
    static Mesh rounded;

    [MenuItem("Tools/Smart Factory/Quest Task/Setup or Repair Quest Task")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before setup.");
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Input");Directory.CreateDirectory("Documentation/QuestTask");AssetDatabase.Refresh();
        ConfigureXR();PrepareArt();
        if(SceneManager.GetActiveScene().isDirty)EditorSceneManager.SaveOpenScenes();
        var scene=EditorSceneManager.OpenScene(File.Exists(ScenePath)?ScenePath:BaseScene);
        var previous=GameObject.Find("Quest_Task");if(previous!=null)Object.DestroyImmediate(previous);
        // Only the working copy changes: the original architectural scene remains untouched.
        foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)){cam.enabled=false;cam.tag="Untagged";}
        var production=GameObject.Find("Production_Line_A");
        if(production==null)throw new InvalidOperationException("Production_Line_A was not found.");
        foreach(var t in production.GetComponentsInChildren<Transform>(true))
            if(t.name=="Product_Carrier"||t.name=="Housing_In_Process"||t.name=="Product_Cap")t.gameObject.SetActive(false);
        var root=New("Quest_Task");var equipment=New("Task_Equipment",root);var systems=New("Systems",root);
        var queue=systems.gameObject.AddComponent<PickupQueue>();var spawner=New("Part_Pool",systems).gameObject.AddComponent<PartSpawner>();
        var controller=systems.gameObject.AddComponent<ConveyorController>();var tracking=systems.gameObject.AddComponent<XRTrackingProvider>();
        var calibration=systems.gameObject.AddComponent<UserCalibration>();var task=systems.gameObject.AddComponent<XRGrabTaskTracker>();var estimator=systems.gameObject.AddComponent<BodyLoadEstimator>();
        spawner.prefab=CreatePartPrefab();spawner.queue=queue;spawner.poolSize=12;spawner.intervalSeconds=3.5f;
        spawner.spawnPoint=Point("Part_Spawn",systems,new Vector3(-5.15f,1.015f,-3.05f));
        queue.slots=new Transform[4];
        for(int i=0;i<4;i++)
        {
            queue.slots[i]=Point("Pickup_Slot_"+i,systems,new Vector3(3.25f-i*.4f,1.045f,-3.52f));
            Box("Slot_Inlay_"+i,equipment,new Vector3(3.25f-i*.4f,1.043f,-3.52f),new Vector3(.30f,.006f,.32f),i==0?green:blue);
        }
        queue.approach=new[]{Point("Belt_End",systems,new Vector3(1.65f,1.015f,-3.05f)),Point("Queue_Entry",systems,new Vector3(1.85f,1.045f,-3.52f))};
        Box("Pickup_Table",equipment,new Vector3(2.7f,.99f,-3.49f),new Vector3(1.95f,.1f,.81f),steel,true);
        foreach(float x in new[]{1.85f,3.55f})foreach(float z in new[]{-3.78f,-3.2f})Box("Pickup_Leg",equipment,new Vector3(x,.47f,z),new Vector3(.075f,.94f,.075f),dark,true);
        WorldSign(equipment,"Pickup",new Vector3(2.7f,.88f,-3.91f),.38f,.12f);
        for(int i=0;i<3;i++)WorldSign(equipment,i==0?"One":i==1?"Three":"Five",new Vector3(2.3f+i*.4f,.72f,-3.91f),.3f,.11f);
        var done=New("Completion_Box",equipment);done.position=new Vector3(4.22f,0,-3.86f);
        Box("Box_Base",done,new Vector3(0,.77f,0),new Vector3(.88f,.08f,.78f),green,true);
        foreach(float x in new[]{-.43f,.43f})Box("Box_Side",done,new Vector3(x,.95f,0),new Vector3(.035f,.35f,.78f),green,true);
        foreach(float z in new[]{-.38f,.38f})Box("Box_End",done,new Vector3(0,.95f,z),new Vector3(.86f,.35f,.035f),green,true);
        foreach(float x in new[]{-.3f,.3f})foreach(float z in new[]{-.25f,.25f})Box("Box_Stand",done,new Vector3(x,.37f,z),new Vector3(.05f,.74f,.05f),steel,true);
        WorldSign(done,"Complete",new Vector3(0,.96f,-.405f),.4f,.13f);
        var trigger=New("Completion_Interior",done).gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,1.02f,0);trigger.size=new Vector3(.795f,.46f,.69f);
        task.completionVolume=trigger;task.recoveryPoint=Point("Recovery_Pad",systems,new Vector3(3.35f,1.25f,-3.62f));
        // Give static surfaces sensible collision without importing thousands of mesh colliders.
        foreach(var t in production.GetComponentsInChildren<Transform>(true))if(t.name=="Upper_Belt")AddBox(t.gameObject);
        foreach(var t in GameObject.Find("Workstations").GetComponentsInChildren<Transform>(true))if(t.name=="Thick_Metal_Worktop")AddBox(t.gameObject);
        calibration.standingPoint=Point("Standing_Position",systems,new Vector3(3.55f,0,-4.2f));
        var rig=New("XR_Rig",root);rig.position=calibration.standingPoint.position;
        var head=New("Tracked_Head",rig);head.localPosition=new Vector3(0,1.65f,0);
        var camera=head.gameObject.AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.05f;camera.farClipPlane=40;camera.allowHDR=false;camera.allowMSAA=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;head.gameObject.AddComponent<AudioListener>();
        tracking.origin=rig;tracking.head=head;tracking.leftHand=New("Left_Controller_Grip",rig);tracking.rightHand=New("Right_Controller_Grip",rig);
        tracking.leftHand.localPosition=new Vector3(-.2f,1,-.05f);tracking.rightHand.localPosition=new Vector3(.2f,1,-.05f);
        foreach(var hand in new[]{tracking.leftHand,tracking.rightHand})
        {
            var vis = hand.gameObject.AddComponent<VRHandVisualizer>();
            vis.side = (hand == tracking.leftHand) ? HandSide.Left : HandSide.Right;
            vis.tracking = tracking;
            vis.handMaterial = (hand == tracking.leftHand) ? blue : amber;
            vis.accentMaterial = steel;
            vis.BuildHand();
        }
        tracking.actions=CreateActions();calibration.tracking=tracking;calibration.task=task;
        task.tracking=tracking;task.calibration=calibration;task.queue=queue;task.pool=spawner;task.estimator=estimator;
        estimator.tracking=tracking;estimator.calibration=calibration;estimator.task=task;
        var report=systems.gameObject.AddComponent<ErgonomicReport>();report.estimator=estimator;report.task=task;report.tracking=tracking;
        controller.queue=queue;controller.spawner=spawner;controller.calibration=calibration;controller.tracking=tracking;controller.estimator=estimator;controller.task=task;
        controller.beltRenderer=production.GetComponentsInChildren<Renderer>().First(r=>r.name=="Upper_Belt");
        // Texture property animation cannot use a statically batched belt renderer.
        GameObjectUtility.SetStaticEditorFlags(controller.beltRenderer.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);
        var rula=systems.gameObject.AddComponent<RulaAssessment>();rula.tracking=tracking;rula.calibration=calibration;rula.task=task;
        BuildPanel(head,estimator,task,calibration,controller,rula);
        AvatarDemoSetup.Attach(root);
        rula.avatar=root.GetComponentInChildren<FullBodyAvatarIK>();
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.ImportAsset(ScenePath);AssetDatabase.SaveAssets();
        var buildScene=new EditorBuildSettingsScene(ScenePath,true){guid=new GUID(AssetDatabase.AssetPathToGUID(ScenePath))};EditorBuildSettings.scenes=new[]{buildScene};
        Selection.activeGameObject=root.gameObject;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(3,1,-3.5f),Quaternion.Euler(25,0,0),3);
        Validate();Debug.Log("QUEST_SETUP_OK");
    }
    static void PrepareArt()
    {
        Sprites.Clear();
        foreach(string path in Directory.GetFiles(Root+"/Art","*.png"))
        {
            string unityPath=path.Replace('\\','/');var imp=(TextureImporter)AssetImporter.GetAtPath(unityPath);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
            Sprites[Path.GetFileNameWithoutExtension(path)]=AssetDatabase.LoadAssetAtPath<Sprite>(unityPath);
        }
        string circlePath=Root+"/Art/Circle.png";
        if(!File.Exists(circlePath))
        {
            var t=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32));pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d));}
            t.SetPixels(pixels);t.Apply();File.WriteAllBytes(circlePath,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(circlePath);
            var imp=(TextureImporter)AssetImporter.GetAtPath(circlePath);imp.textureType=TextureImporterType.Sprite;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.SaveAndReimport();
        }
        Sprites["Circle"]=AssetDatabase.LoadAssetAtPath<Sprite>(circlePath);
        dark=Material("Task_Dark",new Color(.055f,.09f,.13f),.3f);steel=Material("Task_Steel",new Color(.65f,.72f,.77f),.55f);
        blue=Material("Part_1kg_Blue",new Color(.10f,.49f,.8f),.25f);amber=Material("Part_3kg_Amber",new Color(.95f,.58f,.11f),.2f);green=Material("Task_Complete_Green",new Color(.13f,.55f,.4f),.2f);
        rounded=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Factory/Meshes/B_0.380_0.220_0.350_0.025.asset");
    }
    static Material Material(string name,Color color,float metallic)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.32f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static Transform New(string name,Transform parent=null){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static Transform Point(string name,Transform parent,Vector3 pos){var t=New(name,parent);t.position=pos;return t;}
    static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material material,bool collider=false)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;
        if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static void AddBox(GameObject g){if(g.GetComponent<Collider>()!=null)return;var c=g.AddComponent<BoxCollider>();var b=g.GetComponent<MeshFilter>().sharedMesh.bounds;c.center=b.center;c.size=b.size;}
    static ConveyorPart CreatePartPrefab()
    {
        var go=New("Task_Part").gameObject;var part=go.AddComponent<ConveyorPart>();var body=go.GetComponent<Rigidbody>();if(body==null)body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        var collider=go.GetComponent<BoxCollider>();if(collider==null)collider=go.AddComponent<BoxCollider>();collider.size=Vector3.one;collider.isTrigger=true;
        var shell=Box("Machined_Housing",go.transform,Vector3.zero,Vector3.one,blue);if(rounded!=null){shell.GetComponent<MeshFilter>().sharedMesh=rounded;shell.transform.localScale=new Vector3(1/.38f,1/.22f,1/.35f);}
        part.bodyRenderer=shell.GetComponent<Renderer>();part.weightMaterials=new[]{blue,amber,Material("Part_5kg_Coral",new Color(.8f,.23f,.27f),.25f)};
        Box("Top_Insert",go.transform,new Vector3(0,.51f,0),new Vector3(.55f,.035f,.55f),dark);
        foreach(float x in new[]{-.34f,.34f})foreach(float z in new[]{-.34f,.34f})Box("Fastener",go.transform,new Vector3(x,.51f,z),new Vector3(.085f,.035f,.085f),steel);
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Task_Part.prefab");Object.DestroyImmediate(go);return prefab.GetComponent<ConveyorPart>();
    }
    static InputActionAsset CreateActions()
    {
        string path=Root+"/Input/FactoryQuest.inputactions";
        var existing=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        // ReportButton이 없으면 파일 재생성
        if(existing!=null&&existing.FindAction("ReportButton")!=null&&existing.FindAction("LeftThumbstick")!=null)return existing;
        if(File.Exists(path))AssetDatabase.DeleteAsset(path);
        var asset=ScriptableObject.CreateInstance<InputActionAsset>();var map=new InputActionMap("FactoryQuest");asset.AddActionMap(map);
        AddAction(map,"HeadPosition","<XRHMD>/centerEyePosition","Vector3");AddAction(map,"HeadRotation","<XRHMD>/centerEyeRotation","Quaternion");AddAction(map,"HeadTracked","<XRHMD>/isTracked","Button");
        foreach(string side in new[]{"Left","Right"})
        {
            string device="<XRController>{"+side+"Hand}";
            AddAction(map,side+"Position",device+"/devicePosition","Vector3");AddAction(map,side+"Rotation",device+"/deviceRotation","Quaternion");AddAction(map,side+"Tracked",device+"/isTracked","Button");AddAction(map,side+"Grip",device+"/gripPressed","Button");
        }
        AddAction(map,"Recalibrate","<XRController>{LeftHand}/secondaryButton","Button");
        AddAction(map,"ReportButton","<XRController>{RightHand}/secondaryButton","Button");
        AddAction(map,"LeftThumbstick","<XRController>{LeftHand}/primary2DAxis","Vector2");
        File.WriteAllText(path,asset.ToJson());Object.DestroyImmediate(asset);AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }
    static void AddAction(InputActionMap map,string name,string binding,string type){var a=map.AddAction(name,type=="Button"?InputActionType.Button:InputActionType.Value,binding);a.expectedControlType=type;}
    static Image Image(string name,Transform parent,Vector2 position,Vector2 size,Color color,string sprite=null)
    {
        var g=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(parent,false);var rect=(RectTransform)g.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
        var image=g.GetComponent<Image>();image.color=color;image.raycastTarget=false;if(sprite!=null)image.sprite=Sprites[sprite];return image;
    }
    static NumberReadout Number(string name,Transform parent,Vector2 pos,float height=27)
    {
        var result=new NumberReadout{digits=new Image[3]};for(int i=0;i<3;i++){result.digits[i]=Image(name+"_"+i,parent,pos+new Vector2((i-1)*height*.55f,0),new Vector2(height*.8f,height),Color.white,"Digit0");result.digits[i].enabled=i==2;}return result;
    }
    static void BuildPanel(Transform head,BodyLoadEstimator estimator,XRGrabTaskTracker task,UserCalibration calibration,ConveyorController controller,RulaAssessment rula)
    {
        // 510×420 패널 — 왼쪽 신체 도식 + 오른쪽 진행 바 차트
        var go=new GameObject("Relative_Load_Panel",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(head,false);go.transform.localPosition=new Vector3(-.52f,.20f,1.08f);go.transform.localRotation=Quaternion.Euler(0,-21,0);go.transform.localScale=Vector3.one*.001f;
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(510,420);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        Image("Panel_Background",rect,Vector2.zero,new Vector2(510,420),new Color(.018f,.032f,.055f,.97f));

        // ── 상단 타이틀 + 평균 바 ────────────────────────────────────
        Image("Title",rect,new Vector2(-60,157),new Vector2(262,55),Color.white,"Title");
        Image("AvgBar_Bg",rect,new Vector2(162,158),new Vector2(140,18),new Color(.07f,.11f,.18f));
        var avgBar=Image("AvgBar_Fill",rect,new Vector2(162,158),new Vector2(140,18),BodyLoadVisualizer.ColorFor(0));
        avgBar.type=UnityEngine.UI.Image.Type.Filled;avgBar.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;avgBar.fillAmount=0;
        Image("Divider",rect,new Vector2(0,132),new Vector2(490,1),new Color(.3f,.45f,.53f));

        // ── 신체 도식 (왼쪽) ─────────────────────────────────────────
        Image("Left_Label",rect,new Vector2(-130,109),new Vector2(60,24),new Color(.65f,.76f,.82f),"Left");
        Image("Right_Label",rect,new Vector2(0,109),new Vector2(60,24),new Color(.65f,.76f,.82f),"Right");
        Image("Head_Schematic",rect,new Vector2(-65,89),new Vector2(37,37),new Color(.62f,.73f,.8f),"Circle");
        Image("Neck",rect,new Vector2(-65,62),new Vector2(15,21),new Color(.4f,.52f,.62f));
        Image("Shoulder_Link",rect,new Vector2(-65,45),new Vector2(130,8),new Color(.35f,.46f,.55f));
        Image("Pelvis",rect,new Vector2(-65,-66),new Vector2(68,20),new Color(.35f,.46f,.55f));
        foreach(float x in new[]{-85f,-45f})Image("Leg_Schematic",rect,new Vector2(x,-102),new Vector2(15,58),new Color(.24f,.34f,.43f));

        var visual=go.AddComponent<BodyLoadVisualizer>();visual.estimator=estimator;visual.task=task;visual.calibration=calibration;visual.rula=rula;
        visual.regions=new Image[7];visual.regionNumbers=new NumberReadout[7];visual.regionBars=new Image[7];visual.averageBar=avgBar;

        // 신체 부위 색상 도식 (X축 -65 기준으로 이동)
        Vector2[] positions={new Vector2(-130,45),new Vector2(0,45),new Vector2(-130,0),new Vector2(0,0),new Vector2(-130,-54),new Vector2(0,-54),new Vector2(-65,-9)};
        for(int i=0;i<7;i++)
        {
            Vector2 size=i==6?new Vector2(60,90):i==2||i==3?new Vector2(16,64):new Vector2(26,26);
            visual.regions[i]=Image("Body_Region_"+i,rect,positions[i],size,BodyLoadVisualizer.ColorFor(0),i==0||i==1||i==4||i==5?"Circle":null);
        }

        // ── 세로 구분선 ──────────────────────────────────────────────
        Image("Chart_Sep",rect,new Vector2(58,5),new Vector2(2,240),new Color(.28f,.42f,.52f,.8f));

        // ── 바 차트 (오른쪽) — 상단:왼팔 / 하단:오른팔 ──────────────
        // 인덱스: 0=왼어깨,1=오른어깨,2=왼팔,3=오른팔,4=왼손목,5=오른손목,6=몸통
        // barY[i] → 각 부위의 화면 Y 좌표 (왼쪽 3개 위 / 오른쪽 3개 아래)
        Image("LGroup_Label",rect,new Vector2(152,118),new Vector2(50,15),new Color(.65f,.76f,.82f),"Left");
        float[] barY={102f,12f,79f,-11f,56f,-34f,-90f};
        for(int i=0;i<7;i++)
        {
            float y=barY[i];
            Image("Bar_Bg_"+i,rect,new Vector2(152,y),new Vector2(150,16),new Color(.07f,.11f,.18f));
            var fill=Image("Bar_Fill_"+i,rect,new Vector2(152,y),new Vector2(150,16),BodyLoadVisualizer.ColorFor(0));
            fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillAmount=0;
            visual.regionBars[i]=fill;
            visual.regionNumbers[i]=Number("Score_"+i,rect,new Vector2(228,y),18);
        }
        // 그룹 구분선 + 오른팔 레이블
        Image("Group_Sep",rect,new Vector2(152,40),new Vector2(155,1),new Color(.45f,.58f,.65f,.7f));
        Image("RGroup_Label",rect,new Vector2(152,28),new Vector2(50,15),new Color(.65f,.76f,.82f),"Right");
        // 67% 위험 기준선 (전체 바 차트 높이에 맞춤)
        Image("Danger_Line",rect,new Vector2(178,7),new Vector2(2,212),new Color(.97f,.23f,.22f,.5f));

        // ── 하단 푸터 ────────────────────────────────────────────────
        Image("Footer_Line",rect,new Vector2(0,-118),new Vector2(490,1),new Color(.3f,.45f,.53f));
        Image("Weight_Label",rect,new Vector2(-175,-143),new Vector2(56,24),new Color(.6f,.73f,.8f),"Weight");
        visual.weight=Number("Weight",rect,new Vector2(-127,-143),23);
        Image("Kg",rect,new Vector2(-88,-143),new Vector2(36,24),Color.white,"Kg");
        Image("Average_Label",rect,new Vector2(-5,-143),new Vector2(56,24),new Color(.6f,.73f,.8f),"Average");
        visual.average=Number("Average",rect,new Vector2(54,-143),23);
        visual.rulaIndicator=Image("RULA_Indicator",rect,new Vector2(105,-143),new Vector2(31,31),new Color(.18f,.85f,.48f),"Circle");
        visual.rulaScore=Number("RULA_Score",rect,new Vector2(105,-143),20);
        // 벨트 속도 바 (푸터 오른쪽 — 초록=정상, 빨강=감속)
        Image("BeltBar_Bg",rect,new Vector2(155,-143),new Vector2(110,14),new Color(.07f,.11f,.18f));
        var beltBar=Image("BeltBar_Fill",rect,new Vector2(155,-143),new Vector2(110,14),BodyLoadVisualizer.ColorFor(0));
        beltBar.type=UnityEngine.UI.Image.Type.Filled;beltBar.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;beltBar.fillAmount=1f;
        visual.beltSpeedBar=beltBar;
        visual.beltSpeedPct=Number("BeltSpeed",rect,new Vector2(230,-143),16);
        visual.conveyor=controller;

        visual.glyphs=new Sprite[10];for(int i=0;i<10;i++)visual.glyphs[i]=Sprites["Digit"+i];
        visual.calibrationProgress=Image("Calibration_Progress",rect,new Vector2(237,158),new Vector2(13,13),Color.cyan,"Circle");
        visual.calibrationProgress.type=UnityEngine.UI.Image.Type.Filled;visual.calibrationProgress.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;visual.calibrationProgress.fillAmount=0;
        string matPath=Root+"/Materials/LoadPanel_Overlay.mat";var overlay=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(overlay==null){overlay=new Material(Shader.Find("FactoryTask/OverlayUI"));AssetDatabase.CreateAsset(overlay,matPath);}overlay.shader=Shader.Find("FactoryTask/OverlayUI");EditorUtility.SetDirty(overlay);foreach(var graphic in go.GetComponentsInChildren<Graphic>())graphic.material=overlay;
    }
    static void WorldSign(Transform parent,string word,Vector3 pos,float width,float height)
    {
        var go=new GameObject(word+"_Sign",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=Vector3.one*.001f;go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(width*1000,height*1000);Image("Backing",rect,Vector2.zero,rect.sizeDelta,new Color(.035f,.065f,.085f));Image(word,rect,Vector2.zero,rect.sizeDelta,Color.white,word);
    }
    static void ConfigureXR()
    {
        var settings=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Root+"/XRGeneralSettings.asset");
        if(settings==null){settings=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(settings,Root+"/XRGeneralSettings.asset");}
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,settings,true);
        foreach(var target in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})
        {
            if(!settings.HasSettingsForBuildTarget(target))settings.CreateDefaultSettingsForBuildTarget(target);
            if(!settings.HasManagerSettingsForBuildTarget(target))settings.CreateDefaultManagerSettingsForBuildTarget(target);
            var general=settings.SettingsForBuildTarget(target);general.InitManagerOnStart=true;
            if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",target))throw new Exception("OpenXR loader could not be assigned.");
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(target);
            var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(target);if(xr==null)throw new Exception("OpenXR settings are unavailable.");
            xr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;xr.depthSubmissionMode=OpenXRSettings.DepthSubmissionMode.None;
            xr.latencyOptimization=OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            foreach(var feature in xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())feature.enabled=feature is OculusTouchControllerProfile||(target==BuildTargetGroup.Android&&feature is MetaQuestFeature);
            EditorUtility.SetDirty(general);EditorUtility.SetDirty(xr);
        }
        var meta=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<MetaQuestFeature>();
        if(meta==null)throw new Exception("Meta Quest Support feature is missing.");
        meta.enabled=true;var soMeta=new SerializedObject(meta);var devices=soMeta.FindProperty("targetDevices");
        if(devices!=null)for(int i=0;i<devices.arraySize;i++)devices.GetArrayElementAtIndex(i).FindPropertyRelative("enabled").boolValue=true;soMeta.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
        PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.nova.smartfactory.questtask");PlayerSettings.productName="NOVA Factory Task";
        var ps=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=ps.FindProperty("activeInputHandler");if(input!=null){input.intValue=1;ps.ApplyModifiedPropertiesWithoutUndo();}
        PlayerSettings.SetPreloadedAssets(Array.Empty<Object>());
        var mobile=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        mobile.msaaSampleCount=4;mobile.renderScale=1;mobile.supportsHDR=false;mobile.shadowDistance=16;mobile.shadowCascadeCount=1;
        QualitySettings.SetQualityLevel(0,true);QualitySettings.renderPipeline=mobile;Time.fixedDeltaTime=1f/72;
        EditorUtility.SetDirty(mobile);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Smart Factory/Quest Task/Validate Task")]
    public static void Validate()
    {
        var errors=new List<string>();var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new Exception("Open SmartFactory_QuestTask first.");
        var tracker=Object.FindFirstObjectByType<XRGrabTaskTracker>();
        if(tracker==null)errors.Add("Missing task tracker");else
        {
            if(tracker.queue.slots.Length!=4)errors.Add("Queue must have four slots");
            if(tracker.pool.prefab==null||tracker.completionVolume==null||tracker.tracking.actions==null)errors.Add("Missing task reference");
            if(tracker.pool.poolSize>16)errors.Add("Unbounded pool");
        }
        if(scene.GetRootGameObjects().Count(g=>g.name=="Quest_Task")!=1)errors.Add("Duplicate task root");
        if(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled&&c.targetTexture==null)!=1)errors.Add("Expected only one active first-person output camera");
        foreach(var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)!=0)errors.Add("Missing script: "+go.name);
            foreach(var renderer in go.GetComponents<Renderer>())foreach(var material in renderer.sharedMaterials)if(material==null||material.shader==null||!material.shader.isSupported)errors.Add("Broken material: "+go.name);
        }
        foreach(var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))if(graphic.material==null||graphic.material.shader==null||graphic.material.shader.name=="Hidden/InternalErrorShader"||!graphic.material.shader.isSupported)errors.Add("Broken UI material: "+graphic.name);
        string report="Scene: "+ScenePath+"\nTask roots: "+scene.GetRootGameObjects().Count(g=>g.name=="Quest_Task")+"\nQueue: 4 FIFO reservations (includes in-transit parts)\nPool: 12\nWeight variants: 1, 3, 5 kg\nRuntime camera: tracked first person only\nLightmaps preserved: "+LightmapSettings.lightmaps.Length+"\nErrors: "+errors.Count+"\n"+string.Join("\n",errors);
        Directory.CreateDirectory("Documentation/QuestTask");File.WriteAllText("Documentation/QuestTask/SceneValidation.txt",report);Debug.Log(report);if(errors.Count>0)throw new Exception("Quest task validation failed.");
    }
    public static void SetupBatch(){Setup();Setup();Validate();QuestTaskVerification.RunMathChecks();Capture();}
    [MenuItem("Tools/Smart Factory/Quest Task/Build Quest APK")]
    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Builds/QuestTask");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestTask/NOVA_FactoryTask.apk",target=BuildTarget.Android,options=BuildOptions.None});
        string apk="Builds/QuestTask/NOVA_FactoryTask.apk";File.WriteAllText("Documentation/QuestTask/AndroidBuild.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nAPK bytes: "+(File.Exists(apk)?new FileInfo(apk).Length:0));
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Quest APK build failed.");
    }
    [MenuItem("Tools/Smart Factory/Quest Task/Verify Play Mode")]
    public static void PlayTests()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before starting verification.");
        if(SceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
        var general=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        SessionState.SetBool("QuestTask.XrInitBeforeTest",general.InitManagerOnStart);SessionState.SetBool("QuestTask.RestoreXr",true);general.InitManagerOnStart=false;EditorUtility.SetDirty(general);AssetDatabase.SaveAssets();
        SessionState.SetBool("QuestTask.RunTests",true);EditorApplication.isPlaying=true;
    }
    public static void PlayTestsBatch(){SessionState.SetBool("QuestTask.BatchTests",true);PlayTests();}
    public static void Capture()
    {
        ShaderUtil.allowAsyncCompilation=false;var camera=Camera.main;if(camera==null)return;var rotation=camera.transform.localRotation;camera.transform.localRotation=Quaternion.Euler(24,-5,0);
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Documentation/QuestTask/TaskView.png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;camera.transform.localRotation=rotation;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
    public static void Open(){EditorSceneManager.OpenScene(ScenePath);Selection.activeObject=GameObject.Find("Quest_Task");}
}

[InitializeOnLoad]
public static class QuestTaskEditorBridge
{
    static double nextPoll;
    static QuestTaskEditorBridge(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=Changed;}
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("QuestTask.RunTests",false))
        {SessionState.SetBool("QuestTask.RunTests",false);new GameObject("Editor_Only_Verification").AddComponent<QuestTaskVerification>();}
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("QuestTask.RestoreXr",false))
        {
            SessionState.SetBool("QuestTask.RestoreXr",false);var general=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);general.InitManagerOnStart=SessionState.GetBool("QuestTask.XrInitBeforeTest",true);EditorUtility.SetDirty(general);AssetDatabase.SaveAssets();
            int exitCode=SessionState.GetInt("QuestTask.TestExitCode",-1);if(exitCode>=0){SessionState.SetInt("QuestTask.TestExitCode",-1);EditorApplication.Exit(exitCode);}
        }
    }
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;nextPoll=EditorApplication.timeSinceStartup+1;
        const string path="Temp/FactoryTask.command";if(!File.Exists(path))return;string command=File.ReadAllText(path).Trim();File.Delete(path);
        try{switch(command){case "setup":QuestTaskSetup.Setup();break;case "open":QuestTaskSetup.Open();break;case "validate":QuestTaskSetup.Validate();break;case "playtests":QuestTaskSetup.PlayTests();break;case "capture":QuestTaskSetup.Capture();break;case "build":QuestTaskSetup.BuildAndroid();break;default:throw new Exception("Unknown local editor command");}File.WriteAllText("Temp/FactoryTask.result",command+" OK");}
        catch(Exception error){File.WriteAllText("Temp/FactoryTask.result",error.ToString());Debug.LogException(error);}
    }
}
