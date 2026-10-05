using System.Collections.Generic;
using UnityEngine;

namespace FactoryTask
{
    // Work run state machine driven by the three-button panel.
    //   Green (Baseline): one part every 2 s at a fixed belt speed, fixed table height.
    //   Blue (AI agent), same quota and deadline (100 parts in about 200 s):
    //     Phase 1 (first 20 parts) - explores slow / normal / fast belt speeds (average = baseline) to
    //       learn how the worker's load responds to pace, and measures the elbow height. Then the belt
    //       stops, the worker confirms behind them, and the line height is set once (ISO 14738:
    //       work surface = elbow - 15 cm).
    //     Phase 2 - every second: fits load L = a * v + b on the recent parts (ridge regression toward
    //       a prior "faster pace = more load"), corrects it with the live load, and picks the belt speed
    //       v* minimising J(v) = w1 * L(v) + wDanger * overshoot(peak(v) - 60)^2 + w2 * lateness(v, v_req),
    //       where v_req still delivers the quota by the deadline. High load slows the belt (recovery);
    //       spare capacity speeds it back up (catch-up). v* is reached through EMA smoothing.
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
        public BodyLoadEstimator estimator;
        // Writes each finished run (per-part loads, a per-second timeline and a summary row).
        public RunDataLogger dataLogger;
        // Asks the worker (behind them) before the one-shot height change; the run waits for YES.
        public HeightConfirmPanel confirmPanel;
        public UserCalibration calibration;

        [Header("Run")]
        public int quota = 100;
        public float deadlineSeconds = 200;
        public float baselineSpeed = ConveyorController.FixedSpeed;
        [Tooltip("Seconds between parts in the baseline mode (spacing = speed x interval).")]
        public float baselineInterval = 2;
        [Tooltip("Belt length from the spawn point to the table, travelled by every part.")]
        public float travelDistance = 3.2f;

        [Header("Phase 1: exploration and one-shot height calibration")]
        public int calibrationParts = 20;
        [Tooltip("Belt speeds tried in turn during phase 1; their average equals the baseline speed.")]
        public float[] explorationSpeeds = { .32f, .38f, .44f };
        [Tooltip("ISO 14738 heavy handling: work surface this far below the elbow.")]
        public float elbowClearance = .15f;

        [Header("Phase 2: load-based pacing")]
        public int window = 15;
        public float minSpeed = .26f, maxSpeed = .55f;
        [Tooltip("Seconds between speed decisions.")]
        public float controlInterval = 1;
        [Tooltip("Load index (0-100) above which the worker is given recovery time.")]
        public float loadThreshold = 48;
        [Tooltip("Prior slope of load per m/s of belt speed (faster pace = more load), used by the ridge fit.")]
        public float priorSlope = 60;
        [Tooltip("Ridge strength pulling the fitted slope toward the prior while the data are thin.")]
        public float ridge = .002f;
        [Tooltip("Weights of mean load, load above the threshold, and lateness against the deadline.")]
        public float w1 = 1, wDanger = 80, w2 = 6;
        [Tooltip("Lateness multipliers for running slower than required (late) or faster (early).")]
        public float latePenalty = 1, earlyPenalty = .15f;

        public Mode SelectedMode { get; private set; } = Mode.Baseline;
        public RunState State { get; private set; }
        // Run time excluding the wait at the height prompt, which does not count against the deadline.
        public float Elapsed => State == RunState.Waiting ? 0 : (State == RunState.Running ? Time.time : finishedAt) - startedAt - pausedSeconds - (AwaitingConfirmation ? Time.time - pausedAt : 0);
        public bool AwaitingConfirmation { get; private set; }
        public int Completed { get; private set; }
        public bool HeightCalibrated { get; private set; }
        public float ElbowHeight { get; private set; }
        // Latest model and decision (inspectable, shown in the spectator view, written to the log).
        public float Alpha { get; private set; }
        public float Beta { get; private set; }
        public float RequiredSpeed { get; private set; }
        public float OptimalSpeed { get; private set; }
        public float LiveLoad { get; private set; }
        public float LivePeak { get; private set; }
        public string StatusText { get; private set; } = "대기";
        public string ModeLabel => SelectedMode == Mode.Optimized ? "AI 모드" : "기준 모드";
        public float Spacing => baselineSpeed * baselineInterval;

