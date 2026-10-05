using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FactoryTask
{
    // Spectator-only "visual dummy" worker. The tracked avatar keeps driving RULA and the load
    // estimate in the background (hidden); this one gives the third-person view a clean version of
    // the task, independent of how messy the headset session is: every part that comes off the belt
    // onto the table is picked up there with both hands, carried over the bin rim and dropped into
    // the bin's near row. Arrivals during a sequence are queued; one sequence fits within the
    // measured interval between arrivals. While the line changes height the dummy steps back.
    //
    // Animator: Idle --(Trigger PickAndPlace)--> PickAndPlace --(Bool Performing false)--> Idle.
    // Both states play the standing idle clip; reach, lift, turn and place are humanoid IK aimed at
    // the real table and bin, so a raised or lowered table is followed automatically.
    [RequireComponent(typeof(Animator))]
    public sealed class ThirdPersonAvatarDirector : MonoBehaviour
    {
        public enum Stage { Idle, Reach, Carry, Lower, Return }
        static readonly int PickAndPlaceTrigger = Animator.StringToHash("PickAndPlace");
        static readonly int Performing = Animator.StringToHash("Performing");

        [Header("Scene")]
        public Collider pickupTable;
        public BoxCollider binVolume;
        [Tooltip("Top of the bin walls; parts are released just above it, never lowered past it.")]
        public float binRimHeight = 1.125f;
        [Tooltip("Top of the bin floor that dropped parts come to rest on.")]
        public float binFloorHeight = .81f;
        [Tooltip("Carried part shown between the dummy's hands (no physics); also the template for parts left in the bin.")]
        public GameObject visualPart;
        public float partSize = .24f;
        [Tooltip("Parts shown in the bin in the third-person view; the oldest fades out first.")]
        public int binShowCount = 2;

        [Header("Timing (scaled so one sequence fits the arrival interval)")]
        public float reachSeconds = .7f, carrySeconds = 1f, lowerSeconds = .45f, returnSeconds = .6f;
        [Tooltip("Arrivals waiting beyond this are skipped so the dummy never falls far behind.")]
        public int maxQueued = 2;

        [Header("Line height change")]
        public LineHeightAdjuster line;
        [Tooltip("How far the dummy steps back while the line is moving.")]
        public float stepBack = .5f;

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

        public bool isPerformingAction => stage != Stage.Idle;
        public Stage CurrentStage => stage;
        public int ShownInBin => shown.Count;
        // Seconds between parts arriving on the table, smoothed.
        public float Pace { get; private set; } = 2;
        public int Queued => arrivals.Count;

        Animator animator;
        Transform body, binVisuals;
        Quaternion baseRotation;
        Vector3 basePosition;
        Stage stage;
        float stageTime, handWeight, lastArrival = -1, retreat;
        bool pickedUp;
        int slot;
        readonly Queue<Vector3> arrivals = new Queue<Vector3>();
        Vector3 pickPoint, liftPoint, overBinPoint, releasePoint, partPoint;
        Vector3[] slotRest = new Vector3[2];
        readonly List<Placed> shown = new List<Placed>();
        Transform leftHand, rightHand, leftIndex, rightIndex, leftLittle, rightLittle, leftMiddle, rightMiddle, chest;

        sealed class Placed { public Transform part; public int slot; public float age, fade = -1; public Vector3 from; }

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            body = transform.parent != null ? transform.parent : transform;
            baseRotation = body.rotation; basePosition = body.position;
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal); rightIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            leftLittle = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal); rightLittle = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            leftMiddle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal); rightMiddle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (visualPart != null) visualPart.SetActive(false);
            binVisuals = new GameObject("Dummy_Bin_Parts").transform;
            // Rim and floor heights relative to the bin volume, so they follow a raised or lowered line.
            if (binVolume != null) { rimAboveVolume = binRimHeight - binVolume.bounds.min.y; floorAboveVolume = binFloorHeight - binVolume.bounds.min.y; }
        }
        float rimAboveVolume, floorAboveVolume;
        float RimHeight => binVolume != null ? binVolume.bounds.min.y + rimAboveVolume : binRimHeight;
        float FloorHeight => binVolume != null ? binVolume.bounds.min.y + floorAboveVolume : binFloorHeight;

        // A part came off the belt onto the table at this position: pick it up (now, or after the
        // sequences already queued).
        public void QueueArrival(Vector3 partCentre)
        {
            float now = Time.time;
            if (lastArrival >= 0) Pace = Mathf.Lerp(Pace, Mathf.Clamp(now - lastArrival, 1f, 10f), .4f);
            lastArrival = now;
            if (!isActiveAndEnabled) return;
            if (arrivals.Count >= maxQueued) arrivals.Dequeue();
            arrivals.Enqueue(partCentre);
        }

        // Starts one sequence on a part at the front edge of the table (Animation-Event style entry).
        public void TriggerPickAndPlaceSequence()
        {
            if (pickupTable == null) return;
            Bounds t = pickupTable.bounds;
            QueueArrival(new Vector3(t.min.x + partSize, t.max.y + partSize * .5f, t.min.z + partSize));
        }

        // A new work run: clear the parts shown in the bin and return to idle.
        public void ResetBin()
        {
            foreach (var p in shown) if (p.part != null) Destroy(p.part.gameObject);
            shown.Clear(); arrivals.Clear(); lastArrival = -1;
            if (visualPart != null) visualPart.SetActive(false);
            if (stage != Stage.Idle) { stage = Stage.Idle; animator.SetBool(Performing, false); }
        }

        void Begin(Vector3 arrival)
        {
            stage = Stage.Reach; stageTime = 0; pickedUp = false;
            // Make room before this part arrives: the oldest of a full bin starts fading now.
            int visible = 0; foreach (var p in shown) if (p.fade < 0) visible++;
            if (visible >= binShowCount) foreach (var p in shown) if (p.fade < 0) { p.fade = 0; break; }
            slot = FreeSlot();
            ComputeTargets(arrival);
            animator.SetBool(Performing, true);
            animator.SetTrigger(PickAndPlaceTrigger);
        }

        int FreeSlot()
        {
            for (int s = 0; s < 2; s++)
            {
                bool used = false;
                foreach (var p in shown) if (p.slot == s && p.fade < 0) used = true;
                if (!used) return s;
            }
            return 0;
        }

        // Pick where the part arrived (kept on the table top); place in the bin's near row, two side by side.
        void ComputeTargets(Vector3 arrival)
        {
            float half = partSize * .5f;
            if (pickupTable != null)
            {
                Bounds t = pickupTable.bounds;
                pickPoint = new Vector3(Mathf.Clamp(arrival.x, t.min.x + half, t.max.x - half), t.max.y + half + .002f, Mathf.Clamp(arrival.z, t.min.z + half, t.max.z - half));
            }
            if (binVolume != null)
            {
                Bounds b = binVolume.bounds;
                float z = b.min.z + half + .01f, x = half + .012f;
                slotRest[0] = new Vector3(b.center.x - x, FloorHeight + half, z);
                slotRest[1] = new Vector3(b.center.x + x, FloorHeight + half, z);
            }
            Vector3 target = slotRest[slot];
            // Released with the part's bottom just over the rim, so the forearms never cross the walls.
            releasePoint = new Vector3(target.x, RimHeight + half + .02f, target.z);
            float clear = Mathf.Max(RimHeight, pickPoint.y + half) + half + .12f;
            liftPoint = new Vector3(pickPoint.x, clear, pickPoint.z);
            overBinPoint = new Vector3(target.x, clear, target.z);
        }

        // One whole sequence takes about 90 % of the arrival interval.
        float Scale => Mathf.Clamp(Pace * .9f / (reachSeconds + carrySeconds + lowerSeconds + returnSeconds), .45f, 1.5f);

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateBinParts(dt);
            // Step back while the line is raised or lowered, return once it has stopped.
            bool lineMoving = line != null && line.Moving;
            retreat = Mathf.MoveTowards(retreat, lineMoving ? stepBack : 0, dt * .6f);
            Vector3 back = Vector3.ProjectOnPlane(baseRotation * Vector3.forward, Vector3.up).normalized;
            body.position = basePosition - back * retreat;
            if (stage == Stage.Idle)
            {
                Relax(dt);
                if (arrivals.Count > 0 && !lineMoving && retreat <= .001f) Begin(arrivals.Dequeue());
                return;
            }
            stageTime += dt;
            float pickYaw = YawTo(pickPoint), placeYaw = YawTo(releasePoint);
            switch (stage)
            {
                case Stage.Reach:
                {
                    float u = stageTime / (reachSeconds * Scale);
                    handWeight = Smooth(u); partPoint = pickPoint; SetYaw(Mathf.Lerp(0, pickYaw, Smooth(u)));
                    if (u >= 1) { OnPickUpEvent(); Next(Stage.Carry); }
                    break;
                }
                case Stage.Carry:
                {
                    float u = stageTime / (carrySeconds * Scale);
                    handWeight = 1;
                    // Straight up off the table, then across to above the bin.
                    partPoint = u < .35f ? Vector3.Lerp(pickPoint, liftPoint, Smooth(u / .35f)) : Vector3.Lerp(liftPoint, overBinPoint, Smooth((u - .35f) / .65f));
                    SetYaw(Mathf.Lerp(pickYaw, placeYaw, Smooth((u - .2f) / .8f)));
                    if (u >= 1) Next(Stage.Lower);
                    break;
                }
                case Stage.Lower:
                {
                    float u = stageTime / (lowerSeconds * Scale);
                    partPoint = Vector3.Lerp(overBinPoint, releasePoint, Smooth(u)); SetYaw(placeYaw);
                    if (u >= 1) { OnPlaceEvent(); Next(Stage.Return); }
                    break;
                }
                case Stage.Return:
                {
                    float u = stageTime / (returnSeconds * Scale);
                    // Hands come up and away from the bin first, then the body turns back.
                    handWeight = 1 - Smooth(u / .7f); partPoint = releasePoint + Vector3.up * .08f * Smooth(u);
                    SetYaw(Mathf.Lerp(placeYaw, 0, Smooth(u)));
                    if (u >= 1)
                    {
                        stage = Stage.Idle; animator.SetBool(Performing, false);
                    }
                    break;
                }
            }
            partPoint = Reachable(partPoint);
            if (visualPart != null && visualPart.activeSelf)
                visualPart.transform.SetPositionAndRotation(partPoint, Quaternion.Euler(0, body.eulerAngles.y, 0));
        }

        void Next(Stage next) { stage = next; stageTime = 0; }
        void SetYaw(float yaw) { body.rotation = baseRotation * Quaternion.Euler(0, yaw, 0); }

        void Relax(float dt)
        {
            handWeight = Mathf.MoveTowards(handWeight, 0, dt * 2);
            body.rotation = Quaternion.RotateTowards(body.rotation, baseRotation, 90 * dt);
        }

        // Parts left in the bin drop the last few centimetres into their slot, then the oldest fades.
        void UpdateBinParts(float dt)
        {
            for (int i = shown.Count - 1; i >= 0; i--)
            {
                var p = shown[i]; p.age += dt;
                float fall = Mathf.Clamp01(p.age / .28f);
                p.part.position = Vector3.Lerp(p.from, slotRest[p.slot], fall * fall);
                if (p.fade < 0) continue;
                p.fade += dt;
                float k = 1 - Mathf.SmoothStep(0, 1, p.fade / .6f);
                p.part.localScale = Vector3.one * partSize * k;
                if (k <= .02f) { Destroy(p.part.gameObject); shown.RemoveAt(i); }
            }
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

        float YawTo(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(baseRotation) * (point - body.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            return Mathf.Clamp(yaw, -maxTurnDegrees, maxTurnDegrees);
        }

        static float Smooth(float x) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(x));

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

        // Animation Event hook (also fired by the sequence): the part appears in the hands.
        public void OnPickUpEvent()
        {
            if (pickedUp) return;
            pickedUp = true;
            if (visualPart != null) { visualPart.transform.position = pickPoint; visualPart.SetActive(true); }
            onPickedUp.Invoke();
        }

        // Animation Event hook (also fired by the sequence): the part is released into the bin.
        public void OnPlaceEvent()
        {
            if (visualPart == null || !visualPart.activeSelf) return;
            visualPart.SetActive(false);
            var copy = Instantiate(visualPart, partPoint, Quaternion.Euler(0, baseRotation.eulerAngles.y, 0), binVisuals);
            copy.name = "Dummy_Bin_Part"; copy.SetActive(true);
            shown.Add(new Placed { part = copy.transform, slot = slot, from = partPoint });
            onPlacedInBin.Invoke();
        }
    }
}
