using UnityEngine;
using UnityEngine.Events;

namespace FactoryTask
{
    // Spectator-only "visual dummy" worker. The tracked avatar keeps driving RULA and the load
    // estimate in the background (hidden); this one plays a clean, two-handed standing
    // pick-and-place every time the VR user picks up a part.
    //
    // Animator: Idle --(Trigger PickAndPlace)--> PickAndPlace --(exit time)--> Idle. Both states play
    // the standing idle clip; the reach, lift, turn and place come from humanoid IK aimed at the real
    // pickup table and bin, so a raised or lowered table is followed automatically.
    // One sequence lasts one part-arrival interval, which stretches when the belt slows down.
    [RequireComponent(typeof(Animator))]
    public sealed class ThirdPersonAvatarDirector : MonoBehaviour
    {
        static readonly int PickAndPlaceTrigger = Animator.StringToHash("PickAndPlace");
        static readonly int ActionSpeed = Animator.StringToHash("ActionSpeed");

        [Header("Scene")]
        public Collider pickupTable;
        public BoxCollider binVolume;
        [Tooltip("Top of the bin walls; the part is carried over it, then lowered straight in.")]
        public float binRimHeight = 1.125f;
        public PartSpawner spawner;
        public ConveyorController conveyor;
        [Tooltip("Carried part shown between the dummy's hands (no physics).")]
        public GameObject visualPart;
        public float partSize = .24f;

        [Header("Sequence (normalized time)")]
        [Range(0, 1)] public float pickUpAt = .26f;
        [Range(0, 1)] public float placeAt = .78f;
        public float minSeconds = 2.5f, maxSeconds = 8f;

        [Header("Pose")]
        public float maxTurnDegrees = 35;
        [Range(0, 1)] public float leanWeight = .4f;
        [Tooltip("Wrist to palm centre along the fingers, and palm surface out from the bone line.")]
        public float palmForward = .06f, palmDepth = .025f;
        [Tooltip("Farthest the carried part's centre may be from the chest; keeps both hands on it.")]
        public float maxReach = .58f;

        [Header("Events")]
        public UnityEvent onPickedUp = new UnityEvent();
        public UnityEvent onPlacedInBin = new UnityEvent();

        public bool isPerformingAction { get; private set; }
        public float SequenceSeconds { get; private set; }
        public float Progress => isPerformingAction ? Mathf.Clamp01(elapsed / SequenceSeconds) : 0;

