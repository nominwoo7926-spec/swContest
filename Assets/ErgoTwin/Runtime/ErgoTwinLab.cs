using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.Rendering;

namespace ErgoTwin
{
    public sealed class ErgoTwinLab : MonoBehaviour
    {
        public GameObject workerPrefab;
        public Material workerMaterial;
        public bool autoDemo = true;
        public bool automaticControl = true;
        public float partMassKg = 3;
        public float Score => load.score;
        public string State => state;
        public int Cycles => cycles;
        public bool Stopped => stopped;
        public float TableHeight => tableHeight;
        public Camera View => view;
        Transform world, bench, supply, rightHand, leftHand, head, avatar, robotUpper, robotLower, carried;
        Transform origin;
        Camera view;
        Material dark, steel, white, teal, amber, black, cyan, red;
        Text metrics, status, comparison, controls, modeText;
        Image scoreBar;
        readonly List<Transform> parts = new List<Transform>();
        readonly List<Transform> bones = new List<Transform>();
        readonly Dictionary<Transform, Quaternion> restRotations = new Dictionary<Transform, Quaternion>();
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        readonly List<float> history = new List<float>();
        LineRenderer plot;
        ErgoPolicy.Load load;
        Vector3 handPosition, leftPosition, headPosition;
        float tableHeight = .82f, supplyHeight = 1.62f, supplyDistance = .87f, phase, exposure, beforeSum, afterSum;
        float sampleTimer, uiTimer, yaw = -27, pitch = 12, targetHeight = .82f, targetSupply = 1.62f, targetDistance = .87f;
        float conveyorSpeed = .55f, elapsed, afterWait;
        int beforeCount, afterCount, cycles;
        bool stopped, holding, xr, tracked, adjusting, optimized, prevGrip, prevA, prevB, prevX, prevY;
        string state = "OBSERVE / collecting baseline", reason = "Waiting for repeated high reach";
        StreamWriter telemetry;
        readonly Vector3 operatorPosition = new Vector3(0, 0, -.25f);

        void Start()
        {
            BuildWorld();
            Application.runInBackground = true;
            Application.targetFrameRate = 72;
            QualitySettings.vSyncCount = 0;
            try {
                string path = Path.Combine(Application.persistentDataPath, "ErgoTwin-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                telemetry = new StreamWriter(path);
                telemetry.WriteLine("seconds,input,load,reach_m,arm_deg,trunk_est_deg,moment_Nm,table_m,supply_m,speed_mps,state");
                Debug.Log("ERGOTWIN telemetry: " + path);
            } catch (Exception e) { Debug.LogWarning("Telemetry unavailable: " + e.Message); }
        }

        public void BuildWorld()
        {
            Transform previous = null;
            foreach (Transform child in transform) if (child.name == "ERGO TWIN / WORLD") { previous = child; break; }
            if (previous != null) { previous.gameObject.SetActive(false); if (Application.isPlaying) Destroy(previous.gameObject); else DestroyImmediate(previous.gameObject); }
            parts.Clear(); bones.Clear(); restRotations.Clear();
            world = Group("ERGO TWIN / WORLD", transform);
            dark = Mat("Graphite", new Color(.065f,.092f,.12f), .35f);
            steel = Mat("Anodized alloy", new Color(.32f,.42f,.46f), .75f);
            white = Mat("Porcelain", new Color(.76f,.83f,.83f), .2f);
            teal = Mat("Petrol enamel", new Color(.035f,.33f,.35f), .45f);
            amber = Mat("Safety orange", new Color(1,.36f,.065f), .15f);
            black = Mat("Rubber", new Color(.017f,.024f,.03f), 0);
            cyan = Mat("Signal mint", new Color(.15f,.95f,.8f), .2f, true);
            red = Mat("Alarm", new Color(1,.09f,.055f), 0, true);
            Shell(); Station(); Worker(); Rig(); Dashboard();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f,.66f,.75f);
            RenderSettings.ambientEquatorColor = new Color(.33f,.39f,.43f);
            RenderSettings.ambientGroundColor = new Color(.16f,.19f,.23f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.08f,.12f,.16f);
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .018f;
            var light = new GameObject("Daylight / soft shadows").AddComponent<Light>();
            light.transform.SetParent(world); light.type = LightType.Directional; light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(42,-32,0); light.shadows = LightShadows.Soft;
            light.color = new Color(1,.94f,.84f);
        }

