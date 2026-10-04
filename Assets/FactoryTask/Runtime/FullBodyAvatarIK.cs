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
        public float LeftSoleY => leftLeg.end.position.y-leftSoleOffset*modelRoot.lossyScale.y;
        public float RightSoleY => rightLeg.end.position.y-rightSoleOffset*modelRoot.lossyScale.y;
        Quaternion headOffset;
        Vector3 leftFoot, rightFoot, smoothedRoot, bodyCorrection;
        Quaternion leftFootRotation, rightFootRotation;
        bool initialized, planted;
        float smoothDrop, smoothForward, leftSoleOffset, rightSoleOffset;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            ResetSkeleton();
            float soleY=float.PositiveInfinity;
            foreach(var surface in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))soleY=Mathf.Min(soleY,surface.bounds.min.y);
            if(float.IsInfinity(soleY))soleY=Mathf.Min(leftLeg.end.position.y,rightLeg.end.position.y);
            float modelScale=Mathf.Max(.0001f,modelRoot.lossyScale.y);
            leftSoleOffset=(leftLeg.end.position.y-soleY)/modelScale;
            rightSoleOffset=(rightLeg.end.position.y-soleY)/modelScale;
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
            float leftGroundY=floor+leftSoleOffset*scale,rightGroundY=floor+rightSoleOffset*scale;
            if (!planted)
            {
                leftFoot = leftLeg.end.position; rightFoot = rightLeg.end.position;
                leftFoot.y=leftGroundY;rightFoot.y=rightGroundY;
                leftFootRotation = leftLeg.end.rotation; rightFootRotation = rightLeg.end.rotation;
                planted = true;
            }
            // Replant only when the user has actually moved out of the stationary stance.
            Replant(ref leftFoot, leftLeg.end.position, dt);
            Replant(ref rightFoot, rightLeg.end.position, dt);
            // The anchors represent ankle height. Recompute it from the calibrated floor every
            // frame so an elevated first pose or model import offset cannot leave the avatar afloat.
            leftFoot.y=leftGroundY;rightFoot.y=rightGroundY;
            float bend = Mathf.Clamp(smoothDrop * 55 + Mathf.Max(0, smoothForward) * 45, 0, 48);
            hips.position -= Vector3.up * smoothDrop * .3f;
            spine.rotation = Quaternion.AngleAxis(bend * .45f, calibration.Right) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(bend * .55f, calibration.Right) * chest.rotation;
            head.rotation = tracking.head.rotation * headOffset;
            Vector3 eye = head.position + tracking.head.rotation * eyeOffset * scale;
            Vector3 correction=tracking.head.position-eye;
            bodyCorrection=fresh?correction:Vector3.Lerp(bodyCorrection,correction,follow);
            // Move the whole torso through the pelvis. Never translate the head bone itself:
            // doing so changes its local bone length and produces the stretched "rubber neck" seen
            // from the spectator camera.
            hips.position+=Vector3.ClampMagnitude(bodyCorrection,.32f);
            SolveLimb(leftLeg, leftFoot, hips.position + calibration.Forward * 2 - calibration.Right * .15f);
            SolveLimb(rightLeg, rightFoot, hips.position + calibration.Forward * 2 + calibration.Right * .15f);
            leftLeg.end.rotation = leftFootRotation; rightLeg.end.rotation = rightFootRotation;
            ApplyArm(leftArm, tracking.leftHand, tracking.LeftTracked, -1);
            ApplyArm(rightArm, tracking.rightHand, tracking.RightTracked, 1);
            // Finger curl is applied afterwards by AvatarGripPose for both avatar drivers.
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
            // Use a small clavicle swing for the last part of a reach. This keeps the shoulder
            // connected to the torso while avoiding the old direct upper-arm translation.
            Transform clavicle=arm.upper.parent;
            if(clavicle!=null&&delta.magnitude>reach*.9f)
            {
                Vector3 shoulderVector=arm.upper.position-clavicle.position;
                Vector3 desiredVector=wrist-clavicle.position;
                if(shoulderVector.sqrMagnitude>.0001f&&desiredVector.sqrMagnitude>.0001f)
                {
                    Quaternion swing=Quaternion.FromToRotation(shoulderVector,desiredVector);
                    clavicle.rotation=Quaternion.Slerp(clavicle.rotation,swing*clavicle.rotation,.22f);
                    delta=wrist-arm.upper.position;
                }
            }
            // A three-point tracker cannot know shoulder translation. Clamp unreachable controller
            // poses instead of pulling the shoulder out of the torso or lengthening arm bones.
            if(delta.magnitude>reach*.985f)wrist=arm.upper.position+delta.normalized*(reach*.985f);
            Vector3 pole = arm.upper.position + calibration.Right * side * .22f + calibration.Forward * .42f - Vector3.up * .12f;
            SolveLimb(arm, wrist, pole);
            arm.end.rotation = target.rotation * arm.endRotationOffset;
        }
        public static void SolveLimb(AvatarLimb limb, Vector3 target, Vector3 pole, float maxStretch = 1f)
        {
            Vector3 root = limb.upper.position;
            float a = Vector3.Distance(root, limb.lower.position), b = Vector3.Distance(limb.lower.position, limb.end.position);
            Vector3 delta = target - root; float distance = delta.magnitude;
            if (a < .001f || b < .001f || distance < .001f) return;
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
