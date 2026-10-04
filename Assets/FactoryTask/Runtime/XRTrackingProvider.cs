using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace FactoryTask
{
    [DefaultExecutionOrder(-200)]
    public sealed class XRTrackingProvider : MonoBehaviour
    {
        public InputActionAsset actions;
        public Transform origin, head, leftHand, rightHand;
        public bool HeadTracked { get; private set; }
        public bool LeftTracked { get; private set; }
        public bool RightTracked { get; private set; }
        public bool LeftGrip { get; private set; }
        public bool RightGrip { get; private set; }
        public bool Recalibrate { get; private set; }
        public bool Focused { get; private set; } = true;
        public bool AllTracked => HeadTracked && LeftTracked && RightTracked && Focused;
        public bool ExternalPlayback { get; private set; }
        public bool RecordToggle { get; private set; }
        public bool ReportButton { get; private set; }
        public float LeftThumbY
        {
            get
            {
                if(leftThumb!=null)return leftThumb.ReadValue<Vector2>().y;
                if(cachedLeftDevice.isValid&&cachedLeftDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis,out var v))return v.y;
                return 0f;
            }
        }
        InputAction record, reportBtn, leftThumb;
        InputAction hp, hr, ht, lp, lr, lt, lg, rp, rr, rt, rg, reset;
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>(2);
        readonly List<InputDevice> leftDevices = new List<InputDevice>(2);
        InputDevice cachedLeftDevice;
        float nextOriginCheck;
        bool floorOrigin;
        public bool IsFloorOrigin => floorOrigin;
#if UNITY_EDITOR
        public bool Simulated { get; private set; }
#endif
        void Awake()
        {
            hp=actions.FindAction("HeadPosition",true); hr=actions.FindAction("HeadRotation",true); ht=actions.FindAction("HeadTracked",true);
            lp=actions.FindAction("LeftPosition",true); lr=actions.FindAction("LeftRotation",true); lt=actions.FindAction("LeftTracked",true); lg=actions.FindAction("LeftGrip",true);
            rp=actions.FindAction("RightPosition",true); rr=actions.FindAction("RightRotation",true); rt=actions.FindAction("RightTracked",true); rg=actions.FindAction("RightGrip",true); reset=actions.FindAction("Recalibrate",true);
            record=actions.FindAction("RecordToggle",false);
            reportBtn=actions.FindAction("ReportButton",false);
            leftThumb=actions.FindAction("LeftThumbstick",false);
        }
        void OnEnable() { actions.Enable(); Application.onBeforeRender += ReadPoses; }
        void OnDisable() { Application.onBeforeRender -= ReadPoses; actions.Disable(); }
        void OnApplicationFocus(bool value) { Focused=value; }
        void OnApplicationPause(bool value) { Focused=!value; }
        void Update()
        {
            if(ExternalPlayback)return;
#if UNITY_EDITOR
            if (Simulated) return;
#endif
            if (Time.unscaledTime >= nextOriginCheck)
            {
                nextOriginCheck=Time.unscaledTime+2; SubsystemManager.GetSubsystems(subsystems);
                for(int i=0;i<subsystems.Count;i++) if(subsystems[i].running)
                {
                    if (subsystems[i].GetTrackingOriginMode()!=TrackingOriginModeFlags.Floor) subsystems[i].TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                    floorOrigin=subsystems[i].GetTrackingOriginMode()==TrackingOriginModeFlags.Floor;
                }
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left|InputDeviceCharacteristics.Controller,leftDevices);
                if(leftDevices.Count>0)cachedLeftDevice=leftDevices[0];
            }
            ReadPoses(); LeftGrip=lg.IsPressed();RightGrip=rg.IsPressed();Recalibrate=reset.IsPressed();
            RecordToggle=record!=null&&record.IsPressed();
            ReportButton=reportBtn!=null&&reportBtn.IsPressed();
        }
        void ReadPoses()
        {
            if(ExternalPlayback)return;
#if UNITY_EDITOR
            if (Simulated) return;
#endif
            if(hp==null)return;
            var headPos=hp.ReadValue<Vector3>();var headRot=hr.ReadValue<Quaternion>();
            var leftPos=lp.ReadValue<Vector3>();var leftRot=lr.ReadValue<Quaternion>();
            var rightPos=rp.ReadValue<Vector3>();var rightRot=rr.ReadValue<Quaternion>();
            // isTracked binding may not fire on all OpenXR runtimes; fall back to rotation data presence
            HeadTracked=Focused&&(ht.IsPressed()||headRot.w<0.9999f);
            LeftTracked=Focused&&(lt.IsPressed()||leftRot.w<0.9999f);
            RightTracked=Focused&&(rt.IsPressed()||rightRot.w<0.9999f);
            if(HeadTracked)head.SetLocalPositionAndRotation(headPos,headRot);
            if(LeftTracked)leftHand.SetLocalPositionAndRotation(leftPos,leftRot);
            if(RightTracked)rightHand.SetLocalPositionAndRotation(rightPos,rightRot);
        }
        public bool Tracked(HandSide side)=>side==HandSide.Left?LeftTracked:RightTracked;
        public Transform Hand(HandSide side)=>side==HandSide.Left?leftHand:rightHand;
        public void ApplyRecordedPose(Vector3 hp,Quaternion hq,Vector3 lp,Quaternion lq,Vector3 rp,Quaternion rq,int flags)
        {
            ExternalPlayback=true;Focused=true;floorOrigin=true;
            head.SetPositionAndRotation(hp,hq);leftHand.SetPositionAndRotation(lp,lq);rightHand.SetPositionAndRotation(rp,rq);
            HeadTracked=(flags&1)!=0;LeftTracked=(flags&2)!=0;RightTracked=(flags&4)!=0;
            LeftGrip=(flags&8)!=0;RightGrip=(flags&16)!=0;Recalibrate=false;RecordToggle=false;ReportButton=false;
        }
#if UNITY_EDITOR
        // Only compiled in the Editor, for automated Play mode verification. No desktop controls ship.
        public void Simulate(Vector3 headPosition,Vector3 leftPosition,Vector3 rightPosition,bool leftGrip=false,bool rightGrip=false,bool tracked=true)
        {
            Simulated=true;Focused=true;floorOrigin=true;
            head.position=headPosition;head.rotation=Quaternion.identity;leftHand.position=leftPosition;rightHand.position=rightPosition;
            leftHand.rotation=rightHand.rotation=Quaternion.identity;HeadTracked=LeftTracked=RightTracked=tracked;
            LeftGrip=leftGrip;RightGrip=rightGrip;Recalibrate=false;
        }
#endif
    }
}
