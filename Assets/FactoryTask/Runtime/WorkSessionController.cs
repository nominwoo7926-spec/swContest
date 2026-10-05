using System.Collections.Generic;
using UnityEngine;

namespace FactoryTask
{
    // Work run state machine driven by the three-button panel.
    //   Green (Baseline): one part every 2 s at a fixed belt speed, fixed table height.
    //   Blue (Optimized):
    //     Phase 1 (first 20 parts) - collects the worker's elbow height, then sets the line height
    //       once, ISO 14738 style: work surface = elbow height - 15 cm (heavy handling).
    //     Phase 2 (parts 21-100) - per finished part, records belt speed v, handling time t and RULA r
    //       in a sliding window, fits r = alpha * v + beta by least squares, and picks the belt speed v*
    //       minimising J(v) = w1 * r(v) + w2 * Penalty(v, v_req), where v_req is the speed that still
    //       delivers the remaining quota before the deadline. v* is reached through EMA smoothing.
    //   Red: start (or restart) a run in the selected mode.
    // The spacing between parts on the belt is constant, so the belt speed sets the supply rate.
    [DefaultExecutionOrder(-120)]
    public sealed class WorkSessionController : MonoBehaviour
    {
        public enum Mode { Baseline, Optimized }
        public enum RunState { Waiting, Running, Finished }

        public ConveyorController conveyor;
        public PartSpawner spawner;
        public XRGrabTaskTracker task;
        public RulaAssessment rula;
        public FullBodyAvatarIK avatar;
        public LineHeightAdjuster line;
        public ThirdPersonAvatarDirector director;
        public VRSessionRecorder recorder;

        [Header("Run")]
        public int quota = 100;
        public float deadlineSeconds = 200;
        public float baselineSpeed = ConveyorController.FixedSpeed;
        [Tooltip("Seconds between parts in the baseline mode (spacing = speed x interval).")]
        public float baselineInterval = 2;
        [Tooltip("Belt length from the spawn point to the table, travelled by every part.")]
        public float travelDistance = 3.2f;

        [Header("Phase 1: one-shot height calibration")]
        public int calibrationParts = 20;
        [Tooltip("ISO 14738 heavy handling: work surface this far below the elbow.")]
        public float elbowClearance = .15f;

        [Header("Phase 2: data-driven speed optimisation")]
        public int window = 15;
        public float minSpeed = .2f, maxSpeed = .6f;
        [Tooltip("Weight of the expected RULA score.")]
        public float w1 = 1;
        [Tooltip("Weight of the deadline penalty.")]
        public float w2 = 6;
        [Tooltip("Penalty multipliers for running slower than required (late) or faster (early).")]
        public float latePenalty = 1, earlyPenalty = .15f;
        [Tooltip("Ridge term keeping alpha stable while the speed has barely varied.")]
        public float ridge = .002f;

        public Mode SelectedMode { get; private set; } = Mode.Baseline;
        public RunState State { get; private set; }
        public float Elapsed => State == RunState.Waiting ? 0 : (State == RunState.Running ? Time.time : finishedAt) - startedAt;
        public int Completed { get; private set; }
        public bool HeightCalibrated { get; private set; }
        public float ElbowHeight { get; private set; }
        // Latest model and decision (inspectable, and written to the log).
        public float Alpha { get; private set; }
        public float Beta { get; private set; }
        public float RequiredSpeed { get; private set; }
        public float OptimalSpeed { get; private set; }
        public float Spacing => baselineSpeed * baselineInterval;

        sealed class PartLog { public float speedSum, speedTime, rulaSum; public int rulaSamples; }
        readonly Dictionary<ConveyorPart, PartLog> logs = new Dictionary<ConveyorPart, PartLog>();
        readonly Queue<Vector3> samples = new Queue<Vector3>(); // (v, t, r)
        float startedAt, finishedAt, elbowSum;
        int elbowCount;

        void OnEnable() { if (task != null) task.PartCompleted += OnPartCompleted; }
        void OnDisable() { if (task != null) task.PartCompleted -= OnPartCompleted; }

        // Green and blue are exclusive: selecting one deselects the other.
        public void SelectMode(Mode mode) { SelectedMode = mode; }

        // Red: start a run in the selected mode, wiping any run in progress.
        public void StartRun()
        {
            spawner.ResetRun(); task.ResetRun(); conveyor.StopImmediately();
            if (director != null) director.ResetBin();
            if (line != null) line.SetOffsetImmediate(0);
            logs.Clear(); samples.Clear();
            Completed = 0; HeightCalibrated = false; elbowSum = 0; elbowCount = 0;
            Alpha = Beta = 0; RequiredSpeed = OptimalSpeed = baselineSpeed;
            spawner.spawnLimit = quota; spawner.forceSpawn = true;
            conveyor.SetSpeedImmediate(baselineSpeed); conveyor.SupplyEnabled = true;
            State = RunState.Running; startedAt = Time.time;
            if (recorder != null) recorder.RestartRecording();
        }

