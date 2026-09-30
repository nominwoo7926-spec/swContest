using UnityEngine;

namespace ErgoContest
{
    public sealed class BeforeAfterPoseGhost : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private ErgoAgentController agent;
        [SerializeField] private Transform workerRoot;
        [SerializeField] private Transform beforeHandGhost;
        [SerializeField] private Transform afterHandGhost;
        [SerializeField] private LineRenderer beforeReachLine;
        [SerializeField] private LineRenderer afterReachLine;

        private bool capturedBefore;
        private bool capturedAfter;

        private void Update()
        {
            if (inputProvider == null || agent == null)
                return;

            if (agent.HasBefore && !capturedBefore)
            {
                Capture(beforeHandGhost, beforeReachLine);
                capturedBefore = true;
                capturedAfter = false;
                if (afterHandGhost != null)
                    afterHandGhost.gameObject.SetActive(false);
            }

            if (agent.HasAfter && !capturedAfter)
            {
                Capture(afterHandGhost, afterReachLine);
                capturedAfter = true;
            }

            if (!agent.HasBefore)
                capturedBefore = false;
        }

        private void Capture(Transform marker, LineRenderer line)
        {
            Vector3 hand = inputProvider.CurrentFrame.HandPosition;
            Vector3 shoulder = workerRoot != null ? workerRoot.position + Vector3.up * 1.35f : hand + Vector3.down * 0.35f;
            if (marker != null)
            {
                marker.position = hand;
                marker.gameObject.SetActive(true);
            }
            if (line != null)
            {
                line.positionCount = 2;
                line.SetPosition(0, shoulder);
                line.SetPosition(1, hand);
            }
        }
    }
}
