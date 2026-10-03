using UnityEngine;

namespace FactoryTask
{
    public sealed class PartSpawner : MonoBehaviour
    {
        public enum SupplyMode { Random=-1, OneKg=0, ThreeKg=1, FiveKg=2 }
        public ConveyorPart prefab;
        public PickupQueue queue;
        public Transform spawnPoint;
        [Range(6, 16)] public int poolSize = 12;
        public float intervalSeconds = 3.5f;
        public ConveyorPart[] Parts { get; private set; }
        public int SpawnedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public SupplyMode Mode { get; private set; } = SupplyMode.FiveKg;
        bool[] used;
        float countdown;
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
            if (countdown > 0 || queue.Full) return;
            for (int i = 0; i < Parts.Length; i++) if (Parts[i].State == PartState.Pooled)
            {
                int kind=(int)SupplyMode.FiveKg;
                Parts[i].Supply(kind, spawnPoint.position, spawnPoint.rotation);
                if (!queue.Enqueue(Parts[i])) { Parts[i].ReturnToPool(); return; }
                if (used[i]) ReusedCount++; used[i] = true;
                SpawnedCount++; countdown = intervalSeconds; return;
            }
        }
        public void Recycle(ConveyorPart part) { part.ReturnToPool(); }
        public void SetMode(SupplyMode mode) { Mode=SupplyMode.FiveKg; }
    }
}
