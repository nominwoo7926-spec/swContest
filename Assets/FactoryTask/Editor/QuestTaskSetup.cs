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
        // Keep the authored conveyor geometry but put its working surface at 0.82 m.
        // Compute the correction from the measured renderer so repeated Setup is idempotent.
        var sourceBelt=production.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Upper_Belt");
        production.transform.position+=Vector3.up*(.82f-sourceBelt.bounds.center.y);
        // Keep the working line and architecture, but remove unrelated showcase machinery.
        foreach(string name in new[]{"Production_Line_B","Workstations","Factory_Props"})
        {
            var unused=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);if(unused!=null)unused.SetActive(false);
        }
        foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
            if(t.name=="Rear_Feature_Panel"||t.name=="Loading_Bay"||t.name=="Main_Entrance")t.gameObject.SetActive(false);
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(IsDecorativeFactoryLabel(t.name))t.gameObject.SetActive(false);
        // The factory's line captions were hidden above, but their opaque backplates
        // and hanging hardware otherwise remain as blank black screens over the belt.
        foreach(string lineName in new[]{"Production_Line_A","Production_Line_B"})
        {
            var lineRoot=GameObject.Find(lineName);
            if(lineRoot==null)continue;
            foreach(var t in lineRoot.GetComponentsInChildren<Transform>(true))
                if(IsUnusedLineSignHardware(t.name))t.gameObject.SetActive(false);
        }
        foreach(var t in production.GetComponentsInChildren<Transform>(true))
            if(t.name=="Product_Carrier"||t.name=="Housing_In_Process"||t.name=="Product_Cap"||
               t.name=="01_Material_Infeed"||t.name=="02_Inspection_Gantry_Static")t.gameObject.SetActive(false);
        var root=New("Quest_Task");var equipment=New("Task_Equipment",root);var systems=New("Systems",root);
        var queue=systems.gameObject.AddComponent<PickupQueue>();var spawner=New("Part_Pool",systems).gameObject.AddComponent<PartSpawner>();
        var controller=systems.gameObject.AddComponent<ConveyorController>();var tracking=systems.gameObject.AddComponent<XRTrackingProvider>();
        var calibration=systems.gameObject.AddComponent<UserCalibration>();var task=systems.gameObject.AddComponent<XRGrabTaskTracker>();var estimator=systems.gameObject.AddComponent<BodyLoadEstimator>();
        spawner.prefab=CreatePartPrefab();spawner.queue=queue;spawner.poolSize=16;spawner.intervalSeconds=3.5f;
        spawner.spawnPoint=Point("Part_Spawn",systems,new Vector3(-5.15f,.845f,-3.05f));
        var movingTop=New("Adjustable_Pickup_Top",equipment);
        movingTop.position=new Vector3(4.25f,.82f,-3.05f);
        Box("Pickup_Table",movingTop,Vector3.zero,new Vector3(1.70f,.1f,.90f),steel,true);
        var table=systems.gameObject.AddComponent<AdjustableWorktable>();table.movingTop=movingTop;table.tracking=tracking;table.task=task;
        queue.slots=new Transform[12];queue.tableSlotCount=3;
        for(int i=0;i<12;i++)
        {
            // Three parts transfer to the height-adjustable table; the backlog stays on the belt.
            bool onTable=i<queue.tableSlotCount;
            float x=onTable?4.65f-i*.4f:3.05f-(i-queue.tableSlotCount)*.4f;
            queue.slots[i]=New("Pickup_Slot_"+i,onTable?movingTop:systems);
            if(onTable)queue.slots[i].localPosition=new Vector3(x-4.25f,.055f,0);
            else queue.slots[i].position=new Vector3(x,.843f,-3.05f);
            if(i==0)Box("Pickup_Indicator",movingTop,new Vector3(x-4.25f,.053f,0),new Vector3(.23f,.004f,.23f),green);
        }
        var queueEntry=New("Queue_Entry",movingTop);
        queueEntry.localPosition=new Vector3(3.65f-4.25f,.055f,0);
        queue.approach=new[]{Point("Belt_End",systems,new Vector3(3.17f,.845f,-3.05f)),queueEntry};
        var legs=new List<Transform>();
        foreach(float x in new[]{3.55f,4.95f})foreach(float z in new[]{-3.38f,-2.72f})legs.Add(Box("Pickup_Leg",equipment,new Vector3(x,.385f,z),new Vector3(.075f,.77f,.075f),dark,true).transform);
        table.legs=legs.ToArray();
        var done=New("Completion_Box",equipment);done.position=new Vector3(5.75f,0,-3.05f);
        Box("Box_Base",done,new Vector3(0,.77f,0),new Vector3(.88f,.08f,.78f),green,true);
        foreach(float x in new[]{-.43f,.43f})Box("Box_Side",done,new Vector3(x,.95f,0),new Vector3(.035f,.35f,.78f),green,true);
        foreach(float z in new[]{-.38f,.38f})Box("Box_End",done,new Vector3(0,.95f,z),new Vector3(.86f,.35f,.035f),green,true);
        foreach(float x in new[]{-.3f,.3f})foreach(float z in new[]{-.25f,.25f})Box("Box_Stand",done,new Vector3(x,.37f,z),new Vector3(.05f,.74f,.05f),steel,true);
        var trigger=New("Completion_Interior",done).gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,1.02f,0);trigger.size=new Vector3(.795f,.46f,.69f);
        task.completionVolume=trigger;task.recoveryPoint=Point("Recovery_Pad",systems,new Vector3(4.9f,1.08f,-3.05f));
        // Give static surfaces sensible collision without importing thousands of mesh colliders.
        foreach(var t in production.GetComponentsInChildren<Transform>(true))if(t.name=="Upper_Belt")AddBox(t.gameObject);
        calibration.standingPoint=Point("Standing_Position",systems,new Vector3(5.05f,0,-3.70f));
        var rig=New("XR_Rig",root);rig.position=calibration.standingPoint.position;
        var head=New("Tracked_Head",rig);head.localPosition=new Vector3(0,1.65f,0);
        var camera=head.gameObject.AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.05f;camera.farClipPlane=40;camera.allowHDR=false;camera.allowMSAA=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;head.gameObject.AddComponent<AudioListener>();
        tracking.origin=rig;tracking.head=head;tracking.leftHand=New("Left_Controller_Grip",rig);tracking.rightHand=New("Right_Controller_Grip",rig);
        tracking.leftHand.localPosition=new Vector3(-.2f,1,-.05f);tracking.rightHand.localPosition=new Vector3(.2f,1,-.05f);
        foreach(var hand in new[]{tracking.leftHand,tracking.rightHand})
        {
            Box("Grip_Marker",hand,Vector3.zero,new Vector3(.036f,.075f,.042f),hand==tracking.leftHand?blue:amber);
            Box("Thumb_Marker",hand,new Vector3(0,.04f,.016f),new Vector3(.044f,.018f,.05f),steel);
        }
        tracking.actions=CreateActions();calibration.tracking=tracking;calibration.task=task;
        task.tracking=tracking;task.calibration=calibration;task.queue=queue;task.pool=spawner;task.estimator=estimator;
        estimator.tracking=tracking;estimator.calibration=calibration;estimator.task=task;
        controller.queue=queue;controller.spawner=spawner;controller.calibration=calibration;controller.tracking=tracking;
        controller.beltRenderer=production.GetComponentsInChildren<Renderer>().First(r=>r.name=="Upper_Belt");
        // The belt-speed controls are behind the worker and excluded from spectator video.
        var console=New("Completion_Control_Console",equipment);
        console.position=new Vector3(5.05f,.35f,-4.49f);
        console.rotation=Quaternion.Euler(0,180,0);
        var consoleStand=Box("Console_Stand",equipment,new Vector3(5.05f,.46f,-4.49f),new Vector3(.07f,.92f,.07f),steel,true);
        Box("Console_Backplate",console,new Vector3(0,.59f,0),new Vector3(1.00f,.36f,.035f),dark,true);
        Box("Console_Bracket",console,new Vector3(0,.59f,.075f),new Vector3(.36f,.16f,.17f),steel,true);
        PanelText(console,"BELT",new Vector3(.285f,.755f,-.029f),.18f);
        PanelText(console,"5 KG",new Vector3(-.17f,.61f,-.065f),.24f);
        var speedControls=systems.gameObject.AddComponent<ConveyorSpeedControls>();speedControls.conveyor=controller;speedControls.tracking=tracking;
        speedControls.slowerButton=Box("Speed_Slower",console,new Vector3(.20f,.61f,-.037f),new Vector3(.13f,.12f,.045f),blue).transform;
        speedControls.fasterButton=Box("Speed_Faster",console,new Vector3(.37f,.61f,-.037f),new Vector3(.13f,.12f,.045f),green).transform;
        PanelText(console,"-",new Vector3(.20f,.61f,-.065f),.26f);
        PanelText(console,"+",new Vector3(.37f,.61f,-.065f),.26f);
        var speedLabel=new GameObject("Belt_Speed_Readout");speedLabel.transform.SetParent(console,false);
        speedLabel.transform.localPosition=new Vector3(.285f,.455f,-.028f);speedLabel.transform.localScale=Vector3.one*.13f;
        speedControls.speedLabel=speedLabel.AddComponent<TextMesh>();speedControls.speedLabel.text="BELT 0.38 m/s";
        speedControls.speedLabel.anchor=TextAnchor.MiddleCenter;speedControls.speedLabel.alignment=TextAlignment.Center;
        speedControls.speedLabel.fontSize=48;speedControls.speedLabel.characterSize=.08f;speedControls.speedLabel.color=Color.white;
        speedControls.speedLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");speedLabel.GetComponent<MeshRenderer>().sharedMaterial=speedControls.speedLabel.font.material;
        var profile=systems.gameObject.AddComponent<WorkerProfile>();profile.calibration=calibration;
        var agent=systems.gameObject.AddComponent<AdaptiveErgonomicsAgent>();agent.estimator=estimator;agent.conveyor=controller;agent.queue=queue;agent.tracking=tracking;agent.calibration=calibration;agent.task=task;agent.table=table;agent.profile=profile;agent.approvalRequired=true;
        agent.normalMaterial=green;agent.watchMaterial=amber;agent.interventionMaterial=Material("Agent_Intervention",new Color(.9f,.08f,.04f),.15f);agent.disabledMaterial=steel;
        // A small AI-state light on the existing console replaces the sightline-blocking monitor.
        Box("AI_Status_Mount",console,new Vector3(-.29f,.83f,0),new Vector3(.38f,.10f,.035f),dark);
        agent.indicator=Box("Agent_Status_Light",console,new Vector3(-.40f,.83f,-.029f),new Vector3(.10f,.035f,.012f),green).GetComponent<Renderer>();
        PanelText(console,"AI",new Vector3(-.28f,.83f,-.042f),.14f);
        agent.riskModel=systems.gameObject.AddComponent<ErgonomicsRiskModel>();
        var collector=systems.gameObject.AddComponent<ManufacturingDataCollector>();collector.tracking=tracking;collector.calibration=calibration;collector.estimator=estimator;collector.task=task;collector.conveyor=controller;collector.queue=queue;collector.spawner=spawner;collector.agent=agent;collector.table=table;collector.profile=profile;
        agent.collector=collector;
        var menuRoot=New("Personalization_Menu_Console",equipment);
        menuRoot.position=console.position+Vector3.back*.06f;menuRoot.rotation=console.rotation;
        Box("Menu_Backplate",menuRoot,new Vector3(0,1.13f,0),new Vector3(1.15f,.75f,.035f),dark,true);
        var menu=systems.gameObject.AddComponent<PersonalizationConsole>();
        menu.tracking=tracking;menu.profile=profile;menu.collector=collector;menu.agent=agent;menu.estimator=estimator;
        menu.workConsole=console.gameObject;menu.menuConsole=menuRoot.gameObject;
        menu.reportLabel=DynamicText(menuRoot,"Report_Text",new Vector3(-.52f,1.48f,-.027f),.09f,TextAnchor.UpperLeft);
        menu.buttons=new Transform[5];menu.buttonLabels=new TextMesh[5];
        for(int i=0;i<5;i++)
        {
            float x=-.40f+i*.20f;
            menu.buttons[i]=Box("Menu_Action_"+i,menuRoot,new Vector3(x,.84f,-.04f),new Vector3(.16f,.13f,.045f),i==0?green:blue,true).transform;
            menu.buttonLabels[i]=DynamicText(menuRoot,"Menu_Label_"+i,new Vector3(x,.84f,-.069f),.10f,TextAnchor.MiddleCenter);
        }
        menuRoot.gameObject.SetActive(false);
        SetLayerRecursively(console,AvatarDemoSetup.VrLayer);
        SetLayerRecursively(consoleStand.transform,AvatarDemoSetup.VrLayer);
        SetLayerRecursively(menuRoot,AvatarDemoSetup.VrLayer);
        // Texture property animation cannot use a statically batched belt renderer.
        GameObjectUtility.SetStaticEditorFlags(controller.beltRenderer.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);
        BuildPanel(done,estimator,task,calibration);
        AvatarDemoSetup.Attach(root);
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
    static void SetLayerRecursively(Transform root,int layer)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
    }
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
        var fiveKg=Material("Part_5kg_Coral",new Color(.8f,.23f,.27f),.25f);
        var shell=Box("Machined_Housing",go.transform,Vector3.zero,Vector3.one,fiveKg);if(rounded!=null){shell.GetComponent<MeshFilter>().sharedMesh=rounded;shell.transform.localScale=new Vector3(1/.38f,1/.22f,1/.35f);}
        part.bodyRenderer=shell.GetComponent<Renderer>();part.weightMaterials=new[]{blue,amber,fiveKg};
        // The old black insert and four metal studs resembled a button on every part.
        // Print the weight directly on the housing so only the control panel reads as interactive.
        var labelObject=new GameObject("Weight_Label");labelObject.transform.SetParent(go.transform,false);labelObject.transform.localPosition=new Vector3(0,.525f,0);labelObject.transform.localRotation=Quaternion.Euler(90,0,0);labelObject.transform.localScale=Vector3.one*.16f;
        var weightLabel=labelObject.AddComponent<TextMesh>();weightLabel.text="5 kg";weightLabel.anchor=TextAnchor.MiddleCenter;weightLabel.alignment=TextAlignment.Center;weightLabel.fontSize=64;weightLabel.characterSize=.1f;weightLabel.color=Color.white;
        weightLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");labelObject.GetComponent<MeshRenderer>().sharedMaterial=weightLabel.font.material;part.weightLabel=weightLabel;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Task_Part.prefab");Object.DestroyImmediate(go);return prefab.GetComponent<ConveyorPart>();
    }
    static InputActionAsset CreateActions()
    {
        string path=Root+"/Input/FactoryQuest.inputactions";
        if(!File.Exists(path))
        {
            var asset=ScriptableObject.CreateInstance<InputActionAsset>();var map=new InputActionMap("FactoryQuest");asset.AddActionMap(map);
            AddAction(map,"HeadPosition","<XRHMD>/centerEyePosition","Vector3");AddAction(map,"HeadRotation","<XRHMD>/centerEyeRotation","Quaternion");AddAction(map,"HeadTracked","<XRHMD>/isTracked","Button");
            foreach(string side in new[]{"Left","Right"})
            {
                string device="<XRController>{"+side+"Hand}";
                AddAction(map,side+"Position",device+"/devicePosition","Vector3");AddAction(map,side+"Rotation",device+"/deviceRotation","Quaternion");AddAction(map,side+"Tracked",device+"/isTracked","Button");AddAction(map,side+"Grip",device+"/gripPressed","Button");
            }
            AddAction(map,"Recalibrate","<XRController>{LeftHand}/secondaryButton","Button");
            File.WriteAllText(path,asset.ToJson());Object.DestroyImmediate(asset);AssetDatabase.ImportAsset(path);
        }
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
    static void BuildPanel(Transform completionBin,BodyLoadEstimator estimator,XRGrabTaskTracker task,UserCalibration calibration)
    {
        // Mount the live report beside the bin, outside the worker's central task view.
        Box("Load_Panel_Anchor",completionBin,new Vector3(.475f,1.07f,-.38f),new Vector3(.13f,.035f,.035f),steel).layer=AvatarDemoSetup.VrLayer;
        Box("Load_Panel_Post",completionBin,new Vector3(.52f,1.2f,-.38f),new Vector3(.035f,.32f,.035f),steel).layer=AvatarDemoSetup.VrLayer;
        Box("Load_Panel_Arm",completionBin,new Vector3(.67f,1.32f,-.38f),new Vector3(.38f,.035f,.035f),steel).layer=AvatarDemoSetup.VrLayer;
        var go=new GameObject("Relative_Load_Panel",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(completionBin,false);go.transform.localPosition=new Vector3(.85f,1.51f,-.43f);go.transform.localRotation=Quaternion.Euler(0,25,0);go.transform.localScale=Vector3.one*.0015f;
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(350,390);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        Image("Panel_Background",rect,Vector2.zero,new Vector2(350,390),new Color(.035f,.055f,.08f,.94f));
        Image("Title",rect,new Vector2(0,157),new Vector2(262,55),Color.white,"Title");Image("Divider",rect,new Vector2(0,122),new Vector2(305,1),new Color(.3f,.45f,.53f));
        Image("Left_Label",rect,new Vector2(-120,99),new Vector2(70,28),new Color(.65f,.76f,.82f),"Left");Image("Right_Label",rect,new Vector2(120,99),new Vector2(70,28),new Color(.65f,.76f,.82f),"Right");
        Image("Head_Schematic",rect,new Vector2(0,89),new Vector2(37,37),new Color(.62f,.73f,.8f),"Circle");Image("Neck",rect,new Vector2(0,62),new Vector2(15,21),new Color(.4f,.52f,.62f));
        Image("Shoulder_Link",rect,new Vector2(0,46),new Vector2(122,10),new Color(.35f,.46f,.55f));
        Image("Pelvis",rect,new Vector2(0,-66),new Vector2(68,20),new Color(.35f,.46f,.55f));
        foreach(float x in new[]{-20f,20f})Image("Leg_Schematic",rect,new Vector2(x,-102),new Vector2(15,58),new Color(.24f,.34f,.43f));
        var visual=go.AddComponent<BodyLoadVisualizer>();visual.estimator=estimator;visual.task=task;visual.calibration=calibration;visual.regions=new Image[7];visual.regionNumbers=new NumberReadout[7];
        Vector2[] positions={new Vector2(-65,45),new Vector2(65,45),new Vector2(-81,0),new Vector2(81,0),new Vector2(-94,-54),new Vector2(94,-54),new Vector2(0,-9)};
        for(int i=0;i<7;i++)
        {
            Vector2 size=i==6?new Vector2(60,90):i==2||i==3?new Vector2(19,57):new Vector2(26,26);
            visual.regions[i]=Image("Body_Region_"+i,rect,positions[i],size,BodyLoadVisualizer.ColorFor(0),i==0||i==1||i==4||i==5?"Circle":null);
            Vector2 numberPos=i==6?new Vector2(0,-12):positions[i]+new Vector2(i%2==0?-46:46,0);
            visual.regionNumbers[i]=Number("Score_"+i,rect,numberPos,27);
        }
        Image("Footer_Line",rect,new Vector2(0,-141),new Vector2(305,1),new Color(.3f,.45f,.53f));
        Image("Weight_Label",rect,new Vector2(-123,-166),new Vector2(56,24),new Color(.6f,.73f,.8f),"Weight");visual.weight=Number("Weight",rect,new Vector2(-75,-166),23);Image("Kg",rect,new Vector2(-36,-166),new Vector2(36,24),Color.white,"Kg");
        Image("Average_Label",rect,new Vector2(47,-166),new Vector2(56,24),new Color(.6f,.73f,.8f),"Average");visual.average=Number("Average",rect,new Vector2(106,-166),23);
        visual.glyphs=new Sprite[10];for(int i=0;i<10;i++)visual.glyphs[i]=Sprites["Digit"+i];
        visual.calibrationProgress=Image("Calibration_Progress",rect,new Vector2(152,158),new Vector2(13,13),Color.cyan,"Circle");visual.calibrationProgress.type=UnityEngine.UI.Image.Type.Filled;visual.calibrationProgress.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;visual.calibrationProgress.fillAmount=0;
        string matPath=Root+"/Materials/LoadPanel_Overlay.mat";var overlay=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(overlay==null){overlay=new Material(Shader.Find("FactoryTask/OverlayUI"));AssetDatabase.CreateAsset(overlay,matPath);}overlay.shader=Shader.Find("FactoryTask/OverlayUI");EditorUtility.SetDirty(overlay);foreach(var graphic in go.GetComponentsInChildren<Graphic>())graphic.material=overlay;
    }
    static void PanelText(Transform parent,string value,Vector3 position,float scale=.024f)
    {
        var label=new GameObject("Panel_Label_"+value.Replace(' ','_'));label.transform.SetParent(parent,false);
        label.transform.localPosition=position;label.transform.localScale=Vector3.one*scale;
        var text=label.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.fontSize=48;text.characterSize=.08f;text.color=Color.white;
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
    }
    static TextMesh DynamicText(Transform parent,string name,Vector3 position,float scale,TextAnchor anchor)
    {
        var label=new GameObject(name);label.transform.SetParent(parent,false);
        label.transform.localPosition=position;label.transform.localScale=Vector3.one*scale;
        var text=label.AddComponent<TextMesh>();text.anchor=anchor;
        text.alignment=anchor==TextAnchor.UpperLeft?TextAlignment.Left:TextAlignment.Center;
        text.fontSize=48;text.characterSize=.08f;text.color=Color.white;
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
        return text;
    }
    static void PanelTopText(Transform parent,string value,float x,float y,float z)
    {
        var label=new GameObject("Button_Top_"+value);label.transform.SetParent(parent,false);
        label.transform.localPosition=new Vector3(x,y,z);
        label.transform.localRotation=Quaternion.Euler(90,0,0);
        label.transform.localScale=Vector3.one*.045f;
        var text=label.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.fontSize=48;text.characterSize=.08f;text.color=Color.white;
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
    }
    static bool IsDecorativeFactoryLabel(string name)
    {
        if(name.IndexOf("SMART FACTORY",StringComparison.OrdinalIgnoreCase)>=0||
           name.IndexOf("N O V A",StringComparison.OrdinalIgnoreCase)>=0)return true;
        if(!name.StartsWith("Label_",StringComparison.OrdinalIgnoreCase))return false;
        return name.IndexOf("EXIT",StringComparison.OrdinalIgnoreCase)<0&&
               name.IndexOf("FIRE",StringComparison.OrdinalIgnoreCase)<0&&
               name.IndexOf("400 V",StringComparison.OrdinalIgnoreCase)<0&&
               name.IndexOf("SAFETY",StringComparison.OrdinalIgnoreCase)<0;
    }
    static bool IsUnusedLineSignHardware(string name)
    {
        return name=="Sign_Backplate"||name=="Sign_Suspension"||
               name=="Infeed_Sign_Post"||name=="Sign_Foot"||name=="Station_Sign_Support";
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
        PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.nova.smartfactory.questtask");PlayerSettings.productName="Adaptive Workbench VR";
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
            if(tracker.queue.slots.Length!=12)errors.Add("Queue must have twelve accumulation slots");
            if(tracker.pool.prefab==null||tracker.completionVolume==null||tracker.tracking.actions==null)errors.Add("Missing task reference");
            if(tracker.pool.poolSize>16)errors.Add("Unbounded pool");
        }
        var table=Object.FindFirstObjectByType<AdjustableWorktable>();
        if(table==null||table.movingTop==null||table.legs==null||table.legs.Length!=4)errors.Add("Missing adjustable pickup worktable");
        else if(tracker!=null&&(tracker.queue.tableSlotCount!=3||tracker.queue.slots.Take(3).Any(slot=>!slot.IsChildOf(table.movingTop))||
                tracker.queue.slots.Skip(3).Any(slot=>slot.IsChildOf(table.movingTop))))
            errors.Add("Only the three pickup slots should follow the moving table");
        var beltTop=GameObject.Find("Production_Line_A")?.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="Upper_Belt");
        var pickupTop=GameObject.Find("Pickup_Table")?.GetComponent<Renderer>();
        if(beltTop==null||pickupTop==null)errors.Add("Missing belt or pickup table geometry");
        else
        {
            if(pickupTop.bounds.min.x-beltTop.bounds.max.x<.10f||Mathf.Abs(pickupTop.bounds.center.z-beltTop.bounds.center.z)>.05f)
                errors.Add("Belt and pickup table must form a left-to-right line without overlap");
            var bin=GameObject.Find("Completion_Box")?.GetComponentInChildren<Renderer>();
            if(bin==null||bin.bounds.min.x-pickupTop.bounds.max.x<.10f||Mathf.Abs(bin.bounds.center.z-pickupTop.bounds.center.z)>.05f)
                errors.Add("Completion bin must follow the pickup table in the same line");
            if(tracker!=null&&tracker.queue.slots.Take(3).Any(slot=>slot.position.y<pickupTop.bounds.max.y))errors.Add("Pickup slot is embedded in the table");
            if(tracker!=null&&tracker.queue.slots.Skip(3).Any(slot=>slot.position.x>beltTop.bounds.max.x||slot.position.x<beltTop.bounds.min.x))
                errors.Add("Backlog slot is outside the conveyor belt");
        }
        var speedControls=Object.FindFirstObjectByType<ConveyorSpeedControls>();
        var agentControls=Object.FindFirstObjectByType<AdaptiveErgonomicsAgent>();
        if(speedControls==null||agentControls==null)
            errors.Add("Missing operator controls");
        else
        {
            var buttons=new List<Transform>{speedControls.slowerButton,speedControls.fasterButton};
            if(speedControls.speedLabel==null)
                errors.Add("Operator controls have missing buttons");
            if(Object.FindFirstObjectByType<PartSelectionControls>()!=null||
               Object.FindFirstObjectByType<VRFeedbackControls>()!=null||
               agentControls.toggleButton!=null||Object.FindFirstObjectByType<TableHeightControls>()!=null||
               GameObject.Find("Select_1kg")!=null||GameObject.Find("Select_3kg")!=null||GameObject.Find("Select_5kg")!=null)
                errors.Add("Legacy buttons remain visible");
            if(tracker!=null&&tracker.pool.Mode!=PartSpawner.SupplyMode.FiveKg)
                errors.Add("Supply must be fixed at 5 kg");
            var standing=tracker==null||tracker.calibration==null||tracker.calibration.standingPoint==null?
                new Vector3(5.05f,0,-3.70f):tracker.calibration.standingPoint.position;
            if(tracker!=null&&tracker.queue!=null&&tracker.queue.slots!=null&&tracker.queue.slots.Length>0)
            {
                var delta=tracker.queue.slots[0].position-standing;delta.y=0;
                if(delta.magnitude>.85f)errors.Add("First pickup is too far from the calibrated standing position");
            }
            foreach(var button in buttons)
                if(button==null||Mathf.Abs(button.position.x-standing.x)>.60f||button.position.z-standing.z>-.45f||
                   button.position.z-standing.z<-.95f||button.gameObject.layer!=AvatarDemoSetup.VrLayer)
                    errors.Add("Operator button is not hidden behind the worker: "+(button==null?"null":button.name));
            var console=GameObject.Find("Completion_Control_Console");
            if(console==null||console.transform.parent==null||console.transform.parent.name!="Task_Equipment")
                errors.Add("Operator console is not mounted behind the worker");
            var personal=Object.FindFirstObjectByType<PersonalizationConsole>();
            if(personal==null||personal.profile==null||personal.menuConsole==null||personal.menuConsole.activeSelf||
               personal.buttons==null||personal.buttons.Length!=5||!agentControls.approvalRequired)
                errors.Add("Contextual profile, feedback and approval menu is not ready");
            for(int i=0;i<buttons.Count;i++)for(int j=i+1;j<buttons.Count;j++)
                if(buttons[i]!=null&&buttons[j]!=null&&VRButtonTouch.IsInReach(buttons[i].position-buttons[i].forward*.15f,buttons[j].position,buttons[j].rotation))
                    errors.Add("One controller touch could activate two buttons: "+buttons[i].name+" / "+buttons[j].name);
        }
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(t.gameObject.activeInHierarchy&&IsDecorativeFactoryLabel(t.name))errors.Add("Decorative factory label still visible: "+t.name);
        foreach(string lineName in new[]{"Production_Line_A","Production_Line_B"})
        {
            var lineRoot=GameObject.Find(lineName);
            if(lineRoot!=null)foreach(var t in lineRoot.GetComponentsInChildren<Transform>(true))
                if(t.gameObject.activeInHierarchy&&IsUnusedLineSignHardware(t.name))errors.Add("Blank line sign still visible: "+t.name);
        }
        if(GameObject.Find("Agent_Status_Backing")!=null)errors.Add("Obstructive agent monitor remains");
        foreach(string name in new[]{"Production_Line_B","Workstations","Factory_Props"})
            if(scene.GetRootGameObjects().Any(g=>g.name==name&&g.activeSelf))errors.Add("Unneeded background remains: "+name);
        if(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Any(t=>t.name=="Rear_Feature_Panel"&&t.gameObject.activeInHierarchy))
            errors.Add("Blank rear feature panel remains visible");
        var loadPanel=GameObject.Find("Relative_Load_Panel")?.GetComponent<RectTransform>();
        var completionBin=GameObject.Find("Completion_Box");
        if(completionBin!=null&&completionBin.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Complete_Sign"||t.name=="Complete"))
            errors.Add("Decorative completion sign remains on the bin");
        if(loadPanel==null||completionBin==null||loadPanel.transform.parent!=completionBin.transform)
            errors.Add("Load report must be mounted on the completion bin, not the headset");
        else if(loadPanel.position.x-loadPanel.rect.width*loadPanel.lossyScale.x*.5f<completionBin.transform.position.x+.46f||
                loadPanel.position.y-loadPanel.rect.height*loadPanel.lossyScale.y*.5f<1.12f)
            errors.Add("Load report overlaps the completion bin");
        if(scene.GetRootGameObjects().Count(g=>g.name=="Quest_Task")!=1)errors.Add("Duplicate task root");
        if(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled&&c.targetTexture==null)!=1)errors.Add("Expected only one active first-person output camera");
        foreach(var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)!=0)errors.Add("Missing script: "+go.name);
            foreach(var renderer in go.GetComponents<Renderer>())foreach(var material in renderer.sharedMaterials)if(material==null||material.shader==null||!material.shader.isSupported)errors.Add("Broken material: "+go.name);
        }
        foreach(var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))if(graphic.material==null||graphic.material.shader==null||graphic.material.shader.name=="Hidden/InternalErrorShader"||!graphic.material.shader.isSupported)errors.Add("Broken UI material: "+graphic.name);
        string report="Scene: "+ScenePath+"\nTask roots: "+scene.GetRootGameObjects().Count(g=>g.name=="Quest_Task")+"\nQueue: 12 FIFO accumulation slots (includes in-transit parts)\nPool: 16\nSupply: fixed 5 kg\nRuntime camera: tracked first person only\nLightmaps preserved: "+LightmapSettings.lightmaps.Length+"\nErrors: "+errors.Count+"\n"+string.Join("\n",errors);
        Directory.CreateDirectory("Documentation/QuestTask");File.WriteAllText("Documentation/QuestTask/SceneValidation.txt",report);Debug.Log(report);if(errors.Count>0)throw new Exception("Quest task validation failed.");
    }
    public static void SetupBatch(){Setup();Setup();Validate();QuestTaskVerification.RunMathChecks();Capture();}
    [MenuItem("Tools/Smart Factory/Quest Task/Build Quest APK")]
    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Builds/QuestTask");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestTask/Adaptive_Workbench_VR.apk",target=BuildTarget.Android,options=BuildOptions.None});
        string apk="Builds/QuestTask/Adaptive_Workbench_VR.apk";File.WriteAllText("Documentation/QuestTask/AndroidBuild.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nAPK bytes: "+(File.Exists(apk)?new FileInfo(apk).Length:0));
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
        ShaderUtil.allowAsyncCompilation=false;var camera=Camera.main;if(camera==null)return;var rotation=camera.transform.localRotation;
        camera.transform.LookAt(new Vector3(4.45f,.99f,-3.05f),Vector3.up);
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Documentation/QuestTask/TaskView.png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;camera.transform.localRotation=rotation;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        camera.transform.LookAt(new Vector3(4.90f,1.0f,-5.0f),Vector3.up);
        rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();old=RenderTexture.active;RenderTexture.active=rt;
        image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
        File.WriteAllBytes("Documentation/QuestTask/ControlPanelView.png",image.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        camera.transform.LookAt(new Vector3(6.60f,1.51f,-3.48f),Vector3.up);
        rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();old=RenderTexture.active;RenderTexture.active=rt;
        image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
        File.WriteAllBytes("Documentation/QuestTask/LoadPanelView.png",image.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;camera.transform.localRotation=rotation;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
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
        try{switch(command){case "setup":QuestTaskSetup.Setup();break;case "open":QuestTaskSetup.Open();break;case "validate":QuestTaskSetup.Validate();break;case "playtests":QuestTaskSetup.PlayTests();break;case "capture":QuestTaskSetup.Capture();break;default:throw new Exception("Unknown local editor command");}File.WriteAllText("Temp/FactoryTask.result",command+" OK");}
        catch(Exception error){File.WriteAllText("Temp/FactoryTask.result",error.ToString());Debug.LogException(error);}
    }
}
