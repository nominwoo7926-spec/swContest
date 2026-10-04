using UnityEngine;

namespace FactoryTask
{
    // First-person hand: Meta's skinned human hand mesh on the controller grip pose.
    // Setup aligns the mesh (palm toward the handle, fingers forward, thumb up) and stores, per finger
    // joint, the axis that bends it toward the palm. Here each joint is set from its rest pose:
    // relaxed when idle, wrapped around a held part, or closed around the handle on an empty grip.
    [DefaultExecutionOrder(150)]
    public sealed class RealisticHandVisual : MonoBehaviour
    {
        public HandSide side;
        public XRTrackingProvider tracking;
        public XRGrabTaskTracker task;
        public Transform[] joints;
        public Vector3[] axes;
        // 1-3 = proximal, middle, distal joint of a finger; thumb joints are flagged separately.
        public int[] segment;
        public bool[] thumb;
        static readonly float[] Relaxed = { 0, 10, 12, 8 }, Fist = { 0, 62, 85, 55 }, Hold = { 0, 28, 34, 22 };
        Quaternion[] rest;
        float fist, hold;

        void Awake()
        {
            rest = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) rest[i] = joints[i].localRotation;
        }
        void LateUpdate()
        {
            if (tracking == null || joints == null) return;
            bool grip = side == HandSide.Left ? tracking.LeftGrip : tracking.RightGrip;
            bool holding = task != null && (task.enabled ? task.Held(side) != null : grip && task.HeldWeight > 0);
            float k = 1 - Mathf.Exp(-Time.deltaTime * 16);
            fist = Mathf.Lerp(fist, grip && !holding ? 1 : 0, k);
            hold = Mathf.Lerp(hold, holding ? 1 : 0, k);
            for (int i = 0; i < joints.Length; i++)
            {
                int s = segment[i];
                float angle = Relaxed[s] + fist * (Fist[s] - Relaxed[s]) + hold * (Hold[s] - Relaxed[s]);
                if (thumb[i]) angle *= .8f;
                joints[i].localRotation = rest[i] * Quaternion.AngleAxis(angle, axes[i]);
            }
        }
    }
}
