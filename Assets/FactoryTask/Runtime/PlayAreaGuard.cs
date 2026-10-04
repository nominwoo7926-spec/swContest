using UnityEngine;

namespace FactoryTask
{
    // Keeps the tracked user from walking into (or onto) the conveyor, table, bin and workbench.
    // The head may lean leanAllowance into a footprint to reach parts; deeper than that the rig is
    // pushed back out, so walking further in just slides the world along with the user.
    [DefaultExecutionOrder(-140)]
    public sealed class PlayAreaGuard : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        // World-space XZ footprints (Y is ignored).
        public Bounds[] keepOut;
        public float leanAllowance = .35f;

        void LateUpdate()
        {
            if (tracking == null || keepOut == null || !tracking.HeadTracked || tracking.ExternalPlayback) return;
            Vector3 head = tracking.head.position, push = Vector3.zero;
            for (int i = 0; i < keepOut.Length; i++)
            {
                Bounds b = keepOut[i];
                float minX = b.min.x + leanAllowance, maxX = b.max.x - leanAllowance;
                float minZ = b.min.z + leanAllowance, maxZ = b.max.z - leanAllowance;
                if (minX >= maxX || minZ >= maxZ) continue;
                if (head.x <= minX || head.x >= maxX || head.z <= minZ || head.z >= maxZ) continue;
                // Leave through the nearest side of the allowed lean region.
                float left = head.x - minX, right = maxX - head.x, near = head.z - minZ, far = maxZ - head.z;
                float best = Mathf.Min(Mathf.Min(left, right), Mathf.Min(near, far));
                Vector3 step = best == left ? Vector3.left * left : best == right ? Vector3.right * right : best == near ? Vector3.back * near : Vector3.forward * far;
                push += step; head += step;
            }
            if (push != Vector3.zero) tracking.origin.position += push;
        }
    }
}
