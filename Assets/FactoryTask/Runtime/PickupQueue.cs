using UnityEngine;

namespace FactoryTask
{
    public sealed class PickupQueue : MonoBehaviour
    {
        public Transform[] slots;
        public Transform[] approach;
        ConveyorPart[] entries;
        public int Count { get; private set; }
        public int Capacity => slots == null ? 0 : slots.Length;
        public ConveyorPart Front => Count > 0 ? entries[0] : null;
        public bool Full => Count >= Capacity;
        void Awake() { Initialize(); }
        public void Initialize() { if (entries == null) entries = new ConveyorPart[Capacity]; }
        public bool Enqueue(ConveyorPart part)
        {
            Initialize(); if (part == null || Full) return false;
            for (int i = 0; i < Count; i++) if (entries[i] == part) return false;
            entries[Count++] = part; return true;
        }
        public bool CanPick(ConveyorPart part) => part == Front && part.State == PartState.Waiting;
        public bool Remove(ConveyorPart part)
        {
            for (int i = 0; i < Count; i++) if (entries[i] == part)
            {
                for (int j = i; j < Count - 1; j++) entries[j] = entries[j + 1];
                entries[--Count] = null; return true;
            }
            return false;
        }
        public void Tick(float deltaTime, float metresPerSecond)
        {
            Initialize();
            for (int i = 0; i < Count; i++)
            {
                var part = entries[i];
                bool onApproach = part.RouteIndex < approach.Length;
                Vector3 target = onApproach ? approach[part.RouteIndex].position : slots[i].position;
                target += Vector3.up*(part.HalfHeight+.004f);
                Vector3 next = Vector3.MoveTowards(part.Body.position, target, metresPerSecond * deltaTime);
                bool reached = (next - target).sqrMagnitude < .00001f;
                if (onApproach && reached) part.RouteIndex++;
                part.SetQueuePosition(next, !onApproach && reached);
            }
        }
    }
}
