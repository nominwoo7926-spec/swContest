using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FactoryTask;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Meta.XR.Movement.Retargeting;
using Meta.XR.Movement.Editor;
using Meta.XR.Movement.Retargeting.Editor;
using Object = UnityEngine.Object;

public static class AvatarDemoSetup
{
    public const string Root="Assets/FactoryTask/Avatar";
    public const string PrefabPath=Root+"/Worker_Avatar.prefab";
    public const int VrLayer=28,PanelLayer=29,AvatarLayer=30;
    static readonly HumanBodyBones[] Required={HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};

    [MenuItem("Tools/Smart Factory/Avatar Demo/Setup or Repair Avatar Demo")]
    public static void Setup(){QuestTaskSetup.Setup();}

    [MenuItem("Tools/Smart Factory/Avatar Demo/Configure Movement Body Tracking")]
    public static void ConfigureMovementBodyTracking()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null)throw new InvalidOperationException("Create the worker avatar prefab first.");
        var metadata=MSDKUtilityEditor.RunDefaultRetargetingSetup(prefab);
        if(metadata==null||metadata.ConfigJson==null)throw new InvalidOperationException("Movement retargeting config could not be generated.");
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var standard=root.GetComponent<MetaSourceDataProvider>();
            if(standard!=null&&standard.GetType()==typeof(MetaSourceDataProvider))Object.DestroyImmediate(standard,true);
            var source=root.GetComponent<MovementBodyPoseStream>();
            if(source==null)source=root.AddComponent<MovementBodyPoseStream>();
            source.ProvidedSkeletonType=OVRPlugin.BodyJointSet.FullBody;
            var retargeter=root.GetComponent<CharacterRetargeter>();
            if(retargeter==null)retargeter=root.AddComponent<CharacterRetargeter>();
            retargeter.ConfigAsset=metadata.ConfigJson;
            CharacterRetargeterConfigEditor.LoadConfig(new SerializedObject(retargeter),retargeter);
            var ik=root.GetComponent<FullBodyAvatarIK>();if(ik!=null)ik.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var runtime=OVRRuntimeSettings.GetRuntimeSettings();
        runtime.BodyTrackingJointSet=OVRPlugin.BodyJointSet.FullBody;
        runtime.BodyTrackingFidelity=OVRPlugin.BodyTrackingFidelity2.High;
        EditorUtility.SetDirty(runtime);AssetDatabase.SaveAssets();
        Debug.Log("Movement Body Tracking configured. Run Setup or Repair Avatar Demo once to refresh the scene instance.");
    }

    public static void ConfigureMovementAndBuildQuestBatch()
    {
        try
        {
            ConfigureMovementBodyTracking();
            QuestTaskSetup.Setup();
            QuestTaskSetup.Validate();
            QuestTaskSetup.BuildAndroid();
            EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
    public static void Attach(Transform taskRoot)
    {
        Directory.CreateDirectory(Root+"/Generated");AssetDatabase.Refresh();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null){BuildPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Rocketbox/Construction_Male_05.fbx"),true);prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
        var tracker=taskRoot.GetComponentInChildren<XRGrabTaskTracker>();var tracking=tracker.tracking;
        var worker=(GameObject)PrefabUtility.InstantiatePrefab(prefab,taskRoot);
        var ik=worker.GetComponent<FullBodyAvatarIK>();ik.tracking=tracking;ik.calibration=tracker.calibration;
        var bodySource=worker.GetComponent<MovementBodyPoseStream>();
        var bodyRetargeter=worker.GetComponent<CharacterRetargeter>();
        var gripPose=worker.GetComponent<AvatarGripPose>();if(gripPose==null)gripPose=worker.AddComponent<AvatarGripPose>();
        gripPose.avatar=ik;gripPose.task=tracker;gripPose.retargeter=bodyRetargeter;
        ik.modelRoot.position=tracker.calibration.standingPoint.position;
        worker.GetComponent<AvatarLoadHeatmap>().estimator=tracker.estimator;
        var recorder=taskRoot.gameObject.AddComponent<VRSessionRecorder>();
        recorder.tracking=tracking;recorder.calibration=tracker.calibration;recorder.task=tracker;recorder.estimator=tracker.estimator;recorder.pool=tracker.pool;recorder.conveyor=taskRoot.GetComponentInChildren<ConveyorController>();recorder.avatar=ik;recorder.bodySource=bodySource;recorder.bodyRetargeter=bodyRetargeter;recorder.line=taskRoot.GetComponentInChildren<LineHeightAdjuster>();
        // The tracked avatar keeps solving every frame (RULA, load estimate) but is no longer filmed;
        // the spectator camera shows the animation-driven dummy instead.
        foreach(var skin in worker.GetComponentsInChildren<Renderer>(true))skin.enabled=false;
        BuildSpectatorDummy(taskRoot,prefab,tracker);
        var map=tracking.actions.actionMaps[0];
        if(tracking.actions.FindAction("RecordToggle")==null){map.AddAction("RecordToggle",UnityEngine.InputSystem.InputActionType.Button,"<XRController>{RightHand}/secondaryButton");File.WriteAllText(AssetDatabase.GetAssetPath(tracking.actions),tracking.actions.ToJson());AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(tracking.actions));}
        // Both cameras retain one shared tracking/input system. The observer renders only to its texture.
        var main=tracking.head.GetComponent<Camera>();main.cullingMask&=~((1<<AvatarLayer)|(1<<PanelLayer)|(1<<FigureLayer));
        var vrPanel=taskRoot.GetComponentInChildren<BodyLoadVisualizer>(true);SetLayer(vrPanel.gameObject,VrLayer);
        foreach(var marker in tracking.leftHand.GetComponentsInChildren<Renderer>())marker.gameObject.layer=VrLayer;
        foreach(var marker in tracking.rightHand.GetComponentsInChildren<Renderer>())marker.gameObject.layer=VrLayer;
        var cameraObject=new GameObject("SpectatorCamera");cameraObject.transform.SetParent(taskRoot,false);
        var camera=cameraObject.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=40;camera.fieldOfView=43;camera.allowHDR=false;camera.allowMSAA=true;
        camera.cullingMask=~((1<<VrLayer)|(1<<FigureLayer)|(1<<QuestTaskSetup.LocalUILayer));camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        // Fixed observer across the table, facing the worker's front and looking down at about 45 degrees.
        var standing=QuestTaskSetup.StandingPosition;var chest=standing+new Vector3(0,1.1f,0);
        camera.transform.position=chest+new Vector3(0,2,2);camera.transform.LookAt(chest);
        string texturePath=Root+"/Generated/Spectator_1080p.renderTexture";
        var texture=AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
        if(texture==null){texture=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){name="Spectator_1080p",antiAliasing=2};AssetDatabase.CreateAsset(texture,texturePath);}
        camera.targetTexture=texture;
        var panel=Object.Instantiate(vrPanel.gameObject,camera.transform);panel.name="Spectator_Load_Panel";SetLayer(panel,PanelLayer);
        var panelRect=panel.GetComponent<RectTransform>();panelRect.localRotation=Quaternion.identity;panelRect.localScale=Vector3.one*.0009f;
        panel.GetComponent<Canvas>().worldCamera=camera;
        panelRect.anchoredPosition3D=new Vector3(-.95f,.3f,1.7f);
        BuildBeltSpeedLabel(camera,taskRoot.GetComponentInChildren<ConveyorController>());
        var viewer=cameraObject.AddComponent<SpectatorCameraController>();viewer.spectatorCamera=camera;viewer.output=texture;viewer.spectatorPanel=panel;viewer.session=recorder;
        // No observer render is performed on the standalone headset at runtime.
        EditorUtility.SetDirty(tracking.actions);AssetDatabase.SaveAssets();
    }
    const string IdleClipPath=Root+"/Animations/Worker_Idle.fbx";
    // Spectator-only worker: same skinned model and load colours, driven by an Animator
    // (Idle <-> PickAndPlace) plus IK instead of headset tracking.
    static void BuildSpectatorDummy(Transform taskRoot,GameObject prefab,XRGrabTaskTracker tracker)
    {
        var idle=ImportIdleClip();
        if(idle==null){Debug.LogWarning("Spectator dummy skipped: "+IdleClipPath+" is missing.");return;}
        var controller=BuildDirectorController(idle);
        var dummy=VisualWorker(prefab,taskRoot,"Spectator_Worker_Dummy",tracker.estimator,controller);
        var standing=tracker.calibration.standingPoint;
        // A step closer to the line than the VR user's spot, so both hands reach the table and bin.
        dummy.transform.SetPositionAndRotation(standing.position+standing.forward*.22f,standing.rotation);
        var animator=dummy.GetComponentInChildren<Animator>(true);
        // The carried part: the task part's look at the single 5 kg size, without physics.
        var partPrefab=tracker.pool.prefab;
        var carried=(GameObject)PrefabUtility.InstantiatePrefab(partPrefab.gameObject,dummy.transform);
        PrefabUtility.UnpackPrefabInstance(carried,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        carried.name="Carried_Part_Visual";var look=carried.GetComponent<ConveyorPart>();
        int kind=tracker.pool.partKind;look.bodyRenderer.sharedMaterial=look.weightMaterials[kind];
        float size=.17f+kind*.035f;carried.transform.localScale=Vector3.one*size;
        Object.DestroyImmediate(look);Object.DestroyImmediate(carried.GetComponent<Collider>());Object.DestroyImmediate(carried.GetComponent<Rigidbody>());
        SetLayer(carried,AvatarLayer);carried.SetActive(false);
        var director=animator.gameObject.AddComponent<ThirdPersonAvatarDirector>();
        director.pickupTable=GameObject.Find("Pickup_Table").GetComponent<Collider>();
        director.binVolume=tracker.completionVolume;
        var binRenderers=GameObject.Find("Completion_Box").GetComponentsInChildren<Renderer>();
        director.binRimHeight=binRenderers.Max(r=>r.bounds.max.y);
        director.binFloorHeight=binRenderers.First(r=>r.name=="Box_Base").bounds.max.y;
        director.visualPart=carried;director.partSize=size;
        var bridge=dummy.AddComponent<GrabToDirectorBridge>();bridge.task=tracker;bridge.director=director;
        BuildLoadFigure(prefab,taskRoot,tracker,controller);
    }
    // A copy of the worker with every tracking driver removed (IK, Movement retargeting, finger
    // pass), keeping the skin, the load heatmap and a humanoid Animator playing the idle clip.
    static GameObject VisualWorker(GameObject prefab,Transform parent,string name,BodyLoadEstimator estimator,RuntimeAnimatorController controller)
    {
        var worker=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);worker.name=name;
        PrefabUtility.UnpackPrefabInstance(worker,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        for(int pass=0;pass<3;pass++)
            foreach(var behaviour in worker.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if(behaviour!=null&&!(behaviour is AvatarLoadHeatmap))Object.DestroyImmediate(behaviour);
        worker.GetComponent<AvatarLoadHeatmap>().estimator=estimator;
        foreach(var skin in worker.GetComponentsInChildren<Renderer>(true))skin.enabled=true;
        var animator=worker.GetComponentInChildren<Animator>(true);
        animator.enabled=true;animator.runtimeAnimatorController=controller;
        animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        return worker;
    }
    public const int FigureLayer=27;
    // Load panel figure: the worker seen from the front by its own small camera, coloured by load.
    static void BuildLoadFigure(GameObject prefab,Transform taskRoot,XRGrabTaskTracker tracker,RuntimeAnimatorController controller)
    {
        var origin=new Vector3(0,-30,0);
        var figure=VisualWorker(prefab,taskRoot,"Load_Panel_Figure",tracker.estimator,controller);
        figure.transform.SetPositionAndRotation(origin,Quaternion.identity);SetLayer(figure,FigureLayer);
        string texturePath=Root+"/Generated/Load_Figure.renderTexture";
        var texture=AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
        if(texture==null){texture=new RenderTexture(256,432,24,RenderTextureFormat.ARGB32){name="Load_Figure",antiAliasing=4};AssetDatabase.CreateAsset(texture,texturePath);}
        var cameraObject=new GameObject("Load_Figure_Camera");cameraObject.transform.SetParent(taskRoot,false);
        cameraObject.transform.SetPositionAndRotation(origin+new Vector3(0,.9f,3),Quaternion.Euler(0,180,0));
        var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.98f;camera.nearClipPlane=.1f;camera.farClipPlane=10;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=QuestTaskSetup.PanelColor;camera.cullingMask=1<<FigureLayer;camera.targetTexture=texture;camera.allowHDR=false;
        var data=camera.GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderPostProcessing=false;data.renderShadows=false;
        cameraObject.AddComponent<LoadFigureCamera>().figureCamera=camera;
        foreach(var panel in taskRoot.GetComponentsInChildren<BodyLoadVisualizer>(true))panel.figure.texture=texture;
    }
    // Belt speed in the top-right corner of the third-person view.
    static void BuildBeltSpeedLabel(Camera camera,ConveyorController conveyor)
    {
        var go=new GameObject("Belt_Speed_Label",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(camera.transform,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(430,74);rect.localScale=Vector3.one*.0009f;rect.localRotation=Quaternion.identity;
        rect.anchoredPosition3D=new Vector3(.78f,.55f,1.7f);
        var background=new GameObject("Background",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        background.rectTransform.SetParent(rect,false);background.rectTransform.sizeDelta=rect.sizeDelta;background.color=QuestTaskSetup.PanelColor;background.raycastTarget=false;
        var text=new GameObject("Text",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
        text.rectTransform.SetParent(rect,false);text.rectTransform.sizeDelta=rect.sizeDelta-new Vector2(28,0);
        text.font=AssetDatabase.LoadAssetAtPath<Font>(QuestTaskSetup.KoreanFontPath)??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=38;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
        text.text="벨트 속도  "+ConveyorController.FixedSpeed.ToString("0.00")+" m/s";
        var label=go.AddComponent<BeltSpeedLabel>();label.conveyor=conveyor;label.label=text;
        SetLayer(go,PanelLayer);
    }
    static AnimationClip ImportIdleClip()
    {
        var importer=AssetImporter.GetAtPath(IdleClipPath) as ModelImporter;
        if(importer==null)return null;
        bool changed=importer.animationType!=ModelImporterAnimationType.Human;
        if(changed){importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips)
        {
            // Loop in place: the dummy's position and heading are owned by the director.
            clip.loopTime=true;clip.lockRootRotation=clip.lockRootHeightY=clip.lockRootPositionXZ=true;
            clip.keepOriginalOrientation=clip.keepOriginalPositionY=clip.keepOriginalPositionXZ=true;
        }
        importer.clipAnimations=clips;importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(IdleClipPath).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
    }
    // Idle --(Trigger PickAndPlace, no exit time, 0.15 s blend)--> PickAndPlace
    // PickAndPlace --(Bool Performing false, no exit time, 0.25 s blend)--> Idle. IK pass on.
    static UnityEditor.Animations.AnimatorController BuildDirectorController(AnimationClip idle)
    {
        string path=Root+"/Generated/Worker_Director.controller";
        AssetDatabase.DeleteAsset(path);
        var controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("PickAndPlace",AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Performing",AnimatorControllerParameterType.Bool);
        var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;
        var machine=controller.layers[0].stateMachine;
        var idleState=machine.AddState("Idle");idleState.motion=idle;machine.defaultState=idleState;
        var action=machine.AddState("PickAndPlace");action.motion=idle;
        var start=idleState.AddTransition(action);start.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If,0,"PickAndPlace");
        start.hasExitTime=false;start.duration=.15f;start.hasFixedDuration=true;
        // The sequence length follows the user's pace, so the director ends the state, not exit time.
        var end=action.AddTransition(idleState);end.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot,0,"Performing");
        end.hasExitTime=false;end.duration=.25f;end.hasFixedDuration=true;
        AssetDatabase.SaveAssets();
        return controller;
    }
    static void SetLayer(GameObject go,int layer){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}

    [MenuItem("Tools/Smart Factory/Avatar Demo/Use Selected Humanoid Model")]
    public static void UseSelectedModel()
    {
        var source=Selection.activeObject as GameObject;
        if(source==null||!AssetDatabase.Contains(source))throw new InvalidOperationException("Select a Humanoid FBX or prefab in the Project window.");
        BuildPrefab(source,false);Setup();
    }
    static T Save<T>(T value,string path) where T:Object
    {
        var previous=AssetDatabase.LoadAssetAtPath<T>(path);
        if(previous==null){AssetDatabase.CreateAsset(value,path);return value;}
        EditorUtility.CopySerialized(value,previous);Object.DestroyImmediate(value);EditorUtility.SetDirty(previous);return previous;
    }
    static void BuildPrefab(GameObject source,bool rocketbox)
    {
        if(source==null)throw new InvalidOperationException("Bundled worker FBX missing. See Documentation/Avatar/README.md.");
        Directory.CreateDirectory(Root+"/Generated");AssetDatabase.Refresh();
        var root=new GameObject("Worker_Avatar");
        try
        {
            var model=Object.Instantiate(source,root.transform);model.name="Worker_Model";
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            var animator=model.GetComponentInChildren<Animator>();
            Dictionary<HumanBodyBones,Transform> bones;
            if(rocketbox)bones=PrepareRocketbox(model);
            else
            {
                if(animator==null||animator.avatar==null||!animator.avatar.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("Configure the FBX Rig as Humanoid, then Apply and select it again.");
                bones=new Dictionary<HumanBodyBones,Transform>();
                foreach(HumanBodyBones b in Enum.GetValues(typeof(HumanBodyBones)))if(b!=HumanBodyBones.LastBone){var t=animator.GetBoneTransform(b);if(t!=null)bones[b]=t;}
            }
            foreach(var bone in Required)if(!bones.ContainsKey(bone))throw new InvalidOperationException("Missing humanoid bone: "+bone);
            if(animator!=null){animator.enabled=false;animator.applyRootMotion=false;}
            var ik=root.AddComponent<FullBodyAvatarIK>();ik.modelRoot=model.transform;ik.hips=bones[HumanBodyBones.Hips];ik.spine=bones[HumanBodyBones.Spine];ik.chest=bones.ContainsKey(HumanBodyBones.UpperChest)?bones[HumanBodyBones.UpperChest]:bones[HumanBodyBones.Chest];ik.head=bones[HumanBodyBones.Head];
            root.AddComponent<MovementBodyPoseStream>();
            ik.leftArm=Limb(bones,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand);
            ik.rightArm=Limb(bones,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand);
            ik.leftLeg=Limb(bones,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot);
            ik.rightLeg=Limb(bones,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot);
            if(rocketbox){var eye=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip01 LEye");ik.eyeOffset=eye.position-ik.head.position;ik.eyeOffset.x=0;}
            ik.referenceEyeHeight=ik.head.position.y+ik.eyeOffset.y;
            ConfigureHand(ik,ik.leftArm,bones,HumanBodyBones.LeftMiddleProximal,true);
            ConfigureHand(ik,ik.rightArm,bones,HumanBodyBones.RightMiddleProximal,false);
            ik.skeleton=model.GetComponentsInChildren<Transform>().Where(t=>t!=model.transform).ToArray();
            ik.restPositions=ik.skeleton.Select(t=>t.localPosition).ToArray();ik.restRotations=ik.skeleton.Select(t=>t.localRotation).ToArray();
            var surfaces=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var surface in surfaces)
            {
                surface.gameObject.SetActive(true);surface.updateWhenOffscreen=true;surface.quality=SkinQuality.Bone4;
                // Expanded local bounds cover arm reaching without forcing runtime bounds calculations.
                surface.localBounds=new Bounds(surface.localBounds.center,Vector3.one*4);
                surface.sharedMesh=BakeMasks(surface,bones);
                var materials=surface.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var original=materials[i];var mat=new Material(Shader.Find("FactoryTask/WorkerHeat")){name=original.name+"_Heat"};
                    Texture texture=rocketbox?AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Rocketbox/Textures/"+original.name+"_color.tga"):original.mainTexture;
                    mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_HeatOpacity",.68f);
                    materials[i]=Save(mat,Root+"/Generated/"+source.name+"_"+surface.name+"_"+i+".mat");
                }
                surface.sharedMaterials=materials;
            }
            root.AddComponent<AvatarLoadHeatmap>().surfaces=surfaces.Cast<Renderer>().ToArray();SetLayer(root,AvatarLayer);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
        }
        finally{Object.DestroyImmediate(root);}
    }
    static AvatarLimb Limb(Dictionary<HumanBodyBones,Transform> bones,HumanBodyBones a,HumanBodyBones b,HumanBodyBones c)=>new AvatarLimb{upper=bones[a],lower=bones[b],end=bones[c]};
    static void ConfigureHand(FullBodyAvatarIK ik,AvatarLimb arm,Dictionary<HumanBodyBones,Transform> bones,HumanBodyBones middle,bool left)
    {
        Vector3 fingerDirection=bones.ContainsKey(middle)?bones[middle].position-arm.end.position:arm.end.position-arm.lower.position;
        arm.endRotationOffset=Quaternion.FromToRotation(fingerDirection.normalized,Vector3.forward)*arm.end.rotation;
        var anchor=new GameObject(left?"Left_Grip_Anchor":"Right_Grip_Anchor").transform;anchor.SetParent(arm.end,false);
        anchor.localPosition=Quaternion.Inverse(arm.endRotationOffset)*ik.gripFromWrist;
        if(left)ik.leftGripAnchor=anchor;else ik.rightGripAnchor=anchor;
        var fingers=arm.end.GetComponentsInChildren<Transform>().Where(t=>t!=arm.end&&t!=anchor&&t.childCount>0).ToArray();
        var axes=new Vector3[fingers.Length];
        for(int i=0;i<fingers.Length;i++)
        {
            Vector3 along=fingers[i].GetChild(0).position-fingers[i].position;
            Vector3 palmNormal=Vector3.Cross(fingerDirection,Vector3.up).normalized*(left?-1:1);
            axes[i]=fingers[i].InverseTransformDirection(Vector3.Cross(along.normalized,-palmNormal).normalized);
        }
        if(left){ik.leftFingers=fingers;ik.leftFingerAxes=axes;}else{ik.rightFingers=fingers;ik.rightFingerAxes=axes;}
    }
    static Dictionary<HumanBodyBones,Transform> PrepareRocketbox(GameObject model)
    {
        var all=model.GetComponentsInChildren<Transform>();var named=all.ToDictionary(t=>t.name,t=>t);
        var map=new Dictionary<HumanBodyBones,Transform>();
        Action<HumanBodyBones,string> add=(b,n)=>{if(named.TryGetValue("Bip01 "+n,out var t))map[b]=t;};
        add(HumanBodyBones.Hips,"Pelvis");add(HumanBodyBones.Spine,"Spine");add(HumanBodyBones.Chest,"Spine1");add(HumanBodyBones.UpperChest,"Spine2");add(HumanBodyBones.Neck,"Neck");add(HumanBodyBones.Head,"Head");
        foreach(bool left in new[]{true,false})
        {
            string side=left?"Left":"Right",prefix=left?"L ":"R ";
            string[] names={"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot","Toes"};
            string[] source={"Clavicle","UpperArm","Forearm","Hand","Thigh","Calf","Foot","Toe0"};
            for(int i=0;i<names.Length;i++)add((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+names[i]),prefix+source[i]);
            string[] fingers={"Thumb","Index","Middle","Ring","Little"};string[] phalanx={"Proximal","Intermediate","Distal"};
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)add((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+fingers[f]+phalanx[j]),prefix+"Finger"+f+(j==0?"":j.ToString()));
            map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+"UpperLeg")].SetParent(map[HumanBodyBones.Hips],true);
            map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+"Shoulder")].SetParent(map[HumanBodyBones.UpperChest],true);
            var upper=map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+"UpperArm")];
            var lower=map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+"LowerArm")];
            var hand=map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+"Hand")];Vector3 direction=left?Vector3.left:Vector3.right;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation;
        }
        var description=new HumanDescription
        {
            human=map.Select(p=>new HumanBone{humanName=HumanTrait.BoneName[(int)p.Key],boneName=p.Value.name,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
            skeleton=model.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
            upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false
        };
        var avatar=AvatarBuilder.BuildHumanAvatar(model,description);avatar.name="Rocketbox_Worker_Humanoid";
        if(!avatar.isValid||!avatar.isHuman)throw new InvalidOperationException("Worker Humanoid avatar could not be built.");
        var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.avatar=Save(avatar,Root+"/Generated/Worker_Humanoid.asset");animator.enabled=false;
        return map;
    }
    static Mesh BakeMasks(SkinnedMeshRenderer surface,Dictionary<HumanBodyBones,Transform> bones)
    {
        var mesh=Object.Instantiate(surface.sharedMesh);mesh.name=surface.name+"_HeatMask";
        var posed=new Mesh();surface.BakeMesh(posed);var vertices=posed.vertices;Object.DestroyImmediate(posed);
        var weights=mesh.boneWeights;var a=new List<Vector4>(vertices.Length);var b=new List<Vector4>(vertices.Length);
        for(int i=0;i<vertices.Length;i++)
        {
            var world=surface.transform.TransformPoint(vertices[i]);Vector4 ma=Vector4.zero,mb=Vector4.zero;var w=weights[i];
            Accumulate(surface.bones[w.boneIndex0],w.weight0,world,bones,ref ma,ref mb);Accumulate(surface.bones[w.boneIndex1],w.weight1,world,bones,ref ma,ref mb);Accumulate(surface.bones[w.boneIndex2],w.weight2,world,bones,ref ma,ref mb);Accumulate(surface.bones[w.boneIndex3],w.weight3,world,bones,ref ma,ref mb);
            a.Add(ma);b.Add(mb);
        }
        mesh.SetUVs(2,a);mesh.SetUVs(3,b);return Save(mesh,Root+"/Generated/"+mesh.name+".asset");
    }
    static bool Under(Transform bone,Dictionary<HumanBodyBones,Transform> map,HumanBodyBones region)=>map.TryGetValue(region,out var parent)&&(bone==parent||bone.IsChildOf(parent));
    static void Accumulate(Transform bone,float weight,Vector3 vertex,Dictionary<HumanBodyBones,Transform> map,ref Vector4 a,ref Vector4 b)
    {
        if(weight<=0)return;
        foreach(bool left in new[]{true,false})
        {
            var hand=left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;var arm=left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm;var shoulder=left?HumanBodyBones.LeftShoulder:HumanBodyBones.RightShoulder;int s=left?0:1;
            if(Under(bone,map,hand)){b[s]+=weight;return;}
            if(Under(bone,map,arm))
            {
                float wrist=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.055f,.13f,Vector3.Distance(vertex,map[hand].position)));
                float cap=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.085f,.20f,Vector3.Distance(vertex,map[arm].position)));
                a[s]+=weight*cap;a[2+s]+=weight*(1-cap)*(1-wrist);b[s]+=weight*(1-cap)*wrist;return;
            }
            if(Under(bone,map,shoulder)){a[s]+=weight;return;}
        }
        if(Under(bone,map,HumanBodyBones.Neck)||Under(bone,map,HumanBodyBones.Head))return;
        if(Under(bone,map,HumanBodyBones.Spine))b.z+=weight;
    }
    public static void Validate()
    {
        var ik=Object.FindFirstObjectByType<FullBodyAvatarIK>();var viewer=Object.FindFirstObjectByType<SpectatorCameraController>();
        if(ik==null||viewer==null)throw new Exception("Avatar demo is missing");
        var animator=ik.modelRoot.GetComponentInChildren<Animator>();
        if(animator==null||animator.avatar==null||!animator.avatar.isHuman||!animator.avatar.isValid)throw new Exception("Invalid Humanoid worker");
        if(Object.FindObjectsByType<XRTrackingProvider>(FindObjectsSortMode.None).Length!=1)throw new Exception("Duplicate XR tracking rig");
        if(viewer.spectatorCamera.GetUniversalAdditionalCameraData().allowXRRendering||viewer.output==null)throw new Exception("Observer camera must be isolated from headset");
        var mesh=ik.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
        var masks=new List<Vector4>();mesh.GetUVs(2,masks);if(masks.Count!=mesh.vertexCount)throw new Exception("Missing vertex heat masks");
        Directory.CreateDirectory("Documentation/Avatar");
        File.WriteAllText("Documentation/Avatar/SceneValidation.txt","Humanoid: valid\nWorker vertices: "+mesh.vertexCount+"\nTracking providers: 1\nObserver: isolated 1920x1080 texture\nHeat masks: vertex surface weights\nErrors: 0\n");
    }
    public static void SetupBatch(){Setup();Setup();Validate();QuestTaskSetup.Validate();}
}
