using UnityEngine;

namespace FactoryTask
{
    public sealed class ConveyorController : MonoBehaviour
    {
        public const float FixedSpeed = .38f;
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
            if (!calibration.IsCalibrated || !tracking.HeadTracked || !tracking.Focused) return;
            queue.Tick(Time.fixedDeltaTime, FixedSpeed); spawner.Tick(Time.fixedDeltaTime);
            if (beltRenderer == null) return;
            // The authored belt top maps its longitudinal V coordinate as world X / 0.9.
            offset = Mathf.Repeat(offset - FixedSpeed * Time.fixedDeltaTime * textureST.y / .9f, 1);
            textureST.w = offset; block.SetVector(BaseMapST, textureST); beltRenderer.SetPropertyBlock(block);
        }
    }
}
