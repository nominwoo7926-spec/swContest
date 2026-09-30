using UnityEngine;

namespace ErgoContest
{
    public sealed class WorkerBodyVisualizer : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private ErgoLoadEstimator loadEstimator;
        [SerializeField] private Transform workerRoot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform pelvis;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform rightShoulder;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftForearm;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightForearm;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform leftShin;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform rightShin;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Renderer[] loadRenderers;
        [SerializeField] private Renderer[] neutralRenderers;
        [SerializeField] private float upperArmLength = 0.48f;
        [SerializeField] private float forearmLength = 0.50f;
        [SerializeField] private float armThickness = 0.105f;
        [SerializeField] private float thighLength = 0.48f;
        [SerializeField] private float shinLength = 0.46f;
        [SerializeField] private float poseSharpness = 12f;

        private readonly Vector3 rightShoulderLocal = new Vector3(0.26f, 1.32f, 0.02f);
        private readonly Vector3 leftShoulderLocal = new Vector3(-0.26f, 1.32f, 0.02f);
        private readonly Vector3 rightHipLocal = new Vector3(0.13f, 0.78f, 0.01f);
        private readonly Vector3 leftHipLocal = new Vector3(-0.13f, 0.78f, 0.01f);
        private readonly Vector3 rightFootLocal = new Vector3(0.13f, 0.04f, 0.04f);
        private readonly Vector3 leftFootLocal = new Vector3(-0.13f, 0.04f, 0.04f);
        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            ApplyColor(neutralRenderers, new Color(0.21f, 0.26f, 0.30f));
        }

        private void LateUpdate()
        {
            if (inputProvider == null)
                return;

            Transform frame = workerRoot != null ? workerRoot : transform;
            ErgoInputFrame input = inputProvider.CurrentFrame;
            Vector3 localHand = frame.InverseTransformPoint(input.HandPosition);
            float reachForward = Mathf.Clamp01((localHand.z + 0.05f) / 1.05f);
            float reachSide = Mathf.Clamp(localHand.x, -0.65f, 0.65f);
            float highReach = Mathf.Clamp01((localHand.y - 1.24f) / 0.62f);
            Quaternion bodyRotation = frame.rotation * Quaternion.Euler(
                -reachForward * 15f + highReach * 5f,
                reachSide * 8f,
                -reachSide * 10f);
            Vector3 pelvisPosition = frame.TransformPoint(new Vector3(reachSide * 0.035f, 0.78f - reachForward * 0.035f, reachForward * 0.055f));
            Vector3 chestPosition = frame.TransformPoint(new Vector3(reachSide * 0.055f, 1.08f - reachForward * 0.02f, reachForward * 0.12f));

            PoseRigid(pelvis, pelvisPosition, bodyRotation * Quaternion.Euler(0f, 0f, reachSide * 3f));
            PoseRigid(torso, chestPosition, bodyRotation);

            Vector3 shoulderOffset = chestPosition - frame.TransformPoint(new Vector3(0f, 1.08f, 0f));
            Vector3 rs = frame.TransformPoint(rightShoulderLocal) + shoulderOffset;
            Vector3 ls = frame.TransformPoint(leftShoulderLocal) + shoulderOffset;
            Vector3 rh = ClampReach(rs, input.HandPosition);
            Vector3 lh = Vector3.Lerp(frame.TransformPoint(new Vector3(-0.35f, 0.86f, -0.05f)), rh + frame.TransformVector(new Vector3(-0.18f, -0.04f, 0f)), input.HoldingPart ? 0.7f : 0.35f);
            Vector3 re = SolveElbow(rs, rh, true);
            Vector3 le = SolveElbow(ls, lh, false);

            PoseRigid(rightShoulder, rs, bodyRotation);
            PoseRigid(leftShoulder, ls, bodyRotation);
            PoseSegment(rightUpperArm, rs, re, armThickness);
            PoseSegment(rightForearm, re, rh, armThickness * 0.9f);
            PoseSegment(leftUpperArm, ls, le, armThickness);
            PoseSegment(leftForearm, le, lh, armThickness * 0.9f);
            PoseRigid(rightHand, rh, input.HandRotation);
            PoseRigid(leftHand, lh, Quaternion.LookRotation((lh - le).normalized, Vector3.up));

            PoseHead(frame, rh, chestPosition, bodyRotation);
            PoseLegs(frame, pelvisPosition, reachForward, reachSide);

            if (loadEstimator != null)
                ApplyColor(loadRenderers, loadEstimator.Current.DisplayColor);
        }

        private void PoseHead(Transform frame, Vector3 hand, Vector3 chestPosition, Quaternion bodyRotation)
        {
            Vector3 neckPosition = Vector3.Lerp(chestPosition, frame.TransformPoint(new Vector3(0f, 1.38f, 0.03f)), 0.65f);
            PoseRigid(neck, neckPosition, bodyRotation);
            if (head != null)
            {
                Vector3 headPosition = neckPosition + frame.TransformVector(new Vector3(0f, 0.20f, 0.03f));
                Vector3 direction = Vector3.Lerp(hand, frame.TransformPoint(new Vector3(0.25f, 1.35f, 0.45f)), 0.25f) - headPosition;
                Quaternion rotation = bodyRotation;
                if (direction.sqrMagnitude > 0.01f)
                    rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                PoseRigid(head, headPosition, rotation);
            }
        }

        private void PoseLegs(Transform frame, Vector3 pelvisPosition, float crouch, float sideReach)
        {
            Vector3 leftHip = pelvisPosition + frame.TransformVector(leftHipLocal - new Vector3(0f, 0.78f, 0f));
            Vector3 rightHip = pelvisPosition + frame.TransformVector(rightHipLocal - new Vector3(0f, 0.78f, 0f));
            Vector3 leftFootPosition = frame.TransformPoint(leftFootLocal + new Vector3(-sideReach * 0.025f, 0f, -crouch * 0.035f));
            Vector3 rightFootPosition = frame.TransformPoint(rightFootLocal + new Vector3(sideReach * 0.025f, 0f, crouch * 0.035f));
            Vector3 leftKnee = SolveKnee(leftHip, leftFootPosition, frame.forward, -1f);
            Vector3 rightKnee = SolveKnee(rightHip, rightFootPosition, frame.forward, 1f);

            PoseSegment(leftThigh, leftHip, leftKnee, 0.13f);
            PoseSegment(leftShin, leftKnee, leftFootPosition, 0.115f);
            PoseSegment(rightThigh, rightHip, rightKnee, 0.13f);
            PoseSegment(rightShin, rightKnee, rightFootPosition, 0.115f);
            PoseRigid(leftFoot, leftFootPosition, frame.rotation * Quaternion.Euler(88f, -4f, 0f));
            PoseRigid(rightFoot, rightFootPosition, frame.rotation * Quaternion.Euler(88f, 4f, 0f));
        }

        private Vector3 ClampReach(Vector3 shoulder, Vector3 hand)
        {
            Vector3 delta = hand - shoulder;
            float max = upperArmLength + forearmLength - 0.02f;
            return delta.magnitude <= max ? hand : shoulder + delta.normalized * max;
        }

        private Vector3 SolveElbow(Vector3 shoulder, Vector3 hand, bool right)
        {
            Vector3 delta = hand - shoulder;
            float distance = Mathf.Clamp(delta.magnitude, 0.08f, upperArmLength + forearmLength - 0.02f);
            Vector3 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward;
            float projection = (upperArmLength * upperArmLength - forearmLength * forearmLength + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, upperArmLength * upperArmLength - projection * projection));
            Vector3 side = workerRoot != null ? workerRoot.right : transform.right;
            Vector3 bendDirection = Vector3.Cross(direction, side * (right ? 1f : -1f));
            if (bendDirection.sqrMagnitude < 0.001f)
                bendDirection = Vector3.down;
            bendDirection = (bendDirection.normalized + Vector3.down * 0.45f).normalized;
            return shoulder + direction * projection + bendDirection * bend;
        }

        private Vector3 SolveKnee(Vector3 hip, Vector3 foot, Vector3 forward, float side)
        {
            Vector3 delta = foot - hip;
            float distance = Mathf.Clamp(delta.magnitude, 0.1f, thighLength + shinLength - 0.02f);
            Vector3 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.down;
            float projection = (thighLength * thighLength - shinLength * shinLength + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - projection * projection));
            Vector3 bendDirection = (forward * 0.8f + Vector3.right * side * 0.05f).normalized;
            return hip + direction * projection + bendDirection * bend;
        }

        private void PoseSegment(Transform segment, Vector3 start, Vector3 end, float thickness)
        {
            if (segment == null)
                return;
            Vector3 delta = end - start;
            if (delta.sqrMagnitude <= 0.0001f)
                return;
            segment.position = (start + end) * 0.5f;
            segment.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            segment.localScale = new Vector3(thickness, delta.magnitude * 0.5f, thickness);
        }

        private void PoseRigid(Transform target, Vector3 position, Quaternion rotation)
        {
            if (target == null)
                return;

            float t = 1f - Mathf.Exp(-poseSharpness * Time.deltaTime);
            target.position = Vector3.Lerp(target.position, position, t);
            target.rotation = Quaternion.Slerp(target.rotation, rotation, t);
        }

        private void ApplyColor(Renderer[] renderers, Color color)
        {
            if (renderers == null)
                return;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
