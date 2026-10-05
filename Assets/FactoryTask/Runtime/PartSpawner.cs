using UnityEngine;

namespace FactoryTask
{
    public sealed class PartSpawner : MonoBehaviour
    {
        public ConveyorPart prefab;
        public Transform spawnPoint;
        [Range(6, 16)] public int poolSize = 16;
        // Seconds between parts; the work session derives it from the belt speed (fixed spacing).
        public float intervalSeconds = 2f;
        // Every supplied part uses one variant: 0 = 1 kg / 17 cm, 1 = 3 kg / 20.5 cm, 2 = 5 kg / 24 cm.
        [Range(0, 2)] public int partKind = 2;
        [Tooltip("Parts supplied in one run (0 = unlimited). The last one uses finalPartMaterial.")]
        public int spawnLimit;
        public Material finalPartMaterial;
        [Tooltip("Supply on time even when the line is backed up: the part is placed on top of the pile.")]
        public bool forceSpawn;
        // Clear space required above the spawn point (largest part plus a gap) before supplying.
        public Vector3 clearance = new Vector3(.34f, .26f, .34f);
        public ConveyorPart[] Parts { get; private set; }
        public int SpawnedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public bool QuotaReached => spawnLimit > 0 && SpawnedCount >= spawnLimit;
        bool[] used;
        float countdown;
        readonly Collider[] overlaps = new Collider[16];
        public void Initialize()
        {
            if (Parts != null) return;
            Parts = new ConveyorPart[poolSize]; used = new bool[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                Parts[i] = Instantiate(prefab, transform); Parts[i].name = "Pooled_Part_" + i;
                Parts[i].Initialize(); Parts[i].ReturnToPool();
            }
        }
        void Awake() { Initialize(); }

        // A new run: every part returns to the pool and the count starts again from zero.
        public void ResetRun()
        {
            Initialize();
            foreach (var part in Parts) part.ReturnToPool();
            SpawnedCount = 0; countdown = 0;
        }

        public void Tick(float deltaTime)
        {
            Initialize(); countdown = Mathf.Max(0, countdown - deltaTime);
            if (countdown > 0 || QuotaReached) return;
            Vector3 position = spawnPoint.position;
            if (!SpawnAreaClear(position))
            {
                // Parts back up on a blocked line. Either wait for the previous part to move clear, or
                // (forced supply) drop the new part onto the pile instead of inside another part.
                if (!forceSpawn) return;
                bool found = false;
                for (int level = 1; level <= 3 && !found; level++)
                {
                    position = spawnPoint.position + spawnPoint.up * (clearance.y * level);
                    found = SpawnAreaClear(position);
                }
                if (!found) return;
            }
            var part = TakeFreePart();
            if (part == null) return;
            bool last = spawnLimit > 0 && SpawnedCount + 1 == spawnLimit;
            part.Supply(partKind, position, spawnPoint.rotation, last ? finalPartMaterial : null);
            part.SpawnIndex = SpawnedCount;
            SpawnedCount++; countdown = intervalSeconds;
        }

        // A pooled part, or under forced supply (the 16-part pool is fixed by the session file format)
        // the part that matters least: one lying on the floor, then the oldest part nobody handled.
        ConveyorPart TakeFreePart()
        {
            for (int i = 0; i < Parts.Length; i++) if (Parts[i].State == PartState.Pooled)
            {
                if (used[i]) ReusedCount++; used[i] = true;
                return Parts[i];
            }
            if (!forceSpawn) return null;
            ConveyorPart best = null; float bestScore = float.MaxValue;
            foreach (var part in Parts)
            {
                if (!part.Free) continue;
                bool onFloor = part.Body.position.y < .5f;
                float score = (onFloor ? 0 : 1e6f) + (part.State == PartState.Conveying ? 0 : 5e5f) + part.SpawnIndex;
                if (score < bestScore) { bestScore = score; best = part; }
            }
            if (best == null) return null;
            ReusedCount++; best.ReturnToPool();
            return best;
        }

        public bool SpawnAreaClear() => SpawnAreaClear(spawnPoint.position);
        bool SpawnAreaClear(Vector3 position)
        {
            Vector3 centre = position + spawnPoint.up * (clearance.y * .5f + .01f);
            int count = Physics.OverlapBoxNonAlloc(centre, clearance * .5f, overlaps, spawnPoint.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (overlaps[i].attachedRigidbody != null && overlaps[i].attachedRigidbody.GetComponent<ConveyorPart>() != null) return false;
            return true;
        }
        public void Recycle(ConveyorPart part) { part.ReturnToPool(); }
    }
}
