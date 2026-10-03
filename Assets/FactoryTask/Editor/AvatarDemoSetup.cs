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
using Object = UnityEngine.Object;

public static class AvatarDemoSetup
{
    public const string Root="Assets/FactoryTask/Avatar";
    public const string PrefabPath=Root+"/Worker_Avatar.prefab";
    public const int VrLayer=28,PanelLayer=29,AvatarLayer=30;
    static readonly HumanBodyBones[] Required={HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};

    [MenuItem("Tools/Smart Factory/Avatar Demo/Setup or Repair Avatar Demo")]
    public static void Setup(){QuestTaskSetup.Setup();}
    public static void Attach(Transform taskRoot)
    {
        Directory.CreateDirectory(Root+"/Generated");AssetDatabase.Refresh();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null){BuildPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Rocketbox/Construction_Male_05.fbx"),true);prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
        var tracker=taskRoot.GetComponentInChildren<XRGrabTaskTracker>();var tracking=tracker.tracking;
        var worker=(GameObject)PrefabUtility.InstantiatePrefab(prefab,taskRoot);
        var ik=worker.GetComponent<FullBodyAvatarIK>();ik.tracking=tracking;ik.calibration=tracker.calibration;
        ik.modelRoot.position=tracker.calibration.standingPoint.position;
        worker.GetComponent<AvatarLoadHeatmap>().estimator=tracker.estimator;
        var recorder=taskRoot.gameObject.AddComponent<VRSessionRecorder>();
        recorder.tracking=tracking;recorder.calibration=tracker.calibration;recorder.task=tracker;recorder.estimator=tracker.estimator;recorder.pool=tracker.pool;recorder.conveyor=taskRoot.GetComponentInChildren<ConveyorController>();recorder.table=taskRoot.GetComponentInChildren<AdjustableWorktable>();recorder.avatar=ik;
        var map=tracking.actions.actionMaps[0];
        if(tracking.actions.FindAction("RecordToggle")==null){map.AddAction("RecordToggle",UnityEngine.InputSystem.InputActionType.Button,"<XRController>{RightHand}/secondaryButton");File.WriteAllText(AssetDatabase.GetAssetPath(tracking.actions),tracking.actions.ToJson());AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(tracking.actions));}
        // Both cameras retain one shared tracking/input system. The observer renders only to its texture.
        var main=tracking.head.GetComponent<Camera>();main.cullingMask&=~((1<<AvatarLayer)|(1<<PanelLayer));
        var vrPanel=taskRoot.GetComponentsInChildren<BodyLoadVisualizer>().First(v=>v.name=="Relative_Load_Panel");SetLayer(vrPanel.gameObject,VrLayer);
        foreach(var marker in tracking.leftHand.GetComponentsInChildren<Renderer>())marker.gameObject.layer=VrLayer;
        foreach(var marker in tracking.rightHand.GetComponentsInChildren<Renderer>())marker.gameObject.layer=VrLayer;
        var cameraObject=new GameObject("SpectatorCamera");cameraObject.transform.SetParent(taskRoot,false);
        var camera=cameraObject.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=40;camera.fieldOfView=48;camera.allowHDR=false;camera.allowMSAA=true;
        camera.cullingMask=~(1<<VrLayer);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        // Camera replaces the right-hand prop cart: nearly level side/front view of the work.
        camera.transform.position=new Vector3(7.40f,1.45f,-5.80f);
        camera.transform.LookAt(new Vector3(3.75f,1.15f,-3.05f));
        string texturePath=Root+"/Generated/Spectator_1080p.renderTexture";
        var texture=AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
        if(texture==null){texture=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){name="Spectator_1080p",antiAliasing=2};AssetDatabase.CreateAsset(texture,texturePath);}
        camera.targetTexture=texture;
        var panel=Object.Instantiate(vrPanel.gameObject,camera.transform);panel.name="Spectator_Load_Panel";SetLayer(panel,PanelLayer);
        var panelRect=panel.GetComponent<RectTransform>();panelRect.localRotation=Quaternion.identity;panelRect.localScale=Vector3.one*.0009f;
        panel.GetComponent<Canvas>().worldCamera=camera;
        panelRect.anchoredPosition3D=new Vector3(-.78f,.33f,1.7f);
        var viewer=cameraObject.AddComponent<SpectatorCameraController>();viewer.spectatorCamera=camera;viewer.output=texture;viewer.spectatorPanel=panel;viewer.session=recorder;
        // No observer render is performed on the standalone headset at runtime.
        EditorUtility.SetDirty(tracking.actions);AssetDatabase.SaveAssets();
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
                    mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_HeatOpacity",1f);
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
