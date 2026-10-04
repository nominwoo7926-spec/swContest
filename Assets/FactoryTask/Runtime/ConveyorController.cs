using UnityEngine;

namespace FactoryTask
{
    public sealed class ConveyorController : MonoBehaviour
    {
        public const float FixedSpeed = .38f;
        public PartSpawner spawner;
        public UserCalibration calibration;
        public XRTrackingProvider tracking;
        public BodyLoadEstimator estimator;
        public XRGrabTaskTracker task;
        public Renderer beltRenderer;
        // Frictionless static colliders the belt drives parts across (upper belt, end drum).
        public Collider[] driveSurfaces;
        public Vector3 beltDirection = Vector3.right;
        // Maximum belt grip, like friction: a blocked part slips instead of being forced through.
        public float beltGrip = 5f;

        // 최고 부위 부하 ≥50% 가 4초 지속되면 감속 시작, 5초에 걸쳐 45%까지 감소
        const float LoadThreshold = 50f;
        const float SlowdownDelay = 4f;
        const float SlowdownRamp  = 5f;
        const float MinSpeedRatio = 0.45f;

        public float CurrentSpeed { get; private set; } = FixedSpeed;
        public float SlowdownRatio => 1f - CurrentSpeed / FixedSpeed;

        float highLoadTimer;
        bool wasSlowing;
        MaterialPropertyBlock block;
        Vector4 textureST;
        float offset;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (beltRenderer != null)
            {
                Vector2 scale = beltRenderer.sharedMaterial.mainTextureScale;
                textureST = new Vector4(scale.x, scale.y, 0, 0);
            }
        }

        void FixedUpdate()
        {
            bool running = calibration.IsCalibrated && tracking.HeadTracked && tracking.Focused;
            // A paused belt still grips its parts so they do not coast on the frictionless surface.
            Drive(running ? CurrentSpeed : 0, Time.fixedDeltaTime);
            if (!running) return;
            UpdateSpeed(Time.fixedDeltaTime);
            spawner.Tick(Time.fixedDeltaTime);
            if (beltRenderer == null) return;
            offset = Mathf.Repeat(offset - CurrentSpeed * Time.fixedDeltaTime * textureST.y / .9f, 1);
            textureST.w = offset; block.SetVector(BaseMapST, textureST); beltRenderer.SetPropertyBlock(block);
        }

        void Drive(float speed, float dt)
        {
            if (spawner.Parts == null || driveSurfaces == null || dt <= 0) return;
            Vector3 along = beltDirection.normalized;
            for (int i = 0; i < spawner.Parts.Length; i++)
            {
                var part = spawner.Parts[i];
                if (!part.Free || part.Body.isKinematic || !OnBelt(part.Shape.bounds)) continue;
                // Accelerate the contact toward belt velocity, limited like belt friction.
                Vector3 slip = along * speed - part.Body.linearVelocity; slip.y = 0;
                part.Body.AddForce(Vector3.ClampMagnitude(slip / dt, beltGrip), ForceMode.Acceleration);
            }
        }

        bool OnBelt(Bounds part)
        {
            for (int i = 0; i < driveSurfaces.Length; i++)
            {
                Bounds belt = driveSurfaces[i].bounds;
                if (part.min.y > belt.max.y + .02f || part.min.y < belt.max.y - .06f) continue;
                // Any footprint still resting on the belt or drum is driven, so a part keeps moving
                // until its trailing edge has left the drum and it sits fully on the table.
                Vector3 c = part.center;
                if (part.max.x >= belt.min.x && part.min.x <= belt.max.x && c.z >= belt.min.z && c.z <= belt.max.z) return true;
            }
            return false;
        }

        void UpdateSpeed(float dt)
        {
            if (estimator == null) { CurrentSpeed = FixedSpeed; return; }

            // 실제로 들고 있는 동안만 누적 (displayed는 10s 감쇠 지연 → 내려놓아도 Max가 높게 유지됨)
            bool activeLoad = estimator.Max >= LoadThreshold && task != null && task.HeldWeight > 0;
            if (activeLoad)
                highLoadTimer += dt;
            else
                highLoadTimer = Mathf.Max(0, highLoadTimer - dt);

            float t = Mathf.Clamp01((highLoadTimer - SlowdownDelay) / SlowdownRamp);
            bool isSlowing = t > 0;
            if (isSlowing && !wasSlowing) Debug.Log($"[Belt] 감속 시작 — 최고부하 {estimator.Max:F0}% (타이머 {highLoadTimer:F1}s)");
            if (!isSlowing && wasSlowing) Debug.Log("[Belt] 정상 속도 복귀");
            wasSlowing = isSlowing;

            float target = Mathf.Lerp(FixedSpeed, FixedSpeed * MinSpeedRatio, t);
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, target, 1 - Mathf.Exp(-0.5f * dt));
        }
    }
}
