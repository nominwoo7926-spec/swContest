using UnityEngine;

namespace ErgoContest
{
    public sealed class WorkTaskObserver : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private Transform workerRoot;
        [SerializeField] private Transform supplyPickupPoint;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private Transform supplyMoveZone;
        [SerializeField] private Transform benchMoveZone;
        [SerializeField] private Vector3 supplyMoveZoneSize = new Vector3(1.0f, 1.4f, 1.0f);
        [SerializeField] private Vector3 benchMoveZoneSize = new Vector3(1.2f, 1.0f, 1.1f);
        [SerializeField] private float pickupRadius = 0.38f;
        [SerializeField] private float assemblyRadius = 0.42f;
        [SerializeField] private float highHandOffset = 0.22f;
        [SerializeField] private float farReachDistance = 0.78f;
        [SerializeField] private float cycleAverageSmoothing = 0.35f;

        private WorkPhase phase = WorkPhase.WaitingForPart;
        private bool wasHolding;
        private int completedCycles;
        private int queuedParts = 1;
        private float cycleTimer;
        private float averageCycleSeconds;
        private float lastCycleSeconds;
        private float highReachSeconds;
        private float farReachSeconds;
        private float assemblyHoldSeconds;
        private float queueWaitSeconds;
        private int highReachCount;
        private bool wasHighReach;
        private WorkObservation current;

        public WorkObservation Current => current;
        public float WorkerShoulderHeight => (workerRoot != null ? workerRoot.position.y : 0f) + 1.35f;

        private void Update()
        {
            if (inputProvider == null)
                return;

            ErgoInputFrame input = inputProvider.CurrentFrame;
            cycleTimer += Time.deltaTime;
            if (queuedParts <= 0)
                queueWaitSeconds += Time.deltaTime;

            float shoulderHeight = WorkerShoulderHeight;
            float reach = HorizontalDistance(input.HandPosition, workerRoot != null ? workerRoot.position : Vector3.zero);
            bool highReach = input.HandPosition.y > shoulderHeight + highHandOffset;
            if (highReach)
                highReachSeconds += Time.deltaTime;
            if (highReach && !wasHighReach)
                highReachCount++;
            wasHighReach = highReach;

            if (reach > farReachDistance)
                farReachSeconds += Time.deltaTime;

            bool nearAssembly = assemblyPoint != null && Vector3.Distance(input.HandPosition, assemblyPoint.position) <= assemblyRadius;
            if (nearAssembly && input.HoldingPart)
                assemblyHoldSeconds += Time.deltaTime;

            if (input.GrabPressed && queuedParts > 0 && supplyPickupPoint != null && Vector3.Distance(input.HandPosition, supplyPickupPoint.position) <= pickupRadius)
            {
                queuedParts--;
                phase = WorkPhase.CarryingToBench;
            }
            else if (input.HoldingPart && phase != WorkPhase.CarryingToBench)
            {
                phase = WorkPhase.CarryingToBench;
            }

            if (input.HoldingPart && supplyPickupPoint != null && Vector3.Distance(input.HandPosition, supplyPickupPoint.position) <= pickupRadius)
                phase = WorkPhase.ReachingSupply;

            if (input.HoldingPart && nearAssembly)
                phase = WorkPhase.Assembling;

            if (wasHolding && !input.HoldingPart && nearAssembly)
                CompleteCycle();

            if (!input.HoldingPart && phase == WorkPhase.Completed && cycleTimer > 0.5f)
                phase = WorkPhase.WaitingForPart;

            bool handInSupply = IsInsideBox(input.HandPosition, supplyMoveZone, supplyMoveZoneSize);
            bool handInBench = IsInsideBox(input.HandPosition, benchMoveZone, benchMoveZoneSize);
            current = new WorkObservation(
                phase,
                completedCycles,
                queuedParts,
                averageCycleSeconds,
                lastCycleSeconds,
                highReachSeconds,
                highReachCount,
                farReachSeconds,
                assemblyHoldSeconds,
                queueWaitSeconds,
                handInSupply,
                handInBench,
                input.HoldingPart);

            wasHolding = input.HoldingPart;
        }

        public void AddQueuedPart()
        {
            queuedParts = Mathf.Clamp(queuedParts + 1, 0, 8);
        }

        public void ResetWindow()
        {
            highReachSeconds = 0f;
            farReachSeconds = 0f;
            assemblyHoldSeconds = 0f;
            queueWaitSeconds = 0f;
            highReachCount = 0;
            wasHighReach = false;
        }

        private void CompleteCycle()
        {
            completedCycles++;
            lastCycleSeconds = cycleTimer;
            averageCycleSeconds = averageCycleSeconds <= 0f
                ? lastCycleSeconds
                : Mathf.Lerp(averageCycleSeconds, lastCycleSeconds, cycleAverageSmoothing);
            cycleTimer = 0f;
            phase = WorkPhase.Completed;
            queuedParts = Mathf.Max(queuedParts, 0);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector2 av = new Vector2(a.x, a.z);
            Vector2 bv = new Vector2(b.x, b.z);
            return Vector2.Distance(av, bv);
        }

        private static bool IsInsideBox(Vector3 point, Transform box, Vector3 size)
        {
            if (box == null)
                return false;
            Vector3 local = box.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f
                && Mathf.Abs(local.y) <= size.y * 0.5f
                && Mathf.Abs(local.z) <= size.z * 0.5f;
        }
    }
}