        Material Mat(string name, Color color, float metal, bool glow = false)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying) {
                string safe=System.Text.RegularExpressions.Regex.Replace(name,"[^a-zA-Z0-9]","_");
                var saved=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/ErgoTwin/Art/Materials/"+safe+".mat");
                if(saved!=null)return saved;
            }
#endif
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name = name; m.color = color;
            m.SetFloat("_Metallic",metal); m.SetFloat("_Smoothness",.38f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color * 1.8f); }
            return m;
        }
        Transform Group(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent,false); return t; }
        Transform Shape(string name, Vector3 p, Vector3 scale, Material m, Transform parent = null, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent == null ? world : parent,false);
            go.transform.localPosition = p; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }
        void Label(string text, Vector3 p, float size, Color color, Transform parent = null)
        {
            var t = Group(text,parent == null ? world : parent); t.localPosition = p;
            var mesh = t.gameObject.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 80; mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.color = color;
        }
        void Shell()
        {
            Shape("Foundation",new Vector3(0,-.15f,1),new Vector3(18,.3f,16),dark);
            for(int x=-8;x<=8;x+=2) for(int z=-6;z<=8;z+=2)
                Shape("Expansion seam",new Vector3(x,.002f,z),new Vector3(1.98f,.012f,1.98f),dark);
            Shape("Cell platform",new Vector3(0,.025f,.7f),new Vector3(5,.04f,4.3f),black);
            for(int i=0;i<22;i++) {
                var stripe=Shape("Perimeter chevron",new Vector3(-2.35f+i*.22f,.053f,-1.4f),new Vector3(.11f,.008f,.32f),amber);
                stripe.localRotation=Quaternion.Euler(0,-30,0);
            }
            Shape("Rear wall",new Vector3(0,2.5f,8),new Vector3(18,5,.2f),dark);
            Shape("Left wall",new Vector3(-9,2.5f,1),new Vector3(.2f,5,14),dark);
            for(int x=-8;x<=8;x+=4) {
                Shape("Structural column",new Vector3(x,2.5f,7.8f),new Vector3(.2f,5,.4f),steel);
                Shape("Light blade",new Vector3(x+.25f,2.5f,7.55f),new Vector3(.045f,3.7f,.04f),cyan);
                Shape("Roof beam",new Vector3(x,4.8f,1),new Vector3(.16f,.25f,14),steel);
                Shape("Suspended luminaire",new Vector3(x,4.55f,1),new Vector3(.15f,.06f,5),white);
            }
            Label("ERGO / TWIN",new Vector3(0,3.85f,7.7f),.12f,Color.white);
            Label("HUMAN-CENTERED AUTOMATION     /     RESEARCH FLOOR 01",new Vector3(0,3.25f,7.7f),.028f,new Color(.3f,.95f,.8f));
            for(int i=0;i<3;i++) {
                float x=-5.8f+i*5.7f;
                Shape("Service cabinet",new Vector3(x,1,6.8f),new Vector3(1.7f,2,.8f),teal);
                Shape("Cabinet inset",new Vector3(x,1.35f,6.37f),new Vector3(1.4f,.65f,.04f),black);
                for(int j=0;j<6;j++) Shape("Vent",new Vector3(x,.35f+j*.07f,6.36f),new Vector3(1.3f,.018f,.03f),steel);
                Label("0"+(i+1)+" / SYSTEM",new Vector3(x,1.4f,6.33f),.022f,Color.white);
            }
            for(int i=0;i<8;i++) {
                Shape("Walkway dash",new Vector3(3.9f,.016f,-4+i*1.35f),new Vector3(.06f,.014f,.65f),cyan);
            }
            for(int i=0;i<4;i++) {
                Shape("Safety bollard",new Vector3(-3.3f,.45f,-.8f+i*1.4f),new Vector3(.12f,.9f,.12f),amber);
            }
            Label("01 / ADAPTIVE ASSEMBLY",new Vector3(-2,.3f,3.45f),.022f,Color.white);
        }
        void Station()
        {
            bench=Group("Servo lift workbench",world); bench.localPosition=new Vector3(.35f,tableHeight,.7f);
            Shape("Worktop",Vector3.zero,new Vector3(1.8f,.12f,.8f),teal,bench);
            Shape("ESD mat",new Vector3(0,.067f,0),new Vector3(1.4f,.012f,.6f),black,bench);
            for(int i=-1;i<=1;i+=2) {
                Shape("Lift pedestal",new Vector3(.35f+i*.68f,.33f,.7f),new Vector3(.22f,.6f,.55f),dark);
                Shape("Telescopic actuator",new Vector3(i*.68f,-.22f,0),new Vector3(.12f,.45f,.25f),steel,bench);
                Shape("Foot",new Vector3(.35f+i*.68f,.08f,.7f),new Vector3(.4f,.1f,.8f),steel);
            }
            Shape("Assembly fixture",new Vector3(0,.13f,0),new Vector3(.38f,.12f,.28f),steel,bench);
            for(int i=-1;i<=1;i+=2) Shape("Fixture clamp",new Vector3(i*.23f,.16f,0),new Vector3(.06f,.12f,.32f),amber,bench);
            supply=Group("Adaptive supply gantry",world); supply.localPosition=new Vector3(-.65f,supplyHeight,supplyDistance-.25f);
            Shape("Parts tray",Vector3.zero,new Vector3(.62f,.08f,.4f),amber,supply);
            Shape("Tray back",new Vector3(0,.1f,.18f),new Vector3(.62f,.2f,.025f),teal,supply);
            for(int i=0;i<3;i++) Shape("Supply billet",new Vector3(-.2f+i*.2f,.08f,0),new Vector3(.14f,.08f,.18f),white,supply);
            Shape("Supply tower",new Vector3(-1.1f,.95f,1),new Vector3(.16f,1.9f,.18f),steel);
            Shape("Supply rail",new Vector3(-.15f,-.12f,.12f),new Vector3(.8f,.1f,.1f),steel,supply);
            Shape("Conveyor bed",new Vector3(-2,.78f,1.3f),new Vector3(.85f,.18f,3.6f),teal);
            for(int i=0;i<24;i++) {
                var roller=Shape("Steel roller",new Vector3(-2,.9f,-.38f+i*.145f),new Vector3(.07f,.37f,.07f),steel,null,PrimitiveType.Cylinder);
                roller.localRotation=Quaternion.Euler(0,0,90);
            }
            for(int i=0;i<5;i++) parts.Add(Shape("Moving billet",new Vector3(-2,1,-.25f+i*.65f),new Vector3(.36f,.17f,.3f),white));
            for(int i=-1;i<=1;i+=2) Shape("Conveyor leg",new Vector3(-2,.4f,1.3f+i*1.35f),new Vector3(.6f,.75f,.12f),steel);
            Shape("Robot plinth",new Vector3(2,.35f,1.65f),new Vector3(.8f,.7f,.8f),dark);
            robotUpper=Shape("Collaborative arm upper",new Vector3(2,1.13f,1.65f),new Vector3(.17f,.35f,.17f),amber,null,PrimitiveType.Cylinder);
            robotLower=Shape("Collaborative arm wrist",new Vector3(1.65f,1.62f,1.65f),new Vector3(.14f,.28f,.14f),white,null,PrimitiveType.Cylinder);
            Shape("Robot elbow bearing",new Vector3(1.72f,1.5f,1.65f),Vector3.one*.24f,steel,null,PrimitiveType.Sphere);
            Shape("Robot tool",new Vector3(1.35f,1.65f,1.65f),new Vector3(.2f,.18f,.18f),dark);
            for(int i=-1;i<=1;i+=2) Shape("Gripper finger",new Vector3(1.35f+i*.1f,1.5f,1.65f),new Vector3(.045f,.2f,.045f),steel);
            Shape("Robot shoulder",new Vector3(2,.8f,1.65f),new Vector3(.3f,.3f,.3f),steel,null,PrimitiveType.Sphere);
            carried=Shape("Workpiece in hand",new Vector3(0,1,0),new Vector3(.16f,.1f,.16f),white);
        }
        void Worker()
        {
            avatar=Group("Worker / estimated digital twin",world); avatar.position=operatorPosition;
            if(workerPrefab!=null) {
                var model=Instantiate(workerPrefab,avatar); model.name="Kenney / rigged worker";
                var anim=model.GetComponent<Animator>(); if(anim!=null) anim.enabled=false;
                var renderers=model.GetComponentsInChildren<Renderer>();
                var bounds=new Bounds(avatar.position,Vector3.zero); foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                float scale=1.76f/Mathf.Max(.1f,bounds.size.y); model.transform.localScale*=scale;
                model.transform.position+=Vector3.up*(operatorPosition.y-bounds.min.y)*scale;
                foreach(var r in renderers) if(workerMaterial!=null) r.sharedMaterial=workerMaterial;
                foreach(var bone in model.GetComponentsInChildren<Transform>()) { bones.Add(bone); restRotations[bone]=bone.localRotation; }
            }
            else Shape("Worker torso fallback",new Vector3(0,1.08f,0),new Vector3(.4f,.52f,.24f),teal,avatar,PrimitiveType.Capsule);
            Shape("Safety helmet",new Vector3(0,1.74f,0),new Vector3(.44f,.23f,.4f),amber,avatar,PrimitiveType.Sphere);
            Shape("Helmet brim",new Vector3(0,1.69f,.035f),new Vector3(.48f,.025f,.47f),amber,avatar);
            rightHand=Shape("Right tracked hand",new Vector3(.3f,1,-.05f),Vector3.one*.075f,cyan,null,PrimitiveType.Sphere);
            leftHand=Shape("Left tracked hand",new Vector3(-.25f,1,-.05f),Vector3.one*.075f,cyan,null,PrimitiveType.Sphere);
            head=Group("Head sample",world);
        }
        void Rig()
        {
            origin=Group("XR origin / floor space",world);
            var cam=Group("Main Camera",origin); view=cam.gameObject.AddComponent<Camera>(); cam.tag="MainCamera";
            view.nearClipPlane=.05f; view.farClipPlane=65; view.fieldOfView=58;
            view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.06f,.09f,.13f);
            cam.gameObject.AddComponent<AudioListener>();
            cam.position=new Vector3(4.1f,2.8f,-5.4f); cam.rotation=Quaternion.Euler(pitch,yaw,0);
            cam.LookAt(new Vector3(-.3f,1.25f,.8f)); yaw=cam.eulerAngles.y; pitch=cam.eulerAngles.x;
        }
        Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Text TextUI(Transform parent,string value,Vector2 p,Vector2 size,int font,Color color)
        {
            var go=new GameObject(value,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=p;rt.sizeDelta=size;
            var t=go.AddComponent<Text>();t.font=Font;t.text=value;t.fontSize=font;t.color=color;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        Image Panel(Transform parent,Vector2 p,Vector2 size,Color color)
        {
            var go=new GameObject("Panel",typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=p;r.sizeDelta=size;var im=go.AddComponent<Image>();im.color=color;return im;
        }
        void Dashboard()
        {
            var go=new GameObject("Operations display",typeof(RectTransform));go.transform.SetParent(world,false);
            var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rt=(RectTransform)go.transform;rt.sizeDelta=new Vector2(960,610);rt.position=new Vector3(.5f,2.02f,3.35f);rt.localScale=Vector3.one*.0034f;
            Panel(rt,Vector2.zero,new Vector2(960,610),new Color(.025f,.045f,.065f));
            Panel(rt,new Vector2(0,0),new Vector2(7,610),new Color(.15f,.95f,.8f));
            TextUI(rt,"ERGO / TWIN",new Vector2(35,-24),new Vector2(650,65),49,Color.white);
            modeText=TextUI(rt,"01  /  LIVE OPERATIONS",new Vector2(36,-92),new Vector2(890,40),22,new Color(.25f,.95f,.8f));
            metrics=TextUI(rt,"Initializing sensors",new Vector2(36,-155),new Vector2(510,200),29,Color.white);
            comparison=TextUI(rt,"BASELINE\n--\nAFTER\n--",new Vector2(605,-150),new Vector2(310,210),28,new Color(.6f,.74f,.8f));
            Panel(rt,new Vector2(36,-366),new Vector2(885,12),new Color(.12f,.19f,.22f));
            scoreBar=Panel(rt,new Vector2(36,-366),new Vector2(10,12),new Color(.2f,.95f,.7f));
            status=TextUI(rt,"OBSERVING",new Vector2(36,-402),new Vector2(885,85),24,Color.white);
            controls=TextUI(rt,"PC: 1 demo / 2 manual | A agent | E approve | SPACE stop\nQuest: A approve / B stop / X reset / Y agent | grip pick",new Vector2(36,-523),new Vector2(885,70),20,new Color(.55f,.68f,.75f));
            var line=Group("Load history / 30 seconds",world);plot=line.gameObject.AddComponent<LineRenderer>();plot.sharedMaterial=cyan;
            plot.startWidth=plot.endWidth=.014f;plot.positionCount=0;plot.useWorldSpace=true;
            Label("LOAD HISTORY / 30 s",new Vector3(.5f,.7f,3.25f),.019f,new Color(.25f,.95f,.8f));
        }

        void Update()
        {
            if(view==null)return;
            elapsed+=Time.deltaTime;
            ReadInput();
            if(!stopped) {
                if(autoDemo && !xr) Simulate();
                UpdateLoad(); ControlAgent();
                if(!adjusting && tracked) AnimateEquipment();
            }
            else state="E-STOP / press reset to resume";
            rightHand.position=handPosition;leftHand.position=leftPosition;head.position=headPosition;
            carried.gameObject.SetActive(holding);carried.position=handPosition+Vector3.down*.065f;
            AnimateWorker();
            sampleTimer+=Time.deltaTime;uiTimer+=Time.deltaTime;
            if(sampleTimer>=.25f) {sampleTimer=0;Sample();}
            if(uiTimer>=.1f) {uiTimer=0;RefreshDisplay();}
        }
        void ReadInput()
        {
            var h=InputDevices.GetDeviceAtXRNode(XRNode.Head);var r=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var l=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool detected=XRSettings.isDeviceActive;
            if(detected!=xr) {xr=detected;if(xr) {origin.position=operatorPosition;SubsystemManager.GetSubsystems(subsystems);foreach(var s in subsystems)s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);}}
            if(xr) {
                bool ht=h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool hv)&&hv;
                bool rr=r.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool rv)&&rv;
                bool ll=l.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool lv)&&lv;
                tracked=ht&&rr&&ll;
                if(h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 hp)) {headPosition=origin.TransformPoint(hp);view.transform.localPosition=hp;}
                if(h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out Quaternion hr))view.transform.localRotation=hr;
                if(r.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 rp))handPosition=origin.TransformPoint(rp);
                if(l.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 lp))leftPosition=origin.TransformPoint(lp);
                r.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton,out bool grip);
                if(grip&&!prevGrip&&Vector3.Distance(handPosition,supply.position+Vector3.up*.1f)<.3f)holding=true;
                if(!grip&&prevGrip) {if(holding&&Vector3.Distance(handPosition,bench.position)<.45f)cycles++;holding=false;}prevGrip=grip;
                r.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out bool a);r.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton,out bool b);
                l.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton,out bool x);l.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton,out bool y);
                if(a&&!prevA)RequestAdjustment();if(b&&!prevB)EmergencyStop();if(x&&!prevX)ResetExperiment();if(y&&!prevY)automaticControl=!automaticControl;
                prevA=a;prevB=b;prevX=x;prevY=y;
                avatar.gameObject.SetActive(false);
                return;
            }
            tracked=true;avatar.gameObject.SetActive(true);
            var k=Keyboard.current;var m=Mouse.current;if(k==null)return;
            if(k.digit1Key.wasPressedThisFrame)autoDemo=true;
            if(k.digit2Key.wasPressedThisFrame) {autoDemo=false;handPosition=new Vector3(.25f,1.1f,.15f);headPosition=new Vector3(0,1.65f,-.25f);leftPosition=new Vector3(-.25f,1,-.2f);holding=false;}
            bool flying=m!=null&&m.rightButton.isPressed;
            if(k.aKey.wasPressedThisFrame&&!flying)automaticControl=!automaticControl;
            if(k.eKey.wasPressedThisFrame&&!flying)RequestAdjustment();
            if(k.spaceKey.wasPressedThisFrame)EmergencyStop();
            if(k.rKey.wasPressedThisFrame)ResetExperiment();
            if(!autoDemo&&!stopped) {
                Vector3 delta=new Vector3((k.rightArrowKey.isPressed?1:0)-(k.leftArrowKey.isPressed?1:0),(k.pageUpKey.isPressed?1:0)-(k.pageDownKey.isPressed?1:0),(k.upArrowKey.isPressed?1:0)-(k.downArrowKey.isPressed?1:0));
                handPosition+=delta*Time.deltaTime*.6f;handPosition=new Vector3(Mathf.Clamp(handPosition.x,-1.3f,1.3f),Mathf.Clamp(handPosition.y,.6f,2),Mathf.Clamp(handPosition.z,-.6f,1.3f));
                if(k.gKey.wasPressedThisFrame) {if(holding){if(Vector3.Distance(handPosition,bench.position)<.5f)cycles++;holding=false;}else if(Vector3.Distance(handPosition,supply.position)<.35f)holding=true;}
            }
            if(m!=null&&m.rightButton.isPressed) {
                Vector2 d=m.delta.ReadValue();yaw+=d.x*.15f;pitch=Mathf.Clamp(pitch-d.y*.15f,-65,70);view.transform.rotation=Quaternion.Euler(pitch,yaw,0);
                Vector3 move=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                view.transform.position+=view.transform.TransformDirection(move)*Time.deltaTime*3;
            }
        }
        void Simulate()
        {
            if(adjusting) {holding=false;handPosition=operatorPosition+new Vector3(.28f,1,.0f);leftPosition=operatorPosition+new Vector3(-.25f,1,0);return;}
            float previous=phase;phase=Mathf.Repeat(phase+Time.deltaTime/6,1);if(phase<previous)cycles++;
            Vector3 rest=operatorPosition+new Vector3(.28f,1,0),pick=supply.position+new Vector3(.12f,.14f,-.02f),place=bench.position+new Vector3(0,.2f,0);
            if(phase<.3f)handPosition=Vector3.Lerp(rest,pick,Mathf.SmoothStep(0,1,phase/.3f));
            else if(phase<.48f)handPosition=pick;
            else if(phase<.74f)handPosition=Vector3.Lerp(pick,place,Mathf.SmoothStep(0,1,(phase-.48f)/.26f));
            else handPosition=Vector3.Lerp(place,rest,Mathf.SmoothStep(0,1,(phase-.74f)/.26f));
            holding=phase>.3f&&phase<.74f;
            leftPosition=operatorPosition+new Vector3(-.23f,1,.08f);
            headPosition=operatorPosition+new Vector3(-.04f,1.65f,Mathf.Max(0,supplyDistance-.55f)*Mathf.Sin(phase*Mathf.PI)*.45f);
        }
        void UpdateLoad()
        {
            if(!tracked){state="TRACKING LOST / actuators held";return;}
            load=ErgoPolicy.Estimate(headPosition,handPosition,operatorPosition+Vector3.up*.9f,partMassKg,holding);
            exposure=Mathf.Max(0,exposure+(load.score>42?Time.deltaTime:-Time.deltaTime*.15f));
        }
        public void RequestAdjustment()
        {
            if(stopped||adjusting||!tracked)return;
            Vector2 best=ErgoPolicy.Optimize(headPosition.y-.23f);
            targetHeight=Mathf.Clamp(best.x,.85f,1.35f);targetSupply=targetHeight+.1f;targetDistance=best.y;
            adjusting=true;reason="Grid search: minimize reach + elevation + load moment";
        }
        void ControlAgent()
        {
            if(!tracked){state="TRACKING LOST / actuators held";return;}
            if(automaticControl&&!optimized&&!adjusting&&exposure>3&&beforeCount>=24)RequestAdjustment();
            if(!adjusting) {state=optimized?"VERIFY / measuring post-adjustment cycles":"OBSERVE / collecting baseline";return;}
            // Conservative volume includes both complete tabletops at every allowed height,
            // the initial raised tray, the whole travel path and a hand-size margin.
            var sweep=ErgoPolicy.TravelVolume;
            if(!ErgoPolicy.Clear(leftPosition,handPosition,sweep,holding,tracked)) {state="SAFETY HOLD / clear hands and release part";return;}
            state="ACT / controlled equipment motion";
            tableHeight=Mathf.MoveTowards(tableHeight,targetHeight,.12f*Time.deltaTime);
            supplyHeight=Mathf.MoveTowards(supplyHeight,targetSupply,.15f*Time.deltaTime);
            supplyDistance=Mathf.MoveTowards(supplyDistance,targetDistance,.12f*Time.deltaTime);
            bench.position=new Vector3(.35f,tableHeight,.7f);supply.position=new Vector3(-.65f,supplyHeight,supplyDistance-.25f);
            conveyorSpeed=Mathf.MoveTowards(conveyorSpeed,.26f,.1f*Time.deltaTime);
            if(Mathf.Abs(tableHeight-targetHeight)<.002f&&Mathf.Abs(supplyHeight-targetSupply)<.002f&&Mathf.Abs(supplyDistance-targetDistance)<.002f) {
                adjusting=false;optimized=true;phase=0;afterWait=elapsed+6;reason="Compare complete cycles; geometric proxy only";
            }
        }
        void AnimateEquipment()
        {
            foreach(var p in parts) {var v=p.position;v.z-=Time.deltaTime*conveyorSpeed;if(v.z<-.4f)v.z=3; p.position=v;}
            // Rigid links stay connected at a shared elbow.
            Vector3 shoulder=new Vector3(2,.8f,1.65f),elbow=new Vector3(1.72f,1.5f,1.65f);
            Vector3 tool=new Vector3(1.35f,1.65f,1.65f);
            Link(robotUpper,shoulder,elbow,.17f);Link(robotLower,elbow,tool,.14f);
        }
        static void Link(Transform link,Vector3 a,Vector3 b,float width)
        {link.position=(a+b)*.5f;link.up=(b-a).normalized;link.localScale=new Vector3(width,Vector3.Distance(a,b)*.5f,width);}
        Transform Bone(string name)=>bones.Find(t=>t.name==name);
        void AnimateWorker()
        {
            if(xr)return;
            foreach(var bone in bones)if(bone!=null)bone.localRotation=restRotations[bone];
            SolveArm(Bone("RightArm"),Bone("RightForeArm"),Bone("RightHand"),handPosition,Vector3.right);
            SolveArm(Bone("LeftArm"),Bone("LeftForeArm"),Bone("LeftHand"),leftPosition,Vector3.left);
        }
        static void SolveArm(Transform upper,Transform lower,Transform wrist,Vector3 target,Vector3 pole)
        {
            if(upper==null||lower==null||wrist==null)return;
            Vector3 start=upper.position,axis=target-start;
            float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,wrist.position);
            float d=Mathf.Clamp(axis.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 direction=axis.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(pole+Vector3.back*.4f,direction).normalized;
            float along=(a*a-b*b+d*d)/(2*d),outward=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 elbow=start+direction*along+bend*outward;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(wrist.position-lower.position,target-lower.position)*lower.rotation;
        }
        void Sample()
        {
            if(stopped||!tracked)return;
            history.Add(load.score);if(history.Count>120)history.RemoveAt(0);
            plot.positionCount=history.Count;
            for(int i=0;i<history.Count;i++)plot.SetPosition(i,new Vector3(-1+i*3f/119,.34f+history[i]*.0028f,3.24f));
            if(!adjusting&&!optimized){beforeSum+=load.score;beforeCount++;}
            else if(optimized&&elapsed>afterWait){afterSum+=load.score;afterCount++;}
            if(telemetry!=null) {
                telemetry.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:F2},{1},{2:F2},{3:F3},{4:F1},{5:F1},{6:F2},{7:F3},{8:F3},{9:F2},{10}",elapsed,xr?"XR estimated body":"SIM",load.score,load.reach,load.elevation,load.trunk,load.moment,tableHeight,supplyHeight,conveyorSpeed,state));
                if(history.Count%20==0)telemetry.Flush();
            }
        }
        void RefreshDisplay()
        {
            modeText.text=(xr?"QUEST / HMD + CONTROLLERS":"DESKTOP / "+(autoDemo?"SIMULATED WORKER":"MANUAL HAND"))+"     |     "+(automaticControl?"AUTO AGENT":"MANUAL APPROVAL");
            metrics.text=$"LOAD  {load.score:00} / 100\nREACH  {load.reach:F2} m    ARM  {load.elevation:0} deg\nMOMENT  {load.moment:F1} Nm\nTABLE  {tableHeight:F2} m    CYCLES  {cycles}";
            comparison.text=$"BASELINE   {(beforeCount>0?(beforeSum/beforeCount).ToString("F1"):"--")}\nAFTER         {(afterCount>0?(afterSum/afterCount).ToString("F1"):"--")}\n\nGEOMETRIC PROXY\nBody pose is estimated";
            scoreBar.rectTransform.sizeDelta=new Vector2(885*load.score/100,12);scoreBar.color=load.score>65?new Color(1,.25f,.1f):load.score>40?new Color(1,.65f,.15f):new Color(.2f,.95f,.7f);
            status.text=state+"\n"+reason;
        }
        public void EmergencyStop(){stopped=true;holding=false;state="E-STOP / reset required";}
        public void ResetExperiment()
        {
            // Reset clears the latch without teleporting moving equipment through a user's hands.
            stopped=false;adjusting=false;optimized=false;exposure=0;beforeSum=afterSum=0;beforeCount=afterCount=cycles=0;phase=0;history.Clear();
            reason="New measurement window; equipment position retained";
        }
        void OnGUI()
        {
            if(xr||view==null)return;
            float scale=Mathf.Clamp(Screen.height/900f,.65f,1.6f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            var title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold};
            var body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};
            GUI.Box(new Rect(18,18,340,178),GUIContent.none);GUI.Label(new Rect(34,28,310,35),"ERGO / TWIN",title);
            GUI.Label(new Rect(34,67,310,60),$"{(autoDemo?"SIMULATED WORKER":"MANUAL WORKER")}  /  {(automaticControl?"AUTO AGENT":"MANUAL APPROVAL")}\nLoad {load.score:0}/100  |  Reach {load.reach:F2} m\n{state}",body);
            if(GUI.Button(new Rect(34,137,145,36),stopped?"RESET [R]":"E-STOP [SPACE]")){if(stopped)ResetExperiment();else EmergencyStop();}
            if(GUI.Button(new Rect(188,137,150,36),"APPROVE [E]"))RequestAdjustment();
            float bottom=Screen.height/scale-68;GUI.Box(new Rect(18,bottom,730,50),GUIContent.none);
            GUI.Label(new Rect(30,bottom+7,705,43),"1 Demo   2 Manual   A Auto agent   R Reset   RMB + WASD Fly\nManual: arrows move hand / PgUp PgDn height / G pick & place.  Estimation demo, not clinical scoring.",body);
        }
        void OnDestroy(){telemetry?.Dispose();}
