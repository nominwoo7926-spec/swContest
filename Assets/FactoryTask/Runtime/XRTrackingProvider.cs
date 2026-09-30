using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

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
        InputAction record;
        InputAction hp, hr, ht, lp, lr, lt, lg, rp, rr, rt, rg, reset;
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>(2);
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
            }
            ReadPoses(); LeftGrip=lg.IsPressed();RightGrip=rg.IsPressed();Recalibrate=reset.IsPressed();
            RecordToggle=record!=null&&record.IsPressed();
        }
        void ReadPoses()
        {
            if(ExternalPlayback)return;
#if UNITY_EDITOR
            if (Simulated) return;
#endif
            if(hp==null)return;
            HeadTracked=Focused&&ht.IsPressed();LeftTracked=Focused&&lt.IsPressed();RightTracked=Focused&&rt.IsPressed();
            if(HeadTracked)head.SetLocalPositionAndRotation(hp.ReadValue<Vector3>(),hr.ReadValue<Quaternion>());
            if(LeftTracked)leftHand.SetLocalPositionAndRotation(lp.ReadValue<Vector3>(),lr.ReadValue<Quaternion>());
            if(RightTracked)rightHand.SetLocalPositionAndRotation(rp.ReadValue<Vector3>(),rr.ReadValue<Quaternion>());
        }
        public bool Tracked(HandSide side)=>side==HandSide.Left?LeftTracked:RightTracked;
        public Transform Hand(HandSide side)=>side==HandSide.Left?leftHand:rightHand;
        public void ApplyRecordedPose(Vector3 hp,Quaternion hq,Vector3 lp,Quaternion lq,Vector3 rp,Quaternion rq,int flags)
        {
            ExternalPlayback=true;Focused=true;floorOrigin=true;
            head.SetPositionAndRotation(hp,hq);leftHand.SetPositionAndRotation(lp,lq);rightHand.SetPositionAndRotation(rp,rq);
            HeadTracked=(flags&1)!=0;LeftTracked=(flags&2)!=0;RightTracked=(flags&4)!=0;
            LeftGrip=(flags&8)!=0;RightGrip=(flags&16)!=0;Recalibrate=false;RecordToggle=false;
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
