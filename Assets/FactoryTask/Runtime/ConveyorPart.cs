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
        const float PalmGap = .02f, SeatSeconds = .15f, MaxFollowSpeed = 8, MaxReleaseSpeed = 2.5f, MaxReleaseSpin = 8;
        public float weightKg = 1;
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
        public bool Free => State == PartState.Conveying || State == PartState.Waiting || State == PartState.Dropped;
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
        public void Supply(int kind, Vector3 position, Quaternion rotation)
        {
            Initialize();
            weightKg = kind == 0 ? 1 : kind == 1 ? 3 : 5;
            float size = .17f + kind * .035f;
            transform.localScale = Vector3.one * size;
            position += Vector3.up*(size*.5f+.004f);
            if (weightMaterials.Length > kind) bodyRenderer.sharedMaterial = weightMaterials[kind];
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            Body.position = position; Body.rotation = rotation; Body.mass = weightKg;
            SetPhysical(true); Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            State = PartState.Conveying; Holder = LastHand = HandSide.None;
            InsideSeconds = FloorSeconds = HoldSeconds = TravelMetres = Separation = 0; wasTracked = false;
        }
        void SetPhysical(bool value)
        {
            if (!value && !Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = !value; Body.useGravity = value; Shape.isTrigger = false;
        }
        public bool Grab(HandSide hand, Vector3 handPosition, Quaternion handRotation)
        {
            if (hand == HandSide.None || !Free) return false;
            Holder = LastHand = hand; State = PartState.Held;
            HoldSeconds = TravelMetres = InsideSeconds = FloorSeconds = Separation = 0; previousHand = handPosition; wasTracked = true;
            Body.useGravity = false;
            // Keep the part where it was grabbed, then seat its nearest face into the palm over a
            // short blend instead of snapping the part centre to the controller.
            Quaternion toHand = Quaternion.Inverse(handRotation);
            Vector3 seat = Body.position, closest = Shape.ClosestPoint(handPosition);
            Vector3 gap = handPosition - closest, inward = Body.position - closest;
            if (gap.sqrMagnitude > 1e-8f && inward.sqrMagnitude > 1e-8f) seat += gap + inward.normalized * PalmGap;
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
            Holder = HandSide.None; State = PartState.Dropped; InsideSeconds = FloorSeconds = Separation = 0;
            Body.useGravity = true;
            // Keep the hand's motion, but cap it so a tracking spike cannot launch the part.
            Body.linearVelocity = Vector3.ClampMagnitude(Body.linearVelocity, MaxReleaseSpeed);
            Body.angularVelocity = Vector3.ClampMagnitude(Body.angularVelocity, MaxReleaseSpin);
        }
        public void ReturnToPool()
        {
            Initialize(); SetPhysical(false); Holder = LastHand = HandSide.None;
            State = PartState.Pooled; InsideSeconds = FloorSeconds = HoldSeconds = TravelMetres = Separation = 0;
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
            weightKg=kind==0?1:kind==1?3:5;
            transform.localScale=Vector3.one*(.17f+kind*.035f);
            bodyRenderer.sharedMaterial=weightMaterials[kind];
            State=state;gameObject.SetActive(state!=PartState.Pooled);
            transform.SetPositionAndRotation(position,rotation);Body.position=position;Body.rotation=rotation;
        }
    }
}
