using UnityEngine;

namespace ErgoContest
{
    public sealed class ErgoLoadEstimator : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private WorkTaskObserver observer;
        [SerializeField] private Transform workerRoot;
        [SerializeField] private float partWeightKg = 3f;
        [SerializeField] private float comfortableReachMeters = 0.55f;
        [SerializeField] private float heightWeight = 36f;
        [SerializeField] private float reachWeight = 34f;
        [SerializeField] private float assemblyHoldWeight = 6f;
        [SerializeField] private float repeatedHighReachWeight = 8f;
        [SerializeField] private float partWeightFactor = 3.5f;
        [SerializeField] private float smoothing = 7f;

        private ErgoLoadResult current;
        private float smoothedTotal;

        public ErgoLoadResult Current => current;
        public float PartWeightKg => partWeightKg;

        private void Update()
        {
            if (inputProvider == null || observer == null)
                return;

            ErgoInputFrame input = inputProvider.CurrentFrame;
            WorkObservation work = observer.Current;
            Vector3 worker = workerRoot != null ? workerRoot.position : Vector3.zero;
            float reach = Vector2.Distance(new Vector2(worker.x, worker.z), new Vector2(input.HandPosition.x, input.HandPosition.z));
            float handHeight = input.HandPosition.y - observer.WorkerShoulderHeight;
            float heightExcess = Mathf.Max(0f, handHeight);
            float reachExcess = Mathf.Max(0f, reach - comfortableReachMeters);

            float shoulder = Mathf.Clamp(heightExcess * heightWeight + work.HighReachCount * repeatedHighReachWeight + partWeightKg * partWeightFactor, 0f, 100f);
            float arm = Mathf.Clamp(reachExcess * reachWeight + work.FarReachSeconds * 5f + partWeightKg * 2.5f, 0f, 100f);
            float hand = Mathf.Clamp(work.AssemblyHoldSeconds * assemblyHoldWeight + (input.HoldingPart ? partWeightKg * 4f : 0f), 0f, 100f);
            float raw = Mathf.Clamp(14f + shoulder * 0.42f + arm * 0.36f + hand * 0.22f, 0f, 100f);
            smoothedTotal = Mathf.Lerp(smoothedTotal, raw, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            string cause = "normal range";
            if (shoulder >= arm && shoulder >= hand && shoulder > 22f)
                cause = "high repeated pickup";
            else if (arm >= shoulder && arm >= hand && arm > 22f)
                cause = "long horizontal reach";
            else if (hand > 22f)
                cause = "assembly hold height";

            current = new ErgoLoadResult(smoothedTotal, shoulder, arm, hand, reach, handHeight, cause);
        }
    }
}
