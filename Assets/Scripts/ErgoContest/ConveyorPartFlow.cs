using UnityEngine;

namespace ErgoContest
{
    public sealed class ConveyorPartFlow : MonoBehaviour
    {
        [SerializeField] private AdjustableEquipment conveyorEquipment;
        [SerializeField] private WorkTaskObserver observer;
        [SerializeField] private Transform[] parts;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField] private float baseTravelSeconds = 5.5f;
        [SerializeField] private float spawnIntervalSeconds = 2.6f;

        private readonly float[] progress = new float[8];
        private float spawnTimer;

        private void Start()
        {
            for (int i = 0; i < parts.Length && i < progress.Length; i++)
                progress[i] = -i * 0.28f;
        }

        private void Update()
        {
            if (startPoint == null || endPoint == null || parts == null)
                return;

            float speed = conveyorEquipment != null ? conveyorEquipment.SpeedValue : 1f;
            spawnTimer += Time.deltaTime * speed;
            if (spawnTimer >= spawnIntervalSeconds)
            {
                spawnTimer = 0f;
                if (observer != null)
                    observer.AddQueuedPart();
            }

            for (int i = 0; i < parts.Length && i < progress.Length; i++)
            {
                if (parts[i] == null)
                    continue;

                progress[i] += Time.deltaTime * speed / Mathf.Max(0.1f, baseTravelSeconds);
                if (progress[i] > 1f)
                    progress[i] -= 1.2f;

                bool visible = progress[i] >= 0f;
                parts[i].gameObject.SetActive(visible);
                if (visible)
                {
                    parts[i].position = Vector3.Lerp(startPoint.position, endPoint.position, Mathf.Clamp01(progress[i]));
                    parts[i].rotation = Quaternion.Euler(0f, Time.time * 60f * speed + i * 20f, 0f);
                }
            }
        }
    }
}