        sealed class PartLog
        {
            public float speedSum, speedTime, rulaSum, rulaMax; public int rulaSamples, loadSamples;
            public readonly float[] loadSum = new float[7], loadMax = new float[7];
        }
        readonly Dictionary<ConveyorPart, PartLog> logs = new Dictionary<ConveyorPart, PartLog>();
        readonly Queue<Vector3> samples = new Queue<Vector3>(); // (belt speed v, handling time t, load L)
        float startedAt, finishedAt, elbowSum, pausedAt, pausedSeconds, controlTimer, logTimer;
        bool resumeAfterLift;
        int elbowCount;

        void OnEnable() { if (task != null) task.PartCompleted += OnPartCompleted; }
        void OnDisable() { if (task != null) task.PartCompleted -= OnPartCompleted; }

        // Green and blue are exclusive: selecting one deselects the other.
        public void SelectMode(Mode mode) { SelectedMode = mode; if (State != RunState.Running) StatusText = ModeLabel + " 선택"; }

        // Red: start a run in the selected mode, wiping any run in progress.
        public void StartRun()
        {
            // A run restarted before reaching its quota is not saved.
            if (dataLogger != null) { if (State == RunState.Running) dataLogger.DiscardRun(); dataLogger.BeginRun(SelectedMode == Mode.Optimized ? "AI(파랑)" : "기준(초록)"); }
            spawner.ResetRun(); task.ResetRun(); conveyor.StopImmediately();
            if (director != null) director.ResetBin();
            if (line != null) line.SetOffsetImmediate(0);
            logs.Clear(); samples.Clear();
            Completed = 0; HeightCalibrated = false; elbowSum = 0; elbowCount = 0;
            AwaitingConfirmation = resumeAfterLift = false; pausedSeconds = 0; controlTimer = logTimer = 0;
            if (confirmPanel != null) confirmPanel.Hide();
            Alpha = Beta = 0; RequiredSpeed = OptimalSpeed = baselineSpeed; LiveLoad = LivePeak = 0;
            spawner.remainingToSupply = quota; spawner.forceSpawn = true;
            float first = SelectedMode == Mode.Optimized ? ExplorationSpeed() : baselineSpeed;
            conveyor.SetSpeedImmediate(first); conveyor.SupplyEnabled = true;
            State = RunState.Running; startedAt = Time.time;
            StatusText = SelectedMode == Mode.Optimized ? ExplorationStatus() : "기준 모드 · 고정 속도";
            if (recorder != null) recorder.RestartRecording();
        }

        void FixedUpdate()
        {
            if (State != RunState.Running) return;
            float dt = Time.fixedDeltaTime;
            // Constant spacing on the belt: the supply interval follows the belt speed.
            spawner.intervalSeconds = Spacing / Mathf.Max(.05f, conveyor.CurrentSpeed);
            // Parts lost on the way (dropped outside the work area, reclaimed) are supplied again, so
            // the quota can always be completed.
            spawner.remainingToSupply = RemainingToSupply();
            SampleLiveLoad(dt);
            LogParts(dt);
            // Phase 1: average elbow height of the (tracked, hidden) avatar while the arms are free.
            if (SelectedMode == Mode.Optimized && !HeightCalibrated && avatar != null && task.HeldWeight <= 0)
            {
                elbowSum += (avatar.leftArm.lower.position.y + avatar.rightArm.lower.position.y) * .5f;
                elbowCount++;
            }
            if (SelectedMode == Mode.Optimized && !AwaitingConfirmation && !resumeAfterLift)
            {
                if (!HeightCalibrated) { conveyor.TargetSpeed = ExplorationSpeed(); StatusText = ExplorationStatus(); }
                else if ((controlTimer += dt) >= controlInterval) { controlTimer = 0; Control(); }
            }
            if ((logTimer += dt) >= 1) { logTimer = 0; LogTimeline(); }
        }

