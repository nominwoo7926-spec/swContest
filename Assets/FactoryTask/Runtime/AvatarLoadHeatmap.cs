using UnityEngine;

namespace FactoryTask
{
    [DefaultExecutionOrder(150)]
    public sealed class AvatarLoadHeatmap : MonoBehaviour
    {
        public BodyLoadEstimator estimator;
        public Renderer[] surfaces;
        static readonly int LoadsA = Shader.PropertyToID("_LoadsA"), LoadsB = Shader.PropertyToID("_LoadsB");
        MaterialPropertyBlock block;
        float next;
        void Awake() { block = new MaterialPropertyBlock(); }
        void LateUpdate()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .05f;
            Apply();
        }
        public void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.SetVector(LoadsA, new Vector4(estimator.Score(0), estimator.Score(1), estimator.Score(2), estimator.Score(3)) / 100);
            block.SetVector(LoadsB, new Vector4(estimator.Score(4), estimator.Score(5), estimator.Score(6), 0) / 100);
            foreach (var surface in surfaces) surface.SetPropertyBlock(block);
        }
    }
}
