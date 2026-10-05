using UnityEngine;

namespace FactoryTask
{
    public enum HandSide { None, Left, Right }
    // Waiting is retained only so older recordings keep their numeric state values.
    public enum PartState { Pooled, Conveying, Waiting, Held, Dropped }

    // Every active part is an ordinary dynamic rigidbody: the belt drives it with friction-like
    // acceleration, parts collide and stack with each other, and a held part is pulled toward the
    // hand by velocity so it still collides instead of tunnelling through tables or other parts.
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ConveyorPart : MonoBehaviour
    {
        const float PalmGap = .008f, SeatSeconds = .15f, MaxFollowSpeed = 8, MaxReleaseSpeed = 2.5f, MaxReleaseSpin = 8;
        public float weightKg = 1;
        // Held parts and parts in the bin render only in the headset: the spectator dummy carries and
        // places its own copies, so the real ones would otherwise float or pile up next to them.
        public int heldLayer = 28;
        int freeLayer = -1;
        public Renderer bodyRenderer;
        public Material[] weightMaterials;
        public Rigidbody Body { get; private set; }
        public BoxCollider Shape { get; private set; }
        public PartState State { get; private set; }
        public HandSide Holder { get; private set; }
        public HandSide LastHand { get; private set; }
        public float InsideSeconds { get; set; }
        public float FloorSeconds { get; set; }
        public float HoldSeconds { get; private set; }
        public float TravelMetres { get; private set; }
        // Distance between where the hand wants the part and where physics allowed it to be.
        public float Separation { get; private set; }
        public float HalfHeight => transform.localScale.y*.5f;
        // Counted parts rest in the bin until it is full; they are no longer picked up again.
        public bool Completed { get; private set; }
        public bool Vanishing => vanishTime >= 0;
        public bool Free => !Completed && !Vanishing && (State == PartState.Conveying || State == PartState.Waiting || State == PartState.Dropped);
        const float VanishSeconds = .8f;
        float vanishTime = -1, fullSize;
        Vector3 previousHand, attachStart, attachEnd;
        Quaternion attachRotation;
        float attachBlend;
        bool wasTracked;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (Body != null) return;
            Body = GetComponent<Rigidbody>(); Shape = GetComponent<BoxCollider>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.linearDamping = .05f; Body.angularDamping = .4f;
            Body.maxAngularVelocity = 40; Body.maxDepenetrationVelocity = 1.5f;
            // Extra iterations keep accumulated rows and stacks of parts from sinking into each other.
            Body.solverIterations = 16; Body.solverVelocityIterations = 4;
            Shape.isTrigger = false;
        }
        // Order in which the part was supplied during the current run (0-based).
        public int SpawnIndex { get; set; }
        // The last part of a run's quota, shown in the final-part colour (recorded as kind 3).
        public bool IsFinalPart { get; private set; }
        public int RecordedKind => IsFinalPart ? 3 : weightKg <= 1 ? 0 : weightKg <= 3 ? 1 : 2;
        public void Supply(int kind, Vector3 position, Quaternion rotation, Material overrideMaterial = null)
        {
            Initialize();
            weightKg = kind == 0 ? 1 : kind == 1 ? 3 : 5;
            float size = .17f + kind * .035f;
            transform.localScale = Vector3.one * size;
            position += Vector3.up*(size*.5f+.004f);
            IsFinalPart = overrideMaterial != null;
            if (overrideMaterial != null) bodyRenderer.sharedMaterial = overrideMaterial;
            else if (weightMaterials.Length > kind) bodyRenderer.sharedMaterial = weightMaterials[kind];
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            Body.position = position; Body.rotation = rotation; Body.mass = weightKg;
            SetPhysical(true); Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            State = PartState.Conveying; Holder = LastHand = HandSide.None;
            InsideSeconds = FloorSeconds = HoldSeconds = TravelMetres = Separation = 0; wasTracked = false;
            Completed = false; vanishTime = -1; fullSize = size; CarriedWithBothHands = false;
        }
        public void MarkCompleted() { Completed = true; }
        public void SetSpectatorHidden(bool hidden)
        {
            if (freeLayer < 0) freeLayer = gameObject.layer;
            int layer = hidden ? heldLayer : freeLayer;
            if (gameObject.layer == layer) return;
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
        public void BeginVanish() { if (!Vanishing) vanishTime = 0; }
        // Shrinks away while staying physical, so gravity keeps it settled and parts resting on it
        // ease down as it goes.
        // Returns true once the part has been returned to the pool.
        public bool TickVanish(float deltaTime)
        {
            if (!Vanishing) return false;
            vanishTime += deltaTime;
            float k = 1 - Mathf.SmoothStep(0, 1, vanishTime / VanishSeconds);
            if (k <= .02f) { ReturnToPool(); return true; }
            transform.localScale = Vector3.one * fullSize * k;
            return false;
        }
        void SetPhysical(bool value)
        {
            if (!value && !Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = !value; Body.useGravity = value; Shape.isTrigger = false;
        }
        // Set by a two-handed pick-up; kept after release so completion credits both arms.
        public bool CarriedWithBothHands { get; private set; }
        public bool Grab(HandSide hand, Vector3 handPosition, Quaternion handRotation, bool bothHands = false)
        {
            if (hand == HandSide.None || !Free) return false;
            Holder = LastHand = hand; State = PartState.Held; SetSpectatorHidden(true); CarriedWithBothHands = bothHands;
            HoldSeconds = TravelMetres = InsideSeconds = FloorSeconds = Separation = 0; previousHand = handPosition; wasTracked = true;
            Body.useGravity = false;
            // Keep the part where it was grabbed, then seat its nearest face into the palm over a
            // short blend instead of snapping the part centre to the controller.
            Quaternion toHand = Quaternion.Inverse(handRotation);
            Vector3 seat = Body.position, closest = Shape.ClosestPoint(handPosition);
            Vector3 gap = handPosition - closest, inward = Body.position - closest;
            // Held between two palms the part already sits where the hands closed on it.
            if (!bothHands && gap.sqrMagnitude > 1e-8f && inward.sqrMagnitude > 1e-8f) seat += gap + inward.normalized * PalmGap;
            attachStart = toHand * (Body.position - handPosition);
            attachEnd = toHand * (seat - handPosition);
            attachRotation = toHand * Body.rotation; attachBlend = 0;
            return true;
        }
        public void Follow(Vector3 position, Quaternion rotation, float deltaTime, bool tracked)
        {
            if (State != PartState.Held) return;
            if (!tracked || deltaTime <= 0)
            {
                // Tracking loss: hold the part still in the air until the controller returns.
                wasTracked = false; Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; return;
            }
            HoldSeconds += deltaTime;
            if (wasTracked) TravelMetres += Mathf.Min(Vector3.Distance(position, previousHand), 2f * deltaTime);
            previousHand = position; wasTracked = true;
            attachBlend = Mathf.MoveTowards(attachBlend, 1, deltaTime / SeatSeconds);
            Vector3 target = position + rotation * Vector3.Lerp(attachStart, attachEnd, Mathf.SmoothStep(0, 1, attachBlend));
            Quaternion targetRotation = rotation * attachRotation;
            Separation = Vector3.Distance(target, Body.position);
            Body.linearVelocity = Vector3.ClampMagnitude((target - Body.position) / deltaTime, MaxFollowSpeed);
            (targetRotation * Quaternion.Inverse(Body.rotation)).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            Body.angularVelocity = float.IsFinite(axis.x) && Mathf.Abs(angle) > .01f ? axis * (angle * Mathf.Deg2Rad / deltaTime) : Vector3.zero;
        }
        public void Release()
        {
            if (State != PartState.Held) return;
            Holder = HandSide.None; State = PartState.Dropped; InsideSeconds = FloorSeconds = Separation = 0; SetSpectatorHidden(false);
            Body.useGravity = true;
            // Keep the hand's motion, but cap it so a tracking spike cannot launch the part.
            Body.linearVelocity = Vector3.ClampMagnitude(Body.linearVelocity, MaxReleaseSpeed);
            Body.angularVelocity = Vector3.ClampMagnitude(Body.angularVelocity, MaxReleaseSpin);
        }
        public void ReturnToPool()
        {
            Initialize(); SetPhysical(false); Holder = LastHand = HandSide.None;
            State = PartState.Pooled; InsideSeconds = FloorSeconds = HoldSeconds = TravelMetres = Separation = 0;
            Completed = false; vanishTime = -1; SetSpectatorHidden(false);
            gameObject.SetActive(false);
        }
        public void Recover(Vector3 safePosition)
        {
            if (!Free || Body.isKinematic) return;
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            Body.position = safePosition; transform.position = safePosition;
        }
        public void ApplyRecordedState(PartState state,int kind,Vector3 position,Quaternion rotation)
        {
            Initialize();SetPhysical(false);Body.interpolation=RigidbodyInterpolation.None;
            // Kind 3 is the final part of a quota: the 5 kg size in the final-part colour.
            IsFinalPart=kind==3;int size=Mathf.Min(kind,2);
            weightKg=size==0?1:size==1?3:5;
            transform.localScale=Vector3.one*(.17f+size*.035f);
            bodyRenderer.sharedMaterial=weightMaterials[Mathf.Min(kind,weightMaterials.Length-1)];
            State=state;gameObject.SetActive(state!=PartState.Pooled);SetSpectatorHidden(state==PartState.Held);
            transform.SetPositionAndRotation(position,rotation);Body.position=position;Body.rotation=rotation;
        }
    }
}
