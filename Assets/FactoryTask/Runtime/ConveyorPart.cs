using UnityEngine;

namespace FactoryTask
{
    public enum HandSide { None, Left, Right }
    public enum PartState { Pooled, Conveying, Waiting, Held, Dropped }

    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ConveyorPart : MonoBehaviour
    {
        public float weightKg = 5;
        public Renderer bodyRenderer;
        public Material[] weightMaterials;
        public TextMesh weightLabel;
        public Rigidbody Body { get; private set; }
        public BoxCollider Shape { get; private set; }
        public PartState State { get; private set; }
        public HandSide Holder { get; private set; }
        public HandSide LastHand { get; private set; }
        public int RouteIndex { get; set; }
        public float InsideSeconds { get; set; }
        public float HoldSeconds { get; private set; }
        public float TravelMetres { get; private set; }
        public float HalfHeight => transform.localScale.y*.5f;
        public Vector3 GripLocalOffset => Vector3.forward*(HalfHeight+.015f);
        public Vector3 GripWorldPosition => Body.position-Body.rotation*GripLocalOffset;
        Vector3 previousHand;
        Quaternion previousRotation;
        bool wasTracked;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (Body != null) return;
            Body = GetComponent<Rigidbody>(); Shape = GetComponent<BoxCollider>();
            Body.mass = 1; // Task weights are estimator inputs, never simulated force feedback.
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.linearDamping = 1.3f; Body.angularDamping = 3;
            Body.maxDepenetrationVelocity = 1;
        }
        public void Supply(int kind, Vector3 position, Quaternion rotation)
        {
            Initialize(); gameObject.SetActive(true); SetKinematic(true);
            weightKg = kind == 0 ? 1 : kind == 1 ? 3 : 5;
            float size = .17f + kind * .035f;
            transform.localScale = Vector3.one * size;
            position += Vector3.up*(size*.5f+.004f);
            if (weightMaterials.Length > kind) bodyRenderer.sharedMaterial = weightMaterials[kind];
            SetWeightLabel(kind);
            transform.SetPositionAndRotation(position, rotation); Body.position = position; Body.rotation = rotation;
            State = PartState.Conveying; Holder = LastHand = HandSide.None;
            RouteIndex = 0; InsideSeconds = HoldSeconds = TravelMetres = 0; wasTracked = false;
        }
        void SetKinematic(bool value)
        {
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = value; Body.useGravity = !value;
            // Keep a solid collider during conveying/waiting so accumulated parts have real contact geometry.
            Shape.isTrigger = false;
        }
        public void SetQueuePosition(Vector3 position, bool atSlot)
        {
            if (State != PartState.Conveying && State != PartState.Waiting) return;
            Body.MovePosition(position); State = atSlot ? PartState.Waiting : PartState.Conveying;
        }
        public bool Grab(HandSide hand, Vector3 handPosition)
        {
            if (hand == HandSide.None || (State != PartState.Waiting && State != PartState.Dropped)) return false;
            SetKinematic(true); Holder = LastHand = hand; State = PartState.Held;
            HoldSeconds = TravelMetres = InsideSeconds = 0; previousHand = handPosition; wasTracked = true;
            return true;
        }
        public void Follow(Vector3 position, Quaternion rotation, float deltaTime, bool tracked)
        {
            if (State != PartState.Held) return;
            if (!tracked) { wasTracked = false; return; }
            HoldSeconds += deltaTime;
            if (wasTracked) TravelMetres += Mathf.Min(Vector3.Distance(position, previousHand), 2f * deltaTime);
            previousHand = position; wasTracked = true;
            previousRotation = rotation;
            // The palm grips the near face; placing the cube centre in the hand hides the fingers.
            Body.MovePosition(position+rotation*GripLocalOffset); Body.MoveRotation(rotation);
        }
        public void Release()
        {
            if (State != PartState.Held) return;
            Holder = HandSide.None; State = PartState.Dropped; InsideSeconds = 0;
            Body.position=previousHand+previousRotation*GripLocalOffset;Body.rotation=previousRotation;
            SetKinematic(false); // No throwing impulse: stable releases on Quest tracking loss/jitter.
        }
        public void ReturnToPool()
        {
            Initialize(); SetKinematic(true); Holder = LastHand = HandSide.None;
            State = PartState.Pooled; InsideSeconds = HoldSeconds = TravelMetres = 0;
            gameObject.SetActive(false);
        }
        public void Recover(Vector3 safePosition)
        {
            if (State != PartState.Dropped) return;
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            Body.position = safePosition; transform.position = safePosition;
        }
        public void ApplyRecordedState(PartState state,int kind,Vector3 position,Quaternion rotation)
        {
            Initialize();SetKinematic(true);Body.interpolation=RigidbodyInterpolation.None;
            weightKg=kind==0?1:kind==1?3:5;
            transform.localScale=Vector3.one*(.17f+kind*.035f);
            bodyRenderer.sharedMaterial=weightMaterials[kind];
            SetWeightLabel(kind);
            State=state;gameObject.SetActive(state!=PartState.Pooled);
            transform.SetPositionAndRotation(position,rotation);Body.position=position;Body.rotation=rotation;
        }
        void SetWeightLabel(int kind)
        {
            if(weightLabel==null)return;
            weightLabel.text=weightKg.ToString("0")+" kg";
            weightLabel.color=kind==1?new Color(.04f,.06f,.08f):Color.white;
        }
    }
}
