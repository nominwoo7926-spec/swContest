using UnityEngine;

namespace ErgoContest
{
    public sealed class ErgoAgentController : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private WorkTaskObserver observer;
        [SerializeField] private ErgoLoadEstimator loadEstimator;
        [SerializeField] private AdjustableEquipment supplyTray;
        [SerializeField] private AdjustableEquipment workbench;
        [SerializeField] private AdjustableEquipment conveyor;
        [SerializeField] private float evaluationIntervalSeconds = 0.8f;
        [SerializeField] private float minTotalLoadForAction = 48f;
        [SerializeField] private int highReachCountThreshold = 2;
        [SerializeField] private float farReachSecondsThreshold = 1.2f;
        [SerializeField] private float assemblyHoldSecondsThreshold = 1.2f;
        [SerializeField] private int queueThreshold = 3;
        [SerializeField] private bool requireApproval = true;

        private AgentStage stage = AgentStage.Observing;
        private AgentCandidate selected;
        private AgentCandidate secondBest;
        private float evaluationTimer;
        private bool afterMeasurementArmed;
        private int cyclesAtAdjustment;
        private ErgoLoadResult beforeResult;
        private ErgoLoadResult afterResult;
        private bool hasBefore;
        private bool hasAfter;
        private string holdReason = string.Empty;

        public AgentStage Stage => stage;
        public AgentCandidate Selected => selected;
        public AgentCandidate SecondBest => secondBest;
        public string HoldReason => holdReason;
        public ErgoLoadResult BeforeResult => beforeResult;
        public ErgoLoadResult AfterResult => afterResult;
        public bool HasBefore => hasBefore;
        public bool HasAfter => hasAfter;
        public bool IsPaused => stage == AgentStage.Paused;

        private void Update()
        {
            if (inputProvider == null || observer == null || loadEstimator == null)
                return;

            ErgoInputFrame input = inputProvider.CurrentFrame;
            if (input.PausePressed)
                stage = stage == AgentStage.Paused ? AgentStage.Observing : AgentStage.Paused;
            if (stage == AgentStage.Paused)
                return;

            if (input.CancelPressed)
            {
                selected = default;
                holdReason = "operator cancelled adjustment";
                stage = AgentStage.Cancelled;
                return;
            }

            if (IsSelectedEquipmentMoving())
            {
                stage = AgentStage.Moving;
                return;
            }

            if (stage == AgentStage.Moving)
            {
                observer.ResetWindow();
                afterMeasurementArmed = true;
                cyclesAtAdjustment = observer.Current.CompletedCycles;
                stage = AgentStage.MeasuringAfter;
            }

            if (afterMeasurementArmed && observer.Current.CompletedCycles > cyclesAtAdjustment)
            {
                afterResult = loadEstimator.Current;
                hasAfter = true;
                afterMeasurementArmed = false;
                stage = AgentStage.Observing;
            }

            evaluationTimer += Time.deltaTime;
            if (evaluationTimer >= evaluationIntervalSeconds && (stage == AgentStage.Observing || stage == AgentStage.Cancelled || stage == AgentStage.MeasuringAfter))
            {
                evaluationTimer = 0f;
                Evaluate();
            }

            if (!selected.IsValid)
                return;

            if (!SafetyClear(selected.Equipment, out holdReason))
            {
                stage = AgentStage.SafetyHold;
                return;
            }

            if (requireApproval && !input.ApprovePressed)
            {
                holdReason = string.Empty;
                stage = AgentStage.WaitingForApproval;
                return;
            }

            ApplySelected();
        }

        private void Evaluate()
        {
            WorkObservation work = observer.Current;
            ErgoLoadResult load = loadEstimator.Current;
            if (load.TotalScore < minTotalLoadForAction && work.QueuedParts < queueThreshold)
            {
                selected = default;
                secondBest = default;
                stage = AgentStage.Observing;
                return;
            }

            AgentCandidate high = BuildHighPickupCandidate(work);
            AgentCandidate far = BuildFarReachCandidate(work);
            AgentCandidate bench = BuildBenchCandidate(work, load);
            AgentCandidate speed = BuildConveyorCandidate(work);

            selected = PickBest(high, far, bench, speed, out secondBest);
            if (selected.IsValid)
            {
                beforeResult = load;
                hasBefore = true;
                hasAfter = false;
                stage = AgentStage.Evaluating;
            }
            else
            {
                stage = AgentStage.Observing;
            }
        }

