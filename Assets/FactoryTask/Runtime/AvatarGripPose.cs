using Meta.XR.Movement.Retargeting;
using UnityEngine;

namespace FactoryTask
{
    // Final finger pass for both avatar drivers (Movement retargeting and the IK fallback).
    // Runs after them and sets each finger from its rest pose, so nothing accumulates frame to frame.
    // Holding a part wraps the fingers around its face; an empty grip closes to a fist.
    [DefaultExecutionOrder(200)]
    public sealed class AvatarGripPose : MonoBehaviour
    {
        public FullBodyAvatarIK avatar;
        public XRGrabTaskTracker task;
        public CharacterRetargeter retargeter;
        public float relaxedCurl = 12, holdCurl = 38, fistCurl = 62, thumbScale = .55f;
        Quaternion[] leftRest, rightRest;
        float[] leftScale, rightScale;
        float leftCurl, rightCurl, leftWeight = 1, rightWeight = 1;

        void Awake()
        {
            Capture(avatar.leftFingers, out leftRest, out leftScale);
            Capture(avatar.rightFingers, out rightRest, out rightScale);
        }
        void Capture(Transform[] fingers, out Quaternion[] rest, out float[] scale)
        {
            rest = new Quaternion[fingers.Length]; scale = new float[fingers.Length];
            for (int i = 0; i < fingers.Length; i++)
            {
                rest[i] = fingers[i].localRotation;
                scale[i] = fingers[i].name.Contains("Finger0") ? thumbScale : 1;
            }
        }
        void LateUpdate()
        {
            var tracking = avatar.tracking;
            if (tracking == null) return;
            float dt = Time.unscaledDeltaTime;
            bool movement = retargeter != null && retargeter.isActiveAndEnabled;
            Apply(HandSide.Left, tracking.LeftGrip, avatar.leftFingers, avatar.leftFingerAxes, leftRest, leftScale, ref leftCurl, ref leftWeight, movement, dt);
            Apply(HandSide.Right, tracking.RightGrip, avatar.rightFingers, avatar.rightFingerAxes, rightRest, rightScale, ref rightCurl, ref rightWeight, movement, dt);
        }
        void Apply(HandSide side, bool grip, Transform[] fingers, Vector3[] axes, Quaternion[] rest, float[] scale, ref float curl, ref float weight, bool movement, float dt)
        {
            if (fingers == null || axes == null || fingers.Length != axes.Length) return;
            bool holding = task != null && (task.enabled ? task.Held(side) != null : grip && task.HeldWeight > 0);
            float targetCurl = holding ? holdCurl : grip ? fistCurl : relaxedCurl;
            // Movement already supplies a natural open hand; only take over while gripping.
            float targetWeight = movement && !holding && !grip ? 0 : 1;
            curl = Mathf.Lerp(curl, targetCurl, 1 - Mathf.Exp(-dt * 15));
            weight = Mathf.Lerp(weight, targetWeight, 1 - Mathf.Exp(-dt * 12));
            for (int i = 0; i < fingers.Length; i++)
            {
                Quaternion pose = rest[i] * Quaternion.AngleAxis(curl * scale[i], axes[i]);
                fingers[i].localRotation = weight > .999f ? pose : Quaternion.Slerp(fingers[i].localRotation, pose, weight);
            }
        }
    }
}
