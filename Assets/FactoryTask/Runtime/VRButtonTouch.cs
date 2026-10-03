using UnityEngine;

namespace FactoryTask
{
    // The tracked controller origin is behind its physical tip. Accept a hand position
    // just in front of the panel while keeping neighbouring buttons separated by X/Y.
    public static class VRButtonTouch
    {
        public static bool IsTouching(XRTrackingProvider tracking, Transform button)
        {
            if (tracking == null || button == null || !button.gameObject.activeInHierarchy || !tracking.Focused) return false;
            return (tracking.LeftTracked && IsInReach(tracking.leftHand.position, button.position, button.rotation)) ||
                   (tracking.RightTracked && IsInReach(tracking.rightHand.position, button.position, button.rotation));
        }

        public static bool IsInReach(Vector3 hand, Vector3 button) => IsInReach(hand,button,Quaternion.identity);

        public static bool IsInReach(Vector3 hand, Vector3 button, Quaternion rotation)
        {
            Vector3 delta = Quaternion.Inverse(rotation)*(hand - button);
            return Mathf.Abs(delta.x) <= .075f && Mathf.Abs(delta.y) <= .09f &&
                   delta.z >= -.22f && delta.z <= .065f;
        }
    }
}