        void FixedUpdate()
        {
            if (State != RunState.Running) return;
            float dt = Time.fixedDeltaTime;
            // Constant spacing on the belt: the supply interval follows the belt speed.
            spawner.intervalSeconds = Spacing / Mathf.Max(.05f, conveyor.CurrentSpeed);
            foreach (var part in spawner.Parts)
            {
                if (!part.gameObject.activeSelf) continue;
                if (!logs.TryGetValue(part, out var log) || part.State == PartState.Conveying && log.rulaSamples > 0)
                    logs[part] = log = new PartLog();
                if (part.State == PartState.Conveying) { log.speedSum += conveyor.CurrentSpeed * dt; log.speedTime += dt; }
                else if (part.State == PartState.Held && rula != null && rula.IsValid) { log.rulaSum += rula.WorstGrandScore; log.rulaSamples++; }
            }
            // Phase 1: average elbow height of the (tracked, hidden) avatar while the arms are free.
            if (SelectedMode == Mode.Optimized && !HeightCalibrated && avatar != null && task.HeldWeight <= 0)
            {
                elbowSum += (avatar.leftArm.lower.position.y + avatar.rightArm.lower.position.y) * .5f;
                elbowCount++;
            }
        }

        void OnPartCompleted(ConveyorPart part)
        {
            if (State != RunState.Running) return;
            Completed++;
            logs.TryGetValue(part, out var log);
            float v = log != null && log.speedTime > 0 ? log.speedSum / log.speedTime : conveyor.CurrentSpeed;
            float r = log != null && log.rulaSamples > 0 ? log.rulaSum / log.rulaSamples : (rula != null && rula.IsValid ? rula.WorstGrandScore : 0);
            samples.Enqueue(new Vector3(v, part.HoldSeconds, r));
            while (samples.Count > window) samples.Dequeue();
            logs.Remove(part);

            if (SelectedMode == Mode.Optimized)
            {
                if (!HeightCalibrated && Completed >= calibrationParts) CalibrateHeight();
                else if (HeightCalibrated) Optimise();
            }
            if (Completed >= quota) Finish();
        }

        void Finish()
        {
            State = RunState.Finished; finishedAt = Time.time;
            conveyor.TargetSpeed = 0; conveyor.SupplyEnabled = false;
        }

        // Phase 1 result: one line-height change, then the height stays for the rest of the run.
        void CalibrateHeight()
        {
            HeightCalibrated = true;
            if (elbowCount == 0 || line == null) return;
            ElbowHeight = elbowSum / elbowCount;
            float target = ElbowHeight - elbowClearance;
            line.SetTargetOffset(target - line.BaseTableTopHeight);
            Debug.Log($"[AI] Height calibrated: elbow {ElbowHeight:F3} m -> table top {target:F3} m (offset {line.TargetOffset:+0.000;-0.000} m)");
        }

        // Phase 2: refit the speed-load model on the sliding window and choose the next belt speed.
        void Optimise()
        {
            FitLoadModel(samples, ridge, out float alpha, out float beta);
            Alpha = alpha; Beta = beta;
            float meanHandling = 0; foreach (var s in samples) meanHandling += s.y; meanHandling /= Mathf.Max(1, samples.Count);
            int remaining = Mathf.Max(0, quota - spawner.SpawnedCount);
            // The last part must be supplied, travel to the table and be handled before the deadline.
            float timeLeft = deadlineSeconds - Elapsed - meanHandling;
            RequiredSpeed = timeLeft > .5f ? (Spacing * remaining + travelDistance) / timeLeft : maxSpeed;
            // Lateness costs more as the run nears its end, so the deadline is met within a few seconds.
            float urgency = quota / (float)Mathf.Max(1, remaining);
            OptimalSpeed = Mathf.Clamp(OptimalBeltSpeed(alpha, RequiredSpeed, w1, w2, latePenalty * urgency, earlyPenalty), minSpeed, maxSpeed);
            conveyor.TargetSpeed = OptimalSpeed;
            Debug.Log($"[AI] #{Completed}: r = {alpha:F2} v + {beta:F2}, v_req {RequiredSpeed:F3}, v* {OptimalSpeed:F3} m/s");
        }

        // Least-squares line r = alpha * v + beta, with a small ridge term on the slope.
        public static void FitLoadModel(IEnumerable<Vector3> data, float ridge, out float alpha, out float beta)
        {
            float n = 0, sv = 0, sr = 0;
            foreach (var d in data) { n++; sv += d.x; sr += d.z; }
            if (n == 0) { alpha = beta = 0; return; }
            float mv = sv / n, mr = sr / n, cov = 0, var = 0;
            foreach (var d in data) { cov += (d.x - mv) * (d.z - mr); var += (d.x - mv) * (d.x - mv); }
            alpha = cov / (var + ridge * n);
            beta = mr - alpha * mv;
        }

        // argmin J(v) = w1 (alpha v + beta) + w2 k(v) ((v - vReq) / vReq)^2, with k = late below vReq and
        // early above it. dJ/dv = 0 gives v = vReq - w1 alpha vReq^2 / (2 w2 k) on each side of vReq.
        public static float OptimalBeltSpeed(float alpha, float vReq, float w1, float w2, float late, float early)
        {
            if (vReq <= 0) return 0;
            float slower = vReq - w1 * alpha * vReq * vReq / (2 * w2 * late);
            if (slower < vReq) return slower;   // load rises with speed: accept a little lateness
            float faster = vReq - w1 * alpha * vReq * vReq / (2 * w2 * early);
            if (faster > vReq) return faster;   // load falls with speed: run a little ahead
            return vReq;
        }
    }
}