        private AgentCandidate BuildHighPickupCandidate(WorkObservation work)
        {
            if (supplyTray == null)
                return default;
            float score = work.HighReachSeconds * 10f + work.HighReachCount * 18f;
            if (work.HighReachCount < highReachCountThreshold)
                score *= 0.35f;
            Vector3 target = supplyTray.CurrentPosition + Vector3.down * 0.22f;
            target = supplyTray.ClampPosition(target);
            return new AgentCandidate(EquipmentType.SupplyTray, target, 0f, score, "repeated high pickup", "lower supply tray", $"high reach {work.HighReachCount}x / {work.HighReachSeconds:0.0}s");
        }

        private AgentCandidate BuildFarReachCandidate(WorkObservation work)
        {
            if (supplyTray == null)
                return default;
            float score = work.FarReachSeconds * 22f;
            if (work.FarReachSeconds < farReachSecondsThreshold)
                score *= 0.35f;
            Vector3 target = supplyTray.CurrentPosition + new Vector3(0.22f, 0f, -0.16f);
            target = supplyTray.ClampPosition(target);
            return new AgentCandidate(EquipmentType.SupplyTray, target, 0f, score, "long horizontal reach", "move supply tray closer", $"far reach {work.FarReachSeconds:0.0}s");
        }

        private AgentCandidate BuildBenchCandidate(WorkObservation work, ErgoLoadResult load)
        {
            if (workbench == null)
                return default;
            float score = work.AssemblyHoldSeconds * 24f + Mathf.Abs(load.HandHeightFromShoulder + 0.25f) * 18f;
            if (work.AssemblyHoldSeconds < assemblyHoldSecondsThreshold)
                score *= 0.35f;
            float desiredDelta = load.HandHeightFromShoulder > -0.15f ? -0.16f : 0.14f;
            Vector3 target = workbench.ClampPosition(workbench.CurrentPosition + Vector3.up * desiredDelta);
            return new AgentCandidate(EquipmentType.Workbench, target, 0f, score, "assembly hold height", "adjust workbench height", $"assembly hold {work.AssemblyHoldSeconds:0.0}s");
        }

        private AgentCandidate BuildConveyorCandidate(WorkObservation work)
        {
            if (conveyor == null)
                return default;
            float score = Mathf.Max(0, work.QueuedParts - queueThreshold + 1) * 18f + work.QueueWaitSeconds * 2f;
            float targetSpeed = conveyor.ClampSpeed(conveyor.SpeedValue - 0.2f);
            return new AgentCandidate(EquipmentType.Conveyor, conveyor.CurrentPosition, targetSpeed, score, "part inflow exceeds pace", "reduce conveyor speed", $"queue {work.QueuedParts}, cycle {work.AverageCycleSeconds:0.0}s");
        }

        private static AgentCandidate PickBest(AgentCandidate a, AgentCandidate b, AgentCandidate c, AgentCandidate d, out AgentCandidate runnerUp)
        {
            AgentCandidate best = default;
            runnerUp = default;
            Consider(a, ref best, ref runnerUp);
            Consider(b, ref best, ref runnerUp);
            Consider(c, ref best, ref runnerUp);
            Consider(d, ref best, ref runnerUp);
            return best;
        }

        private static void Consider(AgentCandidate candidate, ref AgentCandidate best, ref AgentCandidate runnerUp)
        {
            if (!candidate.IsValid)
                return;
            if (!best.IsValid || candidate.Score > best.Score)
            {
                runnerUp = best;
                best = candidate;
            }
            else if (!runnerUp.IsValid || candidate.Score > runnerUp.Score)
            {
                runnerUp = candidate;
            }
        }

        private bool SafetyClear(EquipmentType type, out string reason)
        {
            WorkObservation work = observer.Current;
            if (work.HoldingPart)
            {
                reason = "holding part";
                return false;
            }
            if (type == EquipmentType.SupplyTray && work.HandInSupplyMoveZone)
            {
                reason = "hand inside supply tray travel zone";
                return false;
            }
            if (type == EquipmentType.Workbench && work.HandInBenchMoveZone)
            {
                reason = "hand inside workbench lift zone";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void ApplySelected()
        {
            switch (selected.Equipment)
            {
                case EquipmentType.SupplyTray:
                    if (supplyTray != null)
                        supplyTray.MoveTo(selected.TargetPosition);
                    break;
                case EquipmentType.Workbench:
                    if (workbench != null)
                        workbench.MoveTo(selected.TargetPosition);
                    break;
                case EquipmentType.Conveyor:
                    if (conveyor != null)
                        conveyor.SetSpeed(selected.TargetSpeed);
                    break;
            }
            stage = AgentStage.Moving;
        }

        private bool IsSelectedEquipmentMoving()
        {
            return (supplyTray != null && supplyTray.IsMoving)
                || (workbench != null && workbench.IsMoving)
                || (conveyor != null && conveyor.IsMoving);
        }
    }
}