        // Current whole-body load (mean of the seven regions) and the most loaded region, smoothed.
        void SampleLiveLoad(float dt)
        {
            if (estimator == null) return;
            float sum = 0, peak = 0;
            for (int i = 0; i < 7; i++) { float s = estimator.Score(i); sum += s; peak = Mathf.Max(peak, s); }
            float k = 1 - Mathf.Exp(-dt / 2f);
            LiveLoad = Mathf.Lerp(LiveLoad, sum / 7, k); LivePeak = Mathf.Lerp(LivePeak, peak, k);
        }

        void LogParts(float dt)
        {
            foreach (var part in spawner.Parts)
            {
                if (!part.gameObject.activeSelf) continue;
                if (!logs.TryGetValue(part, out var log) || part.State == PartState.Conveying && log.loadSamples > 0)
                    logs[part] = log = new PartLog();
                if (part.State == PartState.Conveying) { log.speedSum += conveyor.CurrentSpeed * dt; log.speedTime += dt; }
                else if (part.State == PartState.Held)
                {
                    if (rula != null && rula.IsValid) { log.rulaSum += rula.WorstGrandScore; log.rulaMax = Mathf.Max(log.rulaMax, rula.WorstGrandScore); log.rulaSamples++; }
                    if (estimator != null)
                    {
                        for (int i = 0; i < 7; i++) { float s = estimator.Score(i); log.loadSum[i] += s; log.loadMax[i] = Mathf.Max(log.loadMax[i], s); }
                        log.loadSamples++;
                    }
                }
            }
        }

        void LogTimeline()
        {
            if (dataLogger == null || estimator == null) return;
            float sum = 0, peak = 0;
            for (int i = 0; i < 7; i++) { float s = estimator.Score(i); sum += s; peak = Mathf.Max(peak, s); }
            dataLogger.AddSample(new RunDataLogger.TimelineSample
            {
                time = Elapsed, beltSpeed = conveyor.CurrentSpeed, targetSpeed = conveyor.TargetSpeed, requiredSpeed = RequiredSpeed,
                load = sum / 7, peakLoad = peak, rula = rula != null && rula.IsValid ? rula.WorstGrandScore : 0,
                tableHeight = line != null ? line.TableTopHeight : 0, completed = Completed, paused = AwaitingConfirmation || resumeAfterLift, status = StatusText
            });
        }

        // Phase 1 speeds: the exploration speeds in equal blocks of the calibration parts.
        float ExplorationSpeed()
        {
            if (explorationSpeeds == null || explorationSpeeds.Length == 0) return baselineSpeed;
            int block = Mathf.Clamp(Completed * explorationSpeeds.Length / Mathf.Max(1, calibrationParts), 0, explorationSpeeds.Length - 1);
            return explorationSpeeds[block];
        }
        string ExplorationStatus() => $"AI 탐색 · 속도별 부하 학습 ({Mathf.Min(Completed, calibrationParts)}/{calibrationParts})";

