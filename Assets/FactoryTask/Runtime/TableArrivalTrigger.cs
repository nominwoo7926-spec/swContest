using System.Collections.Generic;
using UnityEngine;

namespace FactoryTask
{
    // Third-person cue: every part that comes off the belt is handed to the spectator dummy exactly
    // once, whatever then happens to it in the headset. A part counts as delivered when it reaches the
    // pickup table or when the worker takes it, whichever comes first - workers often grab parts at the
    // very end of the belt, before they are over the table. A part that was already delivered never
    // triggers again (dropped, thrown past the table, put back), so the dummy neither skips parts nor
    // picks up ones that fell under the table. Uses positions and part states only, so it works the
    // same live and while replaying a recorded session.
    public sealed class TableArrivalTrigger : MonoBehaviour
    {
        public PartSpawner pool;
        public ConveyorController conveyor;
        public Collider pickupTable;
        public ThirdPersonAvatarDirector director;
        // Parts that have ridden the belt and not yet been delivered. Kept while a part crosses the
        // small gap between the end drum and the table, where it is over neither.
        readonly HashSet<ConveyorPart> fromBelt = new HashSet<ConveyorPart>();
        // Parts already handed to the dummy during their current supply.
        readonly HashSet<ConveyorPart> delivered = new HashSet<ConveyorPart>();

        void Update()
        {
            if (pool == null || pool.Parts == null || director == null) return;
            foreach (var part in pool.Parts)
            {
                // Returned to the pool: the next supply of this part is a new delivery.
                if (!part.gameObject.activeSelf || part.State == PartState.Pooled) { fromBelt.Remove(part); delivered.Remove(part); continue; }
                if (delivered.Contains(part)) continue;
                Bounds b = part.Shape.bounds;
                bool onBelt = OverBelt(b);
                if (onBelt && part.State != PartState.Held) { fromBelt.Add(part); continue; }
                if (!fromBelt.Contains(part)) continue;
                bool onTable = pickupTable != null && Over(b, pickupTable.bounds) && part.State != PartState.Held;
                if (onTable || part.State == PartState.Held)
                {
                    fromBelt.Remove(part); delivered.Add(part);
                    director.QueueArrival(b.center);
                }
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
