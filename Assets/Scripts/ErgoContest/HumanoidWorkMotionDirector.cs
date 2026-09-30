using UnityEngine;

namespace ErgoContest
{
    public sealed class HumanoidWorkMotionDirector : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform headLookTarget;
        [SerializeField] private Transform supplyPickupPoint;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private Transform restPoint;
        [SerializeField] private float blendSpeed = 8f;
        [SerializeField] private Vector3 leftHandCarryOffset = new Vector3(-0.18f, -0.04f, 0.02f);
        [SerializeField] private Vector3 headLookOffset = new Vector3(0f, 0.1f, 0f);

        private Vector3 rightVelocity;
        private Vector3 leftVelocity;
        private Vector3 lookVelocity;

        public Transform RightHandTarget => rightHandTarget;
        public Transform LeftHandTarget => leftHandTarget;
        public Transform HeadLookTarget => headLookTarget;

        private void LateUpdate()
        {
            if (inputProvider == null)
                return;

            ErgoInputFrame input = inputProvider.CurrentFrame;
            Vector3 rightTarget = input.HandPosition;
            if (rightHandTarget != null)
            {
                rightHandTarget.position = Smooth(rightHandTarget.position, rightTarget, ref rightVelocity);
                rightHandTarget.rotation = input.HandRotation;
            }

            Vector3 leftTarget = input.HoldingPart
                ? rightTarget + GetFrame().TransformVector(leftHandCarryOffset)
                : GetIdleLeftHand();
            if (leftHandTarget != null)
                leftHandTarget.position = Smooth(leftHandTarget.position, leftTarget, ref leftVelocity);

            Vector3 lookTarget = input.HoldingPart || IsNear(input.HandPosition, assemblyPoint, 0.7f)
                ? input.HandPosition + headLookOffset
                : GetDefaultLook();
            if (headLookTarget != null)
                headLookTarget.position = Smooth(headLookTarget.position, lookTarget, ref lookVelocity);
        }

        private Vector3 GetIdleLeftHand()
        {
            Transform frame = GetFrame();
            if (assemblyPoint != null && IsNear(inputProvider.CurrentFrame.HandPosition, assemblyPoint, 0.65f))
                return assemblyPoint.position + frame.TransformVector(new Vector3(-0.18f, 0.05f, 0f));
            return frame.TransformPoint(new Vector3(-0.30f, 0.95f, 0.02f));
        }

        private Vector3 GetDefaultLook()
        {
            if (supplyPickupPoint != null)
                return supplyPickupPoint.position;
            if (restPoint != null)
                return restPoint.position;
            return GetFrame().TransformPoint(new Vector3(0f, 1.25f, 0.7f));
        }

        private Transform GetFrame()
        {
            return restPoint != null && restPoint.parent != null ? restPoint.parent : transform;
        }

        private Vector3 Smooth(Vector3 current, Vector3 target, ref Vector3 velocity)
        {
            float smoothTime = 1f / Mathf.Max(0.01f, blendSpeed);
            return Vector3.SmoothDamp(current, target, ref velocity, smoothTime);
        }

        private static bool IsNear(Vector3 point, Transform target, float distance)
        {
            return target != null && Vector3.Distance(point, target.position) <= distance;
        }
    }
}
