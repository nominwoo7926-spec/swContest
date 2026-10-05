using UnityEngine;

namespace FactoryTask
{
    [DefaultExecutionOrder(-150)]
    public sealed class UserCalibration : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public Transform standingPoint;
        public XRGrabTaskTracker task;
        [Tooltip("시점이 너무 낮으면 양수(+), 너무 높으면 음수(-) 입력 (단위: 미터)")]
        public float heightAdjust = 0f;
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
        bool hadTracking,resetConsumed,placed,recalibrateWasPressed;
        const float MaxHeightAdjust=.25f;
        void Update() { Tick(Time.unscaledDeltaTime); }
        public void Tick(float dt)
        {
            // 캘리브레이션 완료 후 왼쪽 조이스틱 위/아래로 시점 높이 실시간 조절 (0.4m/s)
            // BaselineHead도 같이 이동해야 부하 기준점이 올바르게 유지됨
            if(IsCalibrated)
            {
                float thumb=tracking.LeftThumbY;
                if(Mathf.Abs(thumb)>0.3f)
                {
                    float delta=thumb*0.4f*dt;
                    // Only a small eye-height correction: a large lift reads as standing on the table.
                    float next=Mathf.Clamp(heightAdjust+delta,-MaxHeightAdjust,MaxHeightAdjust);
                    delta=next-heightAdjust;heightAdjust=next;
                    tracking.origin.position+=Vector3.up*delta;
                    BaselineHead+=Vector3.up*delta;
                }
            }
            // Left Y: a short press teleports the user back to the table (keeping the calibration);
            // holding it for a second recalibrates from scratch.
            bool recalibrate=tracking.Recalibrate;
            if(recalibrate)
            {
                if(!resetConsumed&&(task==null||task.HeldWeight==0))resetHeld+=dt;
                if(resetHeld>=1&&!resetConsumed){ResetCalibration();resetConsumed=true;}
            }
            else
            {
                if(recalibrateWasPressed&&!resetConsumed&&resetHeld<.6f)Recenter();
                resetHeld=0;resetConsumed=false;
            }
            recalibrateWasPressed=recalibrate;
            if(IsCalibrated)return;
            // Put the user at the table straight away; the steady-pose calibration below refines it.
            if(!placed&&tracking.HeadTracked&&tracking.IsFloorOrigin)
            {
                Vector3 localForward=Vector3.ProjectOnPlane(tracking.head.localRotation*Vector3.forward,Vector3.up);
                if(localForward.sqrMagnitude>.01f){Align(tracking.head.localPosition,localForward.normalized);placed=true;}
            }
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
            // The standing eye height is the reference for the torso load. Keep the same reference for
            // the same person across runs and app restarts, so baseline and AI runs are comparable;
            // a forced recalibration (hold left Y) or a clearly different height starts a new one.
            float stored=PlayerPrefs.GetFloat(EyeHeightKey,0);
            if(!forceNewReference&&stored>0&&Mathf.Abs(stored-localHead.y)<SamePersonTolerance)localHead.y=stored;
            else{PlayerPrefs.SetFloat(EyeHeightKey,localHead.y);PlayerPrefs.Save();}
            forceNewReference=false;
            Align(localHead,localForward);
            BaselineHead=tracking.origin.TransformPoint(localHead);Forward=standingPoint.forward;Right=standingPoint.right;
            ShoulderHalfWidth=Mathf.Clamp(stature*.115f,.15f,.24f);ShoulderDrop=Mathf.Clamp(stature*.14f,.18f,.29f);IsCalibrated=true;
        }
        // Teleport: put the user's current head position back on the standing spot, facing the line.
        // The calibrated baseline height and stature are kept; only its floor position follows.
        public void Recenter()
        {
            if(!tracking.HeadTracked)return;
            Vector3 localForward=Vector3.ProjectOnPlane(tracking.head.localRotation*Vector3.forward,Vector3.up);
            if(localForward.sqrMagnitude<.01f)return;
            Align(tracking.head.localPosition,localForward.normalized);
            if(IsCalibrated)BaselineHead=new Vector3(standingPoint.position.x,BaselineHead.y,standingPoint.position.z);
        }
        // Moves the rig so the given tracking-space head position stands on standingPoint, facing its forward.
        void Align(Vector3 localHead,Vector3 localForward)
        {
            tracking.origin.rotation=standingPoint.rotation*Quaternion.Inverse(Quaternion.LookRotation(localForward,Vector3.up));
            Vector3 horizontal=new Vector3(localHead.x,0,localHead.z);
            tracking.origin.position=standingPoint.position-tracking.origin.rotation*horizontal+Vector3.up*heightAdjust;
        }
        public Vector3 Shoulder(HandSide side)
        {
            Vector3 centre=tracking.head.position-Vector3.up*ShoulderDrop-Forward*.045f;
            return centre+Right*(side==HandSide.Left?-ShoulderHalfWidth:ShoulderHalfWidth);
        }
        const string EyeHeightKey="NOVA_StandingEyeHeight";
        const float SamePersonTolerance=.08f;
        bool forceNewReference;
        // Hold left Y: a new person (or a new reference) - the stored eye height is replaced.
        public void ResetCalibration(){forceNewReference=true;IsCalibrated=false;steadySeconds=samples=0;sum=Vector3.zero;hadTracking=false;placed=false;}
        public void ApplyRecordedCalibration(Vector3 baseline,Vector3 forward)
        {
            IsCalibrated=true;BaselineHead=baseline;Forward=forward.normalized;Right=Vector3.Cross(Vector3.up,Forward).normalized;
            float stature=baseline.y-standingPoint.position.y;
            ShoulderHalfWidth=Mathf.Clamp(stature*.115f,.15f,.24f);ShoulderDrop=Mathf.Clamp(stature*.14f,.18f,.29f);
        }
    }
}
