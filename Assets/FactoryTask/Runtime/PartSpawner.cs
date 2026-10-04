using UnityEngine;

namespace FactoryTask
{
    public sealed class PartSpawner : MonoBehaviour
    {
        public ConveyorPart prefab;
        public PickupQueue queue;
        public Transform spawnPoint;
        [Range(6, 24)] public int poolSize = 12;
        public float intervalSeconds = 3.5f;
        public ConveyorPart[] Parts { get; private set; }
        public int SpawnedCount { get; private set; }
        public int ReusedCount { get; private set; }
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
            if (countdown > 0) return;
            for (int i = 0; i < Parts.Length; i++) if (Parts[i].State == PartState.Pooled)
            {
                Parts[i].Supply(SpawnedCount % 3, spawnPoint.position, spawnPoint.rotation);
                if (!queue.Enqueue(Parts[i])) { Parts[i].ReturnToPool(); return; }
                if (used[i]) ReusedCount++; used[i] = true;
                SpawnedCount++; countdown = intervalSeconds; return;
            }
            // Do not stop the conveyor merely because the initial pool is in use. Expand in
            // small batches; completed parts are still recycled and reused first.
            int oldLength=Parts.Length,newLength=oldLength+6;
            var expandedParts=new ConveyorPart[newLength];var expandedUsed=new bool[newLength];
            System.Array.Copy(Parts,expandedParts,oldLength);System.Array.Copy(used,expandedUsed,oldLength);
            Parts=expandedParts;used=expandedUsed;
            for(int i=oldLength;i<newLength;i++)
            {
                Parts[i]=Instantiate(prefab,transform);Parts[i].name="Pooled_Part_"+i;
                Parts[i].Initialize();Parts[i].ReturnToPool();
            }
            Tick(0);
        }
        public void Recycle(ConveyorPart part) { part.ReturnToPool(); }
    }
}
