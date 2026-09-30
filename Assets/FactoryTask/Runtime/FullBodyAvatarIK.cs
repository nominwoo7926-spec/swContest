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
        Quaternion headOffset;
        Vector3 leftFoot, rightFoot, smoothedRoot, bodyCorrection;
        Quaternion leftFootRotation, rightFootRotation;
        bool initialized, planted;
        float leftCurl, rightCurl, smoothDrop, smoothForward;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            ResetSkeleton();
            headOffset = Quaternion.Inverse(modelRoot.rotation) * head.rotation;
            smoothedRoot = modelRoot.position;
        }
        void ResetSkeleton()
        {
            for (int i = 0; i < skeleton.Length; i++)
                skeleton[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
        }
        void LateUpdate() { SolvePose(Time.unscaledDeltaTime); }
        public void ResetPlant() { planted = false; }
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
            Quaternion heading = Quaternion.LookRotation(calibration.Forward, Vector3.up);
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
                leftFoot = leftLeg.end.position; rightFoot = rightLeg.end.position;
                leftFootRotation = leftLeg.end.rotation; rightFootRotation = rightLeg.end.rotation;
                planted = true;
            }
            // Replant only when the user has actually moved out of the stationary stance.
            Replant(ref leftFoot, leftLeg.end.position, dt);
            Replant(ref rightFoot, rightLeg.end.position, dt);
            float bend = Mathf.Clamp(smoothDrop * 55 + Mathf.Max(0, smoothForward) * 45, 0, 48);
            hips.position -= Vector3.up * smoothDrop * .3f;
            spine.rotation = Quaternion.AngleAxis(bend * .45f, calibration.Right) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(bend * .55f, calibration.Right) * chest.rotation;
            head.rotation = tracking.head.rotation * headOffset;
            Vector3 eye = head.position + tracking.head.rotation * eyeOffset * scale;
            Vector3 correction=tracking.head.position-eye;
            bodyCorrection=fresh?correction:Vector3.Lerp(bodyCorrection,correction,follow);
            hips.position+=bodyCorrection;
            Vector3 neckAdjustment=tracking.head.position-(head.position+tracking.head.rotation*eyeOffset*scale);
            // Smooth the torso while keeping the eye pose exact, with a bounded neck translation.
            if(neckAdjustment.magnitude>.07f)hips.position+=neckAdjustment-neckAdjustment.normalized*.07f;
            head.position=tracking.head.position-tracking.head.rotation*eyeOffset*scale;
            SolveLimb(leftLeg, leftFoot, hips.position + calibration.Forward * 2 - calibration.Right * .15f);
            SolveLimb(rightLeg, rightFoot, hips.position + calibration.Forward * 2 + calibration.Right * .15f);
            leftLeg.end.rotation = leftFootRotation; rightLeg.end.rotation = rightFootRotation;
            ApplyArm(leftArm, tracking.leftHand, tracking.LeftTracked, -1);
            ApplyArm(rightArm, tracking.rightHand, tracking.RightTracked, 1);
            leftCurl = Mathf.Lerp(leftCurl, tracking.LeftGrip ? 58 : 12, 1 - Mathf.Exp(-dt * 15));
            rightCurl = Mathf.Lerp(rightCurl, tracking.RightGrip ? 58 : 12, 1 - Mathf.Exp(-dt * 15));
            Curl(leftFingers, leftFingerAxes, leftCurl);
            Curl(rightFingers, rightFingerAxes, rightCurl);
        }
        static void Replant(ref Vector3 anchor, Vector3 nominal, float dt)
        {
            Vector3 flat = nominal - anchor; flat.y = 0;
            if (flat.sqrMagnitude > .5f * .5f) anchor += flat.normalized * Mathf.Min(flat.magnitude, dt * .9f);
        }
        void ApplyArm(AvatarLimb arm, Transform target, bool tracked, float side)
        {
            // The provider retains the last valid hand pose during individual-hand tracking loss.
            Vector3 wrist = target.position - target.rotation * gripFromWrist;
            float reach=Vector3.Distance(arm.upper.position,arm.lower.position)+Vector3.Distance(arm.lower.position,arm.end.position);
            Vector3 delta=wrist-arm.upper.position;
            if(delta.magnitude>reach*1.24f)arm.upper.position+=delta.normalized*Mathf.Min(.08f,delta.magnitude-reach*1.24f);
            Vector3 pole = arm.upper.position + calibration.Right * side * .45f - Vector3.up * .55f - calibration.Forward * .28f;
            SolveLimb(arm, wrist, pole, 1.25f);
            arm.end.rotation = target.rotation * arm.endRotationOffset;
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
