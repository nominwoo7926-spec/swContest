using UnityEngine;

namespace ErgoContest
{
    public sealed class HandTrailVisualizer : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private int maxPoints = 72;
        [SerializeField] private float minPointDistance = 0.025f;

        private readonly Vector3[] points = new Vector3[96];
        private int count;

        private void Awake()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.useWorldSpace = true;
            }
        }

        private void Update()
        {
            if (inputProvider == null || lineRenderer == null)
                return;
            Vector3 hand = inputProvider.CurrentFrame.HandPosition;
            if (count > 0 && Vector3.Distance(points[count - 1], hand) < minPointDistance)
                return;
            if (count >= Mathf.Min(maxPoints, points.Length))
            {
                for (int i = 1; i < count; i++)
                    points[i - 1] = points[i];
                count--;
            }
            points[count++] = hand;
            lineRenderer.positionCount = count;
            for (int i = 0; i < count; i++)
                lineRenderer.SetPosition(i, points[i]);
        }
    }
}
