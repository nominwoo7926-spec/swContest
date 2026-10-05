using UnityEngine;

namespace FactoryTask
{
    // Prompt shown behind the worker after the AI mode's first 20 parts: the belt waits until the
    // worker turns around and touches YES with a controller. Headset-only (LocalUI layer).
    public sealed class HeightConfirmPanel : MonoBehaviour
    {
        public WorkSessionController session;
        public XRTrackingProvider tracking;
        public Transform yesButton;
        public float pressRadius = .08f, travel = .015f;
        Vector3 rest;
        bool down, restCaptured;

        public bool Visible => gameObject.activeSelf;

        public void Show()
        {
            if (!restCaptured) { rest = yesButton.localPosition; restCaptured = true; }
            gameObject.SetActive(true);
            // A hand already resting where the button appears does not count as a press.
            down = Touching();
        }
        public void Hide() { gameObject.SetActive(false); }

        void Update()
        {
            if (session == null || tracking == null) return;
            bool touching = Touching();
            // The panel faces the worker, so its local forward points away: pressing pushes the cap in.
            yesButton.localPosition = rest + Vector3.forward * (touching ? travel : 0);
            if (touching && !down) { Hide(); session.ConfirmHeightAdjustment(); }
            down = touching;
        }

        bool Touching()
        {
            foreach (var side in new[] { HandSide.Left, HandSide.Right })
                if (tracking.Tracked(side) && (tracking.Hand(side).position - yesButton.position).sqrMagnitude < pressRadius * pressRadius) return true;
            return false;
        }
    }
}
