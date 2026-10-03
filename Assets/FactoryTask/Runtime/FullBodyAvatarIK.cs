using System;
using UnityEngine;

namespace FactoryTask
{
    [Serializable]
    public sealed class AvatarLimb
    {
        public Transform upper, lower, end;
        public Quaternion endRotationOffset = Quaternion.identity;
    }

    // Analytic two-bone IK. Only the three tracked poses are measured; all other joints are inferred.
    [DefaultExecutionOrder(100)]
    public sealed class FullBodyAvatarIK : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public Transform modelRoot, hips, spine, chest, head;
        public AvatarLimb leftArm = new AvatarLimb(), rightArm = new AvatarLimb();
        public AvatarLimb leftLeg = new AvatarLimb(), rightLeg = new AvatarLimb();
        public Transform leftGripAnchor, rightGripAnchor;
        public Transform[] skeleton;
        public Vector3[] restPositions;
        public Quaternion[] restRotations;
        public Vector3 eyeOffset = new Vector3(0, .10f, .075f);
        public float referenceEyeHeight = 1.65f;
        public Vector3 gripFromWrist = new Vector3(0, -.015f, .07f);
        public Transform[] leftFingers, rightFingers;
        public Vector3[] leftFingerAxes, rightFingerAxes;
        public float PoseConfidence => tracking != null && tracking.AllTracked ? 1 : 0;
        public float LeftGripError => Vector3.Distance(leftGripAnchor.position, tracking.leftHand.position);
        public float RightGripError => Vector3.Distance(rightGripAnchor.position, tracking.rightHand.position);
        public float LeftSoleHeight => leftLeg.end.position.y-leftSoleOffset*modelRoot.localScale.x;
        public float RightSoleHeight => rightLeg.end.position.y-rightSoleOffset*modelRoot.localScale.x;
        public float LeftWristBend => Quaternion.Angle(leftArm.lower.rotation*leftWristRest,leftArm.end.rotation);
        public float RightWristBend => Quaternion.Angle(rightArm.lower.rotation*rightWristRest,rightArm.end.rotation);
        Quaternion headOffset;
        Vector3 leftFoot, rightFoot, smoothedRoot, bodyCorrection, leftStepStart, rightStepStart, leftStepEnd, rightStepEnd;
        Vector3 leftElbowPole, rightElbowPole;
        Quaternion leftFootRotation, rightFootRotation;
        Quaternion leftWristRest, rightWristRest;
        Quaternion smoothedHeading;
        bool initialized, planted;
        float leftCurl, rightCurl, smoothDrop, smoothForward, leftSoleOffset, rightSoleOffset;
        float leftStepProgress=1, rightStepProgress=1;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            ResetSkeleton();
            headOffset = Quaternion.Inverse(modelRoot.rotation) * head.rotation;
            leftWristRest=Quaternion.Inverse(leftArm.lower.rotation)*leftArm.end.rotation;
            rightWristRest=Quaternion.Inverse(rightArm.lower.rotation)*rightArm.end.rotation;
            float minSole=FindRestSoleHeight();
            leftSoleOffset=Mathf.Clamp(leftLeg.end.position.y-minSole,0,.25f);
            rightSoleOffset=Mathf.Clamp(rightLeg.end.position.y-minSole,0,.25f);
            smoothedRoot = modelRoot.position;
            smoothedHeading=modelRoot.rotation;
        }
        float FindRestSoleHeight()
        {
            float lowest=float.PositiveInfinity;
            foreach(var surface in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked=new Mesh();surface.BakeMesh(baked);
                foreach(var vertex in baked.vertices)lowest=Mathf.Min(lowest,surface.transform.TransformPoint(vertex).y);
                Destroy(baked);
            }
            return float.IsInfinity(lowest)?Mathf.Min(leftLeg.end.position.y,rightLeg.end.position.y):lowest;
        }
        void ResetSkeleton()
        {
            for (int i = 0; i < skeleton.Length; i++)
                skeleton[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
        }
        void LateUpdate() { SolvePose(Time.unscaledDeltaTime); }
        public void ResetPlant() { planted = false; leftStepProgress=rightStepProgress=1; leftElbowPole=rightElbowPole=Vector3.zero;
            if(calibration!=null)smoothedHeading=Quaternion.LookRotation(calibration.Forward,Vector3.up); }
        public void SolvePose(float dt)
        {
            Initialize();
            if (!calibration.IsCalibrated) { planted=false; return; }
            if (!tracking.HeadTracked || !tracking.Focused) return;
            bool fresh=!planted;
            ResetSkeleton();
            float floor = calibration.standingPoint.position.y;
            float height = calibration.BaselineHead.y - floor;
            float scale = Mathf.Clamp(height / referenceEyeHeight, .65f, 1.4f);
            modelRoot.localScale = Vector3.one * scale;
            Vector3 gaze=Vector3.ProjectOnPlane(tracking.head.forward,Vector3.up).normalized;
            if(gaze.sqrMagnitude<.1f)gaze=calibration.Forward;
            Quaternion targetHeading=Quaternion.LookRotation(gaze,Vector3.up);
            smoothedHeading=fresh?targetHeading:Quaternion.RotateTowards(smoothedHeading,targetHeading,Mathf.Max(0,dt)*240);
            Quaternion heading=smoothedHeading;
            Vector3 bodyForward=heading*Vector3.forward,bodyRight=heading*Vector3.right;
            Vector3 displacement = tracking.head.position - calibration.BaselineHead;
            float drop = Mathf.Max(0, -displacement.y);
            float forward = Vector3.Dot(displacement, calibration.Forward);
            float follow=1-Mathf.Exp(-dt*12);
            smoothDrop=fresh?drop:Mathf.Lerp(smoothDrop,drop,follow);
            smoothForward=fresh?forward:Mathf.Lerp(smoothForward,forward,follow);
            Vector3 lateral = Vector3.ProjectOnPlane(displacement, Vector3.up) - calibration.Forward * forward;
            Vector3 desiredRoot = calibration.standingPoint.position + lateral * .7f + calibration.Forward * forward * .35f;
            if (!planted) smoothedRoot = desiredRoot;
            smoothedRoot = Vector3.Lerp(smoothedRoot, desiredRoot, 1 - Mathf.Exp(-dt * 9));
            modelRoot.SetPositionAndRotation(smoothedRoot, heading);
            if (!planted)
            {
                leftFootRotation = leftLeg.end.rotation; rightFootRotation = rightLeg.end.rotation;
                leftStepProgress=rightStepProgress=1;
                planted = true;
            }
            float bend = Mathf.Clamp(smoothDrop * 55 + Mathf.Max(0, smoothForward) * 45, 0, 48);
            hips.position -= Vector3.up * smoothDrop * .3f;
            spine.rotation = Quaternion.AngleAxis(bend * .45f, bodyRight) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(bend * .55f, bodyRight) * chest.rotation;
            head.rotation = tracking.head.rotation * headOffset;
            Vector3 eye = head.position + tracking.head.rotation * eyeOffset * scale;
            Vector3 correction=tracking.head.position-eye;
            bodyCorrection=fresh?correction:Vector3.Lerp(bodyCorrection,correction,follow);
            hips.position+=bodyCorrection;
            Vector3 neckAdjustment=tracking.head.position-(head.position+tracking.head.rotation*eyeOffset*scale);
            // Smooth the torso while keeping the eye pose exact, with a bounded neck translation.
            if(neckAdjustment.magnitude>.07f)hips.position+=neckAdjustment-neckAdjustment.normalized*.07f;
            head.position=tracking.head.position-tracking.head.rotation*eyeOffset*scale;
            // The head correction may move the pelvis away from the authored standing
            // point (especially after a floor-height change). Foot targets must follow
            // the corrected pelvis, not the uncorrected model root/rest-pose feet.
            Vector3 leftNominal=hips.position-bodyRight*.12f-bodyForward*.03f;
            Vector3 rightNominal=hips.position+bodyRight*.12f-bodyForward*.03f;
            leftNominal.y=floor+leftSoleOffset*scale;
            rightNominal.y=floor+rightSoleOffset*scale;
            if(fresh)
            {
                leftFoot=leftNominal;rightFoot=rightNominal;
                leftStepProgress=rightStepProgress=1;
            }
            Replant(ref leftFoot,leftNominal,ref leftStepStart,ref leftStepEnd,ref leftStepProgress,rightStepProgress<1,dt);
            Replant(ref rightFoot,rightNominal,ref rightStepStart,ref rightStepEnd,ref rightStepProgress,leftStepProgress<1,dt);
            leftFoot.y=leftNominal.y;rightFoot.y=rightNominal.y;
            SolveLimb(leftLeg, leftFoot, hips.position + bodyForward * 2 - bodyRight * .15f);
            SolveLimb(rightLeg, rightFoot, hips.position + bodyForward * 2 + bodyRight * .15f);
            leftLeg.end.rotation = leftFootRotation; rightLeg.end.rotation = rightFootRotation;
            ApplyArm(leftArm, leftGripAnchor, tracking.leftHand, tracking.LeftTracked, -1,bodyRight,bodyForward,leftWristRest,ref leftElbowPole);
            ApplyArm(rightArm, rightGripAnchor, tracking.rightHand, tracking.RightTracked, 1,bodyRight,bodyForward,rightWristRest,ref rightElbowPole);
            leftCurl = Mathf.Lerp(leftCurl, tracking.LeftGrip ? 58 : 12, 1 - Mathf.Exp(-dt * 15));
            rightCurl = Mathf.Lerp(rightCurl, tracking.RightGrip ? 58 : 12, 1 - Mathf.Exp(-dt * 15));
            Curl(leftFingers, leftFingerAxes, leftCurl);
            Curl(rightFingers, rightFingerAxes, rightCurl);
        }
        static void Replant(ref Vector3 anchor,Vector3 nominal,ref Vector3 stepStart,ref Vector3 stepEnd,ref float progress,bool otherStepping,float dt)
        {
            Vector3 flat=nominal-anchor;flat.y=0;
            if(progress>=1&&flat.sqrMagnitude>.32f*.32f&&!otherStepping)
            {
                stepStart=anchor;stepEnd=anchor+Vector3.ClampMagnitude(flat,.48f);progress=0;
            }
            if(progress>=1)return;
            progress=Mathf.Min(1,progress+dt/.42f);
            float eased=progress*progress*(3-2*progress);
            anchor=Vector3.Lerp(stepStart,stepEnd,eased);
        }
        void ApplyArm(AvatarLimb arm,Transform gripAnchor,Transform target,bool tracked,float side,Vector3 bodyRight,Vector3 bodyForward,Quaternion wristRest,ref Vector3 previousPole)
        {
            // The provider retains the last valid hand pose during individual-hand tracking loss.
            Vector3 wrist = target.position - target.rotation * gripFromWrist;
            float reach=Vector3.Distance(arm.upper.position,arm.lower.position)+Vector3.Distance(arm.lower.position,arm.end.position);
            Vector3 delta=wrist-arm.upper.position;
            if(delta.magnitude>reach*1.04f)arm.upper.position+=delta.normalized*Mathf.Min(.07f,(delta.magnitude-reach*1.04f)*.5f);
            Vector3 pole = arm.upper.position + bodyRight * side * .45f - Vector3.up * .55f - bodyForward * .28f;
            if(previousPole!=Vector3.zero)pole=Vector3.Lerp(pole,previousPole,.35f);
            SolveLimb(arm, wrist, pole, 1.12f);
            previousPole=arm.lower.position;
            Quaternion neutral=arm.lower.rotation*wristRest;
            Quaternion desired=target.rotation*arm.endRotationOffset;
            arm.end.rotation=Quaternion.RotateTowards(neutral,desired,95);
            // Preserve contact with the real tracked grip after the visual bend limit.
            if(gripAnchor!=null)arm.end.position+=Vector3.ClampMagnitude(target.position-gripAnchor.position,.075f);
        }
        static void Curl(Transform[] fingers, Vector3[] axes, float amount)
        {
            for (int i = 0; i < fingers.Length; i++) fingers[i].localRotation *= Quaternion.AngleAxis(amount, axes[i]);
        }
        public static void SolveLimb(AvatarLimb limb, Vector3 target, Vector3 pole, float maxStretch = 1.04f)
        {
            Vector3 root = limb.upper.position;
            float a = Vector3.Distance(root, limb.lower.position), b = Vector3.Distance(limb.lower.position, limb.end.position);
            Vector3 delta = target - root; float distance = delta.magnitude;
            if (a < .001f || b < .001f || distance < .001f) return;
            float stretch = Mathf.Clamp(distance / ((a + b) * .995f), 1, maxStretch);
            limb.lower.localPosition *= stretch; limb.end.localPosition *= stretch;
            a *= stretch; b *= stretch;
            Vector3 direction = delta / distance;
            float d = Mathf.Clamp(distance, Mathf.Abs(a - b) + .001f, a + b - .001f);
            float along = (a * a - b * b + d * d) / (2 * d);
            Vector3 bend = Vector3.ProjectOnPlane(pole - root, direction).normalized;
            if (bend.sqrMagnitude < .1f) bend = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
            Vector3 elbow = root + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            limb.upper.rotation = Quaternion.FromToRotation(limb.lower.position - root, elbow - root) * limb.upper.rotation;
            limb.lower.rotation = Quaternion.FromToRotation(limb.end.position - limb.lower.position, root + direction * d - limb.lower.position) * limb.lower.rotation;
        }
    }
}
