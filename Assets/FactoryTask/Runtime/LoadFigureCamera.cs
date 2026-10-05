using UnityEngine;

namespace FactoryTask
{
    // Renders the load-panel figure (a copy of the worker with the load heatmap) into its texture.
    // Ten updates a second is plenty for slowly changing colours and keeps the headset cost small.
    public sealed class LoadFigureCamera : MonoBehaviour
    {
        public Camera figureCamera;
        public float interval = .1f;
        float next;

        void Awake() { if (figureCamera != null) figureCamera.enabled = false; }
        void LateUpdate()
        {
            if (figureCamera == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            figureCamera.Render();
        }
    }
}
