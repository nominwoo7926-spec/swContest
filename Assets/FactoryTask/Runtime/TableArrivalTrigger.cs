using System.Collections.Generic;
using UnityEngine;

namespace FactoryTask
{
    // Third-person cue: each part that comes off the belt onto the pickup table is handed to the
    // spectator dummy, whatever then happens to it in the headset. Uses positions only, so it works
    // the same live and while replaying a recorded session.
    public sealed class TableArrivalTrigger : MonoBehaviour
    {
        public PartSpawner pool;
        public ConveyorController conveyor;
        public Collider pickupTable;
        public ThirdPersonAvatarDirector director;
        // Parts that have ridden the belt and not yet reached the table. Kept while a part crosses the
        // small gap between the end drum and the table, where it is over neither.
        readonly HashSet<ConveyorPart> fromBelt = new HashSet<ConveyorPart>();

        void Update()
        {
            if (pool == null || pool.Parts == null || director == null) return;
            foreach (var part in pool.Parts)
            {
                if (!part.gameObject.activeSelf) { fromBelt.Remove(part); continue; }
                Bounds b = part.Shape.bounds;
                if (OverBelt(b)) { fromBelt.Add(part); continue; }
                if (pickupTable != null && Over(b, pickupTable.bounds) && fromBelt.Remove(part)) director.QueueArrival(b.center);
            }
        }

        bool OverBelt(Bounds b)
        {
            if (conveyor == null || conveyor.driveSurfaces == null) return false;
            foreach (var s in conveyor.driveSurfaces) if (s != null && Over(b, s.bounds)) return true;
            return false;
        }

        static bool Over(Bounds part, Bounds surface)
        {
            Vector3 c = part.center;
            return c.x >= surface.min.x && c.x <= surface.max.x && c.z >= surface.min.z && c.z <= surface.max.z && part.min.y >= surface.max.y - .05f && part.min.y <= surface.max.y + .6f;
        }
    }
}