        void OnPartCompleted(ConveyorPart part)
        {
            if (State != RunState.Running) return;
            Completed++;
            logs.TryGetValue(part, out var log);
            float v = log != null && log.speedTime > 0 ? log.speedSum / log.speedTime : conveyor.CurrentSpeed;
            float r = log != null && log.rulaSamples > 0 ? log.rulaSum / log.rulaSamples : (rula != null && rula.IsValid ? rula.WorstGrandScore : 0);
            float load = 0;
            if (log != null && log.loadSamples > 0) { for (int i = 0; i < 7; i++) load += log.loadSum[i] / log.loadSamples; load /= 7; }
            else load = LiveLoad;
            samples.Enqueue(new Vector3(v, part.HoldSeconds, load));
            while (samples.Count > window) samples.Dequeue();
            if (dataLogger != null)
            {
                var record = new RunDataLogger.BoxRecord
                {
                    index = Completed, completedAt = Elapsed, holdSeconds = part.HoldSeconds, beltSpeed = v,
                    tableHeight = line != null ? line.TableTopHeight : 0, rulaMean = r, rulaMax = log != null ? log.rulaMax : r,
                    regionMean = new float[7], regionMax = new float[7]
                };
                for (int i = 0; i < 7; i++)
                {
                    record.regionMean[i] = log != null && log.loadSamples > 0 ? log.loadSum[i] / log.loadSamples : 0;
                    record.regionMax[i] = log != null ? log.loadMax[i] : 0;
                }
                dataLogger.AddBox(record);
            }
            logs.Remove(part);

            if (SelectedMode == Mode.Optimized && !HeightCalibrated && !AwaitingConfirmation && Completed >= calibrationParts) RequestHeightConfirmation();
            if (Completed >= quota) Finish();
        }

        // Phase 1 done: stop the belt completely and show the prompt behind the worker.
        void RequestHeightConfirmation()
        {
            if (confirmPanel == null) { CalibrateHeight(); return; }
            AwaitingConfirmation = true; pausedAt = Time.time;
            conveyor.SetSpeedImmediate(0); conveyor.SupplyEnabled = false;
            StatusText = "AI · 작업대 높이 확인 대기";
            confirmPanel.Show();
        }

        // YES: adjust the line height, then restart the belt once it has reached the new height.
        public void ConfirmHeightAdjustment()
        {
            if (!AwaitingConfirmation) return;
            CalibrateHeight();
            resumeAfterLift = true;
            StatusText = "AI · 체형에 맞게 작업대 높이 조정 중";
        }

        void Update()
        {
            if (!resumeAfterLift || (line != null && line.Moving)) return;
            resumeAfterLift = false; AwaitingConfirmation = false;
            pausedSeconds += Time.time - pausedAt;
            conveyor.TargetSpeed = baselineSpeed; conveyor.SupplyEnabled = true; controlTimer = controlInterval;
        }

        void Finish()
        {
            State = RunState.Finished; finishedAt = Time.time;
            conveyor.TargetSpeed = 0; conveyor.SupplyEnabled = false;
            StatusText = $"{ModeLabel} 완료 · {Elapsed:F0}초";
            LogTimeline();
            if (dataLogger != null) dataLogger.EndRun(Elapsed, deadlineSeconds, line != null ? line.TableTopHeight : 0);
        }

        // Phase 1 result: one line-height change, then the height stays for the rest of the run.
        void CalibrateHeight()
        {
            HeightCalibrated = true;
            if (line == null) return;
            // Plausibility: standing elbow height is about two thirds of eye height. A measurement
            // outside that band (tracking glitch, arms raised) falls back to the anthropometric value.
            float eye = calibration != null && calibration.IsCalibrated ? calibration.BaselineHead.y - calibration.standingPoint.position.y : 0;
            float measured = elbowCount > 0 ? elbowSum / elbowCount : 0;
            ElbowHeight = eye <= 0 ? measured : measured >= eye * .58f && measured <= eye * .74f ? measured : eye * .66f;
            if (ElbowHeight <= 0) return;
            float target = ElbowHeight - elbowClearance;
            line.SetTargetOffset(target - line.BaseTableTopHeight);
            Debug.Log($"[AI] Height calibrated: elbow {ElbowHeight:F3} m -> table top {target:F3} m (offset {line.TargetOffset:+0.000;-0.000} m)");
        }

