using System.Text;
using UnityEngine;

namespace ErgoContest
{
    public sealed class WorldStatusPanel : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private WorkTaskObserver observer;
        [SerializeField] private ErgoLoadEstimator loadEstimator;
        [SerializeField] private ErgoAgentController agent;
        [SerializeField] private AdjustableEquipment supplyTray;
        [SerializeField] private AdjustableEquipment workbench;
        [SerializeField] private AdjustableEquipment conveyor;
        [SerializeField] private TextMesh titleText;
        [SerializeField] private TextMesh metricsText;
        [SerializeField] private TextMesh agentText;
        [SerializeField] private TextMesh compareText;
        [SerializeField] private Transform faceCamera;

        private readonly StringBuilder builder = new StringBuilder(512);

        private void Update()
        {
            if (faceCamera != null)
            {
                Vector3 direction = transform.position - faceCamera.position;
                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            if (titleText != null)
                titleText.text = "ERGONOMIC CONTROL AGENT";

            if (loadEstimator == null || observer == null)
                return;

            WorkObservation work = observer.Current;
            ErgoLoadResult load = loadEstimator.Current;
            if (metricsText != null)
            {
                builder.Length = 0;
                builder.AppendLine($"Input: {(inputProvider != null ? inputProvider.InputModeLabel : "-")}   Phase: {work.Phase}");
                builder.AppendLine($"Load {load.TotalScore:0}/100  Shoulder {load.ShoulderScore:0}  Arm {load.ArmScore:0}  Hand {load.HandScore:0}");
                builder.AppendLine($"Reach {load.ReachMeters:0.00} m  Height {load.HandHeightFromShoulder:+0.00;-0.00} m  Cause: {load.PrimaryCause}");
                builder.AppendLine($"Cycle {work.LastCycleSeconds:0.0}s avg {work.AverageCycleSeconds:0.0}s  Queue {work.QueuedParts}");
                metricsText.text = builder.ToString();
                metricsText.color = load.DisplayColor;
            }

            if (agentText != null && agent != null)
            {
                builder.Length = 0;
                builder.AppendLine($"Agent: {agent.Stage}");
                if (agent.Selected.IsValid)
                {
                    builder.AppendLine($"{agent.Selected.Action}  [{agent.Selected.Cause}]");
                    builder.AppendLine(agent.Selected.Evidence);
                    if (agent.SecondBest.IsValid)
                        builder.AppendLine($"Compared: {agent.SecondBest.Action} ({agent.SecondBest.Score:0})");
                }
                if (!string.IsNullOrEmpty(agent.HoldReason))
                    builder.AppendLine($"Hold: {agent.HoldReason}");
                builder.AppendLine($"Supply H {Height(supplyTray):0.00}  Bench H {Height(workbench):0.00}  Conv {Speed(conveyor):0.00}x");
                builder.AppendLine("P auto   Space/Grip grab   F release   Enter/A approve   C/B cancel   Tab/Menu pause");
                agentText.text = builder.ToString();
            }

            if (compareText != null && agent != null)
            {
                builder.Length = 0;
                if (agent.HasBefore)
                    builder.AppendLine($"Before: {agent.BeforeResult.TotalScore:0} pts, reach {agent.BeforeResult.ReachMeters:0.00} m");
                if (agent.HasAfter)
                {
                    float delta = agent.AfterResult.TotalScore - agent.BeforeResult.TotalScore;
                    builder.AppendLine($"After:  {agent.AfterResult.TotalScore:0} pts, reach {agent.AfterResult.ReachMeters:0.00} m");
                    builder.AppendLine(delta < -1f ? $"Measured improvement {Mathf.Abs(delta):0} pts" : $"No measured improvement ({delta:+0;-0;0} pts)");
                }
                else
                {
                    builder.AppendLine("After: repeat the task after movement");
                }
                compareText.text = builder.ToString();
            }
        }

        private static float Height(AdjustableEquipment equipment)
        {
            return equipment != null ? equipment.CurrentPosition.y : 0f;
        }

        private static float Speed(AdjustableEquipment equipment)
        {
            return equipment != null ? equipment.SpeedValue : 0f;
        }
    }
}
