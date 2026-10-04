using System.Collections.Generic;
using UnityEngine;

namespace FactoryTask
{
    public sealed class PickupQueue : MonoBehaviour
    {
        public Transform[] slots;
        public Transform[] approach;
        readonly List<ConveyorPart> entries=new List<ConveyorPart>();
        public int Count => entries.Count;
        public int Capacity => int.MaxValue;
        public ConveyorPart Front => Count > 0 ? entries[0] : null;
        public bool Full => false;
        void Awake() { Initialize(); }
        public void Initialize() { }
        public bool Enqueue(ConveyorPart part)
        {
            Initialize(); if (part == null || entries.Contains(part)) return false;
            entries.Add(part); return true;
        }
        public bool CanPick(ConveyorPart part) => part == Front && part.State == PartState.Waiting;
        public bool Remove(ConveyorPart part)
        {
            for (int i = 0; i < Count; i++) if (entries[i] == part)
            {
                entries.RemoveAt(i); return true;
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
                int rowWidth=Mathf.Max(1,slots.Length);
                int slotIndex=i%rowWidth,layer=i/rowWidth;
                Vector3 target = onApproach ? approach[part.RouteIndex].position : slots[slotIndex].position+Vector3.up*(layer*.24f);
                target += Vector3.up*(part.HalfHeight+.004f);
                Vector3 next = Vector3.MoveTowards(part.Body.position, target, metresPerSecond * deltaTime);
                bool reached = (next - target).sqrMagnitude < .00001f;
                if (onApproach && reached) part.RouteIndex++;
                part.SetQueuePosition(next, !onApproach && reached);
            }
        }
    }
}
