using UnityEngine;

namespace FactoryTask
{
    // Physical belt: drives parts on its surface and runs the part supply while a work run is active.
    // The belt speed is commanded by WorkSessionController (fixed in the baseline mode, optimised in
    // the AI mode) and eased toward its target so changes are never abrupt.
    public sealed class ConveyorController : MonoBehaviour
    {
        public const float FixedSpeed = .38f;
        public PartSpawner spawner;
        public UserCalibration calibration;
        public XRTrackingProvider tracking;
        public Renderer beltRenderer;
        // Frictionless static colliders the belt drives parts across (upper belt, end drum).
        public Collider[] driveSurfaces;
        public Vector3 beltDirection = Vector3.right;
        // Maximum belt grip, like friction: a blocked part slips instead of being forced through.
        public float beltGrip = 5f;
        [Tooltip("Grip on a part that is stopped by the queue ahead.")]
        public float stalledGrip = 1.5f;
        [Tooltip("Time constant (s) of the exponential easing from the current to the target speed.")]
        public float speedSmoothing = 2f;

        // Set by the work session; 0 keeps the belt stopped.
        public float TargetSpeed { get; set; }
        // Whether new parts are supplied (a run is in progress).
        public bool SupplyEnabled { get; set; }
        public float CurrentSpeed { get; private set; }

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
            float dt = Time.fixedDeltaTime;
            bool running = calibration.IsCalibrated && tracking.HeadTracked && tracking.Focused;
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, running ? TargetSpeed : 0, 1 - Mathf.Exp(-dt / Mathf.Max(.01f, speedSmoothing)));
            // A paused belt still grips its parts so they do not coast on the frictionless surface.
            Drive(CurrentSpeed, dt);
            if (running && SupplyEnabled) spawner.Tick(dt);
            if (beltRenderer == null) return;
            offset = Mathf.Repeat(offset - CurrentSpeed * dt * textureST.y / .9f, 1);
            textureST.w = offset; block.SetVector(BaseMapST, textureST); beltRenderer.SetPropertyBlock(block);
        }

        // Stops the belt at once (used when a run is restarted).
        public void StopImmediately() { CurrentSpeed = 0; TargetSpeed = 0; SupplyEnabled = false; }
        // Starts a run already at speed, so the first parts arrive at the run's exact spacing.
        public void SetSpeedImmediate(float speed) { CurrentSpeed = TargetSpeed = speed; }

        void Drive(float speed, float dt)
        {
            if (spawner.Parts == null || driveSurfaces == null || dt <= 0) return;
            Vector3 along = beltDirection.normalized;
            for (int i = 0; i < spawner.Parts.Length; i++)
            {
                var part = spawner.Parts[i];
                if (!part.Free || part.Body.isKinematic || !OnBelt(part.Shape.bounds)) continue;
                // Accelerate the contact toward belt velocity, limited like belt friction. A part that is
                // held up by the queue ahead gets only a light push (an accumulating conveyor), so a
                // long backed-up line does not press the front parts into each other.
                Vector3 slip = along * speed - part.Body.linearVelocity; slip.y = 0;
                float moving = speed > .01f ? Mathf.Clamp01(Vector3.Dot(part.Body.linearVelocity, along) / speed) : 1;
                part.Body.AddForce(Vector3.ClampMagnitude(slip / dt, Mathf.Lerp(stalledGrip, beltGrip, moving)), ForceMode.Acceleration);
            }
        }

        public bool OnBelt(Bounds part)
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
    }
}
