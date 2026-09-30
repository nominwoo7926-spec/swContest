using UnityEngine;

namespace FactoryTask
{
    [DefaultExecutionOrder(-150)]
    public sealed class UserCalibration : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public Transform standingPoint;
        public XRGrabTaskTracker task;
        public bool IsCalibrated { get; private set; }
        public float Progress => Mathf.Clamp01(steadySeconds/2);
        public Vector3 BaselineHead { get; private set; }
        public Vector3 Forward { get; private set; } = Vector3.forward;
        public Vector3 Right { get; private set; } = Vector3.right;
        public float ShoulderHalfWidth { get; private set; } = .19f;
        public float ShoulderDrop { get; private set; } = .23f;
        float steadySeconds, resetHeld;
        Vector3 previousHead, previousLeft, previousRight, sum;
        Vector3 anchorHead,anchorLeft,anchorRight,anchorForward;
        float samples;
        bool hadTracking,resetConsumed;
        void Update() { Tick(Time.unscaledDeltaTime); }
        public void Tick(float dt)
        {
            if(!tracking.Recalibrate){resetHeld=0;resetConsumed=false;}
            else if(!resetConsumed&&(task==null||task.HeldWeight==0))resetHeld+=dt;
            if(resetHeld>=1&&!resetConsumed){ResetCalibration();resetConsumed=true;}
            if(IsCalibrated)return;
            if(!tracking.AllTracked || !tracking.IsFloorOrigin){steadySeconds=samples=0;sum=Vector3.zero;hadTracking=false;return;}
            Vector3 h=tracking.head.position,l=tracking.leftHand.position,r=tracking.rightHand.position;
            if(!hadTracking){anchorHead=h;anchorLeft=l;anchorRight=r;anchorForward=tracking.head.forward;}
            bool stable=hadTracking && Vector3.Distance(h,previousHead)<Mathf.Max(.012f,.14f*dt) && Vector3.Distance(l,previousLeft)<Mathf.Max(.02f,.22f*dt) && Vector3.Distance(r,previousRight)<Mathf.Max(.02f,.22f*dt);
            stable &= Vector3.Distance(h,anchorHead)<.05f&&Vector3.Distance(l,anchorLeft)<.08f&&Vector3.Distance(r,anchorRight)<.08f&&Vector3.Angle(tracking.head.forward,anchorForward)<8;
            previousHead=h;previousLeft=l;previousRight=r;hadTracking=true;
            if(!stable){steadySeconds=samples=0;sum=Vector3.zero;anchorHead=h;anchorLeft=l;anchorRight=r;anchorForward=tracking.head.forward;return;}
            steadySeconds+=dt;samples++;sum+=h;
            if(steadySeconds>=2) Complete(sum/samples);
        }
        void Complete(Vector3 meanHead)
        {
            float stature=tracking.head.localPosition.y;
            if(stature<.8f||stature>2.3f){steadySeconds=samples=0;sum=Vector3.zero;return;}
            Vector3 localHead=tracking.origin.InverseTransformPoint(meanHead);
            Vector3 localForward=Vector3.ProjectOnPlane(tracking.head.localRotation*Vector3.forward,Vector3.up).normalized;
            if(localForward.sqrMagnitude<.1f)return;
            tracking.origin.rotation=standingPoint.rotation*Quaternion.Inverse(Quaternion.LookRotation(localForward,Vector3.up));
            Vector3 horizontal=new Vector3(localHead.x,0,localHead.z);
            tracking.origin.position=standingPoint.position-tracking.origin.rotation*horizontal;
            BaselineHead=tracking.origin.TransformPoint(localHead);Forward=standingPoint.forward;Right=standingPoint.right;
            ShoulderHalfWidth=Mathf.Clamp(stature*.115f,.15f,.24f);ShoulderDrop=Mathf.Clamp(stature*.14f,.18f,.29f);IsCalibrated=true;
        }
        public Vector3 Shoulder(HandSide side)
        {
            Vector3 centre=tracking.head.position-Vector3.up*ShoulderDrop-Forward*.045f;
            return centre+Right*(side==HandSide.Left?-ShoulderHalfWidth:ShoulderHalfWidth);
        }
        public void ResetCalibration(){IsCalibrated=false;steadySeconds=samples=0;sum=Vector3.zero;hadTracking=false;}
        public void ApplyRecordedCalibration(Vector3 baseline,Vector3 forward)
        {
            IsCalibrated=true;BaselineHead=baseline;Forward=forward.normalized;Right=Vector3.Cross(Vector3.up,Forward).normalized;
            float stature=baseline.y-standingPoint.position.y;
            ShoulderHalfWidth=Mathf.Clamp(stature*.115f,.15f,.24f);ShoulderDrop=Mathf.Clamp(stature*.14f,.18f,.29f);
        }
    }
}
