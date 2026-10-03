using UnityEngine;

namespace FactoryTask
{
    public sealed class ConveyorController : MonoBehaviour
    {
        public const float DefaultSpeed = .38f;
        public const float MinSpeed = .15f;
        public const float MaxSpeed = .75f;
        public float Speed { get; private set; } = DefaultSpeed;
        public float LastManualAdjustmentTime { get; private set; } = -999f;
        public PickupQueue queue;
        public PartSpawner spawner;
        public UserCalibration calibration;
        public XRTrackingProvider tracking;
        public Renderer beltRenderer;
        MaterialPropertyBlock block;
        Vector4 textureST;
        float offset;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (beltRenderer != null) { Vector2 scale = beltRenderer.sharedMaterial.mainTextureScale; textureST = new Vector4(scale.x, scale.y, 0, 0); }
        }
        void FixedUpdate()
        {
            // The conveyor itself keeps running even while tracking/calibration is interrupted.
            if (calibration.IsCalibrated && tracking.HeadTracked && tracking.Focused)
            { queue.Tick(Time.fixedDeltaTime, Speed); spawner.Tick(Time.fixedDeltaTime); }
            if (beltRenderer == null) return;
            // The authored belt top maps its longitudinal V coordinate as world X / 0.9.
            offset = Mathf.Repeat(offset - Speed * Time.fixedDeltaTime * textureST.y / .9f, 1);
            textureST.w = offset; block.SetVector(BaseMapST, textureST);
            // Scroll the existing tread texture; speed must not recolor the belt.
            beltRenderer.SetPropertyBlock(block);
        }
        public void AdjustSpeed(float delta,bool manual=true)
        {
            Speed=Mathf.Clamp(Speed+delta,MinSpeed,MaxSpeed);
            if(manual)LastManualAdjustmentTime=Time.unscaledTime;
        }
        public void ApplyRecordedSpeed(float speed){Speed=Mathf.Clamp(speed,MinSpeed,MaxSpeed);}
    }
}