        // Phase 2, every second: refit the pace-load model and choose the next belt speed.
        void Control()
        {
            FitLoadModel(samples, ridge, priorSlope, out float a, out float b);
            Alpha = a; Beta = b;
            float meanHandling = 0; foreach (var s in samples) meanHandling += s.y; meanHandling /= Mathf.Max(1, samples.Count);
            int remaining = RemainingToSupply();
            // The last part must be supplied, travel to the table and be handled before the deadline.
            float timeLeft = deadlineSeconds - Elapsed - meanHandling;
            RequiredSpeed = remaining == 0 ? baselineSpeed : timeLeft > .5f ? (Spacing * remaining + travelDistance) / timeLeft : maxSpeed;
            // Disturbance estimate: shift the model so it matches the load measured right now.
            float current = conveyor.CurrentSpeed;
            float bias = LiveLoad - (a * current + b);
            float urgency = Mathf.Min(10, quota / (float)Mathf.Max(1, remaining));
            OptimalSpeed = ChooseSpeed(a, b + bias, LivePeak - LiveLoad, RequiredSpeed, loadThreshold, w1, wDanger, w2, latePenalty * urgency, earlyPenalty, minSpeed, maxSpeed);
            conveyor.TargetSpeed = OptimalSpeed;
            // High load: slowed for recovery. Otherwise the belt follows the required speed, which is
            // above the baseline after a recovery phase (catching up) and around it otherwise.
            StatusText = OptimalSpeed < RequiredSpeed - .02f ? "AI · 부하 상승 → 감속 (회복)"
                       : OptimalSpeed > baselineSpeed + .02f ? "AI · 부하 여유 → 가속 (만회)"
                       : "AI · 목표 속도 유지";
        }

        // Parts still to be supplied: the quota minus parts completed and parts still in play.
        int RemainingToSupply()
        {
            int inPlay = 0;
            foreach (var part in spawner.Parts) if (part.gameObject.activeSelf && !part.Completed && !part.Vanishing) inPlay++;
            return Mathf.Max(0, quota - Completed - inPlay);
        }

        // Least-squares line L = a * v + b with a ridge term that pulls the slope toward a prior while
        // the speeds seen so far barely differ (data: x = belt speed, z = load).
        public static void FitLoadModel(IEnumerable<Vector3> data, float ridge, float prior, out float a, out float b)
        {
            float n = 0, sv = 0, sl = 0;
            foreach (var d in data) { n++; sv += d.x; sl += d.z; }
            if (n == 0) { a = prior; b = 0; return; }
            float mv = sv / n, ml = sl / n, cov = 0, var = 0;
            foreach (var d in data) { cov += (d.x - mv) * (d.z - ml); var += (d.x - mv) * (d.x - mv); }
            a = (cov + ridge * n * prior) / (var + ridge * n);
            b = ml - a * mv;
        }

        // argmin over v of J(v) = w1 L(v)/100 + wDanger (max(0, L(v) + peakGap - threshold)/100)^2
        //                       + w2 k(v) ((v - vReq)/vReq)^2, with L(v) = a v + b, k = late below vReq
        // and early above it. Evaluated on a fine grid between the speed limits.
        public static float ChooseSpeed(float a, float b, float peakGap, float vReq, float threshold, float w1, float wDanger, float w2, float late, float early, float min, float max)
        {
            if (vReq <= 0) return min;
            float best = vReq, bestCost = float.MaxValue;
            for (float v = min; v <= max + 1e-4f; v += .005f)
            {
                float load = a * v + b, over = Mathf.Max(0, load + peakGap - threshold) / 100, gap = (v - vReq) / vReq;
                float cost = w1 * load / 100 + wDanger * over * over + w2 * (v < vReq ? late : early) * gap * gap;
                if (cost < bestCost) { bestCost = cost; best = v; }
            }
            return best;
        }
    }
}
