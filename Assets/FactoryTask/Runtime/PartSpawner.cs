using UnityEngine;

namespace FactoryTask
{
    public sealed class PartSpawner : MonoBehaviour
    {
        public ConveyorPart prefab;
        public Transform spawnPoint;
        [Range(6, 16)] public int poolSize = 16;
        public float intervalSeconds = 3.5f;
        // Clear space required above the spawn point (largest part plus a gap) before supplying.
        public Vector3 clearance = new Vector3(.34f, .26f, .34f);
        public ConveyorPart[] Parts { get; private set; }
        public int SpawnedCount { get; private set; }
        public int ReusedCount { get; private set; }
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
        public void Tick(float deltaTime)
        {
            Initialize(); countdown = Mathf.Max(0, countdown - deltaTime);
            // Parts back up on a blocked line; wait for the previous part to move clear rather
            // than spawning one inside another.
            if (countdown > 0 || !SpawnAreaClear()) return;
            // The pool is fixed (session files store one slot per part). A full line backs up to the
            // spawn point physically, so supply simply waits until a part is completed and recycled.
            for (int i = 0; i < Parts.Length; i++) if (Parts[i].State == PartState.Pooled)
            {
                Parts[i].Supply(SpawnedCount % 3, spawnPoint.position, spawnPoint.rotation);
                if (used[i]) ReusedCount++; used[i] = true;
                SpawnedCount++; countdown = intervalSeconds; return;
            }
        }
        public bool SpawnAreaClear()
        {
            Vector3 centre = spawnPoint.position + spawnPoint.up * (clearance.y * .5f + .01f);
            int count = Physics.OverlapBoxNonAlloc(centre, clearance * .5f, overlaps, spawnPoint.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (overlaps[i].attachedRigidbody != null && overlaps[i].attachedRigidbody.GetComponent<ConveyorPart>() != null) return false;
            return true;
        }
        public void Recycle(ConveyorPart part) { part.ReturnToPool(); }
    }
}