        Animator animator;
        Transform body;
        Quaternion baseRotation;
        float elapsed, clipLength = 1, handWeight;
        bool pickedUp, placed;
        Vector3 pickPoint, liftPoint, overBinPoint, placePoint, partPoint;
        Transform leftHand, rightHand, leftIndex, rightIndex, leftLittle, rightLittle, leftMiddle, rightMiddle, chest;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            body = transform.parent != null ? transform.parent : transform;
            baseRotation = body.rotation;
            var clips = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : null;
            if (clips != null && clips.Length > 0) clipLength = Mathf.Max(.1f, clips[0].length);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal); rightIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            leftLittle = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal); rightLittle = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            leftMiddle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal); rightMiddle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (visualPart != null) visualPart.SetActive(false);
        }

        // Call when the VR user picks up a part. Ignored while a sequence is still running.
        public void TriggerPickAndPlaceSequence()
        {
            if (isPerformingAction || !isActiveAndEnabled) return;
            isPerformingAction = true; pickedUp = placed = false; elapsed = 0;
            SequenceSeconds = Mathf.Clamp(ArrivalInterval(), minSeconds, maxSeconds);
            ComputeTargets();
            // The PickAndPlace state lasts exactly one sequence: clip length / speed.
            animator.SetFloat(ActionSpeed, clipLength / SequenceSeconds);
            animator.SetTrigger(PickAndPlaceTrigger);
        }

        // Seconds between parts reaching the table at the current belt speed.
        float ArrivalInterval()
        {
            if (spawner == null) return 3.5f;
            float speed = conveyor != null ? Mathf.Max(.05f, conveyor.CurrentSpeed) : ConveyorController.FixedSpeed;
            return spawner.intervalSeconds * ConveyorController.FixedSpeed / speed;
        }

        // Pick from the table corner nearest the worker; place in the bin just past its near wall.
        void ComputeTargets()
        {
            float half = partSize * .5f;
            if (pickupTable != null)
            {
                Bounds t = pickupTable.bounds;
                pickPoint = new Vector3(t.min.x + half + .04f, t.max.y + half + .002f, t.min.z + half + .05f);
            }
            if (binVolume != null)
            {
                Bounds b = binVolume.bounds;
                // Just past the bin's near wall, where the worker can lower it in with straight arms.
                placePoint = new Vector3(b.center.x, b.min.y + half + .03f, b.min.z + half + .04f);
            }
            float clear = Mathf.Max(binRimHeight, pickPoint.y + half) + half + .04f;
            liftPoint = new Vector3(pickPoint.x, clear, pickPoint.z);
            overBinPoint = new Vector3(placePoint.x, clear, placePoint.z);
        }

        // Carry the part in front of the chest when the path would take it out of arm's reach.
        Vector3 Reachable(Vector3 point)
        {
            if (chest == null) return point;
            Vector3 from = chest.position, offset = point - from;
            Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            // Never pull the part back into the torso: keep it at least a forearm in front.
            float ahead = Vector3.Dot(offset, forward);
            if (ahead < .28f) offset += forward * (.28f - ahead);
            return from + Vector3.ClampMagnitude(offset, maxReach);
        }

        void Update()
        {
            if (!isPerformingAction) { Relax(Time.deltaTime); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / SequenceSeconds);
            if (!pickedUp && t >= pickUpAt) OnPickUpEvent();
            if (!placed && t >= placeAt) OnPlaceEvent();
            if (t >= 1) { isPerformingAction = false; return; }

            partPoint = Reachable(PartPosition(t));
            if (visualPart != null && visualPart.activeSelf)
                visualPart.transform.SetPositionAndRotation(partPoint, Quaternion.Euler(0, body.eulerAngles.y, 0));

            // Face the table to pick, the bin to place, then the line again.
            float pickYaw = YawTo(pickPoint), placeYaw = YawTo(placePoint);
            float carry = Phase(t, pickUpAt + .06f, placeAt - .12f);
            float yaw = t < pickUpAt ? Mathf.Lerp(0, pickYaw, Phase(t, 0, pickUpAt))
                      : t < placeAt ? Mathf.Lerp(pickYaw, placeYaw, carry)
                      : Mathf.Lerp(placeYaw, 0, Phase(t, placeAt + .05f, 1));
            body.rotation = baseRotation * Quaternion.Euler(0, yaw, 0);

            // Reach in, hold firmly while carrying, then ease the hands back out after letting go.
            handWeight = t < pickUpAt ? Phase(t, .02f, pickUpAt) : t < placeAt ? 1 : 1 - Phase(t, placeAt + .02f, .97f);
        }

        void Relax(float dt)
        {
            handWeight = Mathf.MoveTowards(handWeight, 0, dt * 2);
            body.rotation = Quaternion.RotateTowards(body.rotation, baseRotation, 90 * dt);
        }

        // Straight up off the table, across above the bin rim, then straight down into the bin.
        Vector3 PartPosition(float t)
        {
            if (t < pickUpAt) return pickPoint;
            if (t >= placeAt) return placePoint;
            float u = (t - pickUpAt) / (placeAt - pickUpAt);
            if (u < .25f) return Vector3.Lerp(pickPoint, liftPoint, Smooth(u / .25f));
            if (u < .65f) return Vector3.Lerp(liftPoint, overBinPoint, Smooth((u - .25f) / .4f));
            return Vector3.Lerp(overBinPoint, placePoint, Smooth((u - .65f) / .35f));
        }

        float YawTo(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(baseRotation) * (point - body.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Clamp(yaw, -maxTurnDegrees, maxTurnDegrees);
        }

        static float Smooth(float x) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(x));
        static float Phase(float t, float from, float to) => Smooth((t - from) / Mathf.Max(.0001f, to - from));

        void OnAnimatorIK(int layerIndex)
        {
            if (handWeight <= .001f)
            {
                foreach (var goal in new[] { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand })
                { animator.SetIKPositionWeight(goal, 0); animator.SetIKRotationWeight(goal, 0); }
                animator.SetLookAtWeight(0);
                return;
            }
            // Fingers point forward and a little down along the part's sides; palms face its centre.
            Vector3 fingers = (body.forward * .85f - Vector3.up * .5f).normalized;
            Hand(AvatarIKGoal.LeftHand, leftHand, leftMiddle, leftIndex, leftLittle, true, body.right, fingers);
            Hand(AvatarIKGoal.RightHand, rightHand, rightMiddle, rightIndex, rightLittle, false, -body.right, fingers);
            // Looking at the part with some body weight bends the spine toward it (a natural lean).
            animator.SetLookAtWeight(handWeight, leanWeight, .8f, 0, .6f);
            animator.SetLookAtPosition(partPoint);
        }

        void Hand(AvatarIKGoal goal, Transform hand, Transform middle, Transform index, Transform little, bool left, Vector3 palmToward, Vector3 fingers)
        {
            if (hand == null || middle == null || index == null || little == null) return;
            // Current (animated) finger direction and palm normal, measured from the bones.
            Vector3 along = (middle.position - hand.position).normalized;
            Vector3 palm = Vector3.Cross(along, index.position - little.position).normalized * (left ? -1 : 1);
            Vector3 aimFingers = Vector3.ProjectOnPlane(fingers, palmToward).normalized;
            Quaternion delta = Quaternion.LookRotation(aimFingers, palmToward) * Quaternion.Inverse(Quaternion.LookRotation(along, palm));
            // Palm surface rests on the part's side face, so the hand never sinks into it.
            Vector3 face = partPoint - palmToward * (partSize * .5f + .005f);
            Vector3 wrist = face - aimFingers * palmForward - palmToward * palmDepth;
            animator.SetIKPositionWeight(goal, handWeight); animator.SetIKRotationWeight(goal, handWeight);
            animator.SetIKPosition(goal, wrist);
            animator.SetIKRotation(goal, delta * animator.GetIKRotation(goal));
        }

        // Animation Event hook (also fired by the sequence timer): the part appears in the hands.
        public void OnPickUpEvent()
        {
            if (pickedUp) return;
            pickedUp = true;
            if (visualPart != null) { visualPart.transform.position = pickPoint; visualPart.SetActive(true); }
            onPickedUp.Invoke();
        }

        // Animation Event hook (also fired by the sequence timer): the part is released into the bin.
        public void OnPlaceEvent()
        {
            if (placed) return;
            placed = true;
            if (visualPart != null) visualPart.SetActive(false);
            onPlacedInBin.Invoke();
        }
    }
}
