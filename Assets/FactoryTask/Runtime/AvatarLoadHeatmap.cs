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
            // Only the displayed colour is normalised; the estimator's raw 0-100 scores are untouched.
            block.SetVector(LoadsA, new Vector4(MapColor(estimator.Score(0)), MapColor(estimator.Score(1)), MapColor(estimator.Score(2)), MapColor(estimator.Score(3))));
            block.SetVector(LoadsB, new Vector4(MapColor(estimator.Score(4)), MapColor(estimator.Score(5)), MapColor(estimator.Score(6)), 0));
            foreach (var surface in surfaces) surface.SetPropertyBlock(block);
        }

        // Based on Rohmert's curve and the RULA Action Levels, the practical fatigue threshold of a
        // raw score of 60 is mapped to the maximum risk colour (1.0).
        // Rohmert's Curve 및 RULA 조치 수준(Action Level)에 근거하여, 실질적 피로 임계점인 60점을 최대 위험도(1.0)로 매핑함.
        public const float FatigueThreshold = 60f;
        public static float MapColor(float score) => Mathf.InverseLerp(0f, FatigueThreshold, score);
    }
}