#if UNITY_EDITOR
        public System.Collections.IEnumerator VerifyRuntime()
        {
            var savedHeight=tableHeight;var savedPart=parts[0].position;
            EmergencyStop();yield return new WaitForSecondsRealtime(.35f);
            bool freeze=stopped&&Mathf.Approximately(savedHeight,tableHeight)&&Vector3.Distance(savedPart,parts[0].position)<.0001f;
            RequestAdjustment();bool latch=stopped;
            ResetExperiment();bool reset=!stopped;
            autoDemo=false;automaticControl=false;handPosition=new Vector3(.2f,1,-.25f);
            Vector3 startHand=handPosition;
            var keyboard=Keyboard.current;
            if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.RightArrow));
            yield return new WaitForSecondsRealtime(.2f);
            bool manual=handPosition.x>startHand.x;
            if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            handPosition=supply.position;
            if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.G));
            yield return new WaitForSecondsRealtime(.1f);
            bool picked=holding;
            if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            handPosition=bench.position;int previousCycles=cycles;
            if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.G));
            yield return new WaitForSecondsRealtime(.1f);
            bool placed=!holding&&cycles==previousCycles+1;
            if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            autoDemo=true;automaticControl=true;
            int listeners=FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            int cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            File.WriteAllText("Documentation/runtime-verification.txt",$"E-stop freezes equipment and conveyor: {freeze}\nApproval cannot release E-stop: {latch}\nReset releases latch: {reset}\nManual arrow movement: {manual}\nG picks nearby part: {picked}\nG places part and completes cycle: {placed}\nActive audio listeners: {listeners}\nActive cameras: {cameras}\nImported worker: {workerPrefab!=null}\n");
            if(!freeze||!latch||!reset||!manual||!picked||!placed||listeners!=1||cameras!=1)Debug.LogError("ERGOTWIN runtime verification failed");
            else Debug.Log("ERGOTWIN runtime verification passed");
        }
#endif
    }
}
