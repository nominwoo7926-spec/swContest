using UnityEngine;

namespace FactoryTask
{
    public sealed class TableHeightControls : MonoBehaviour
    {
        public AdjustableWorktable table;
        public XRTrackingProvider tracking;
        public Transform lowerButton, raiseButton;
        public float step = .10f;
        bool lowerLatched, raiseLatched;
        Vector3 lowerScale, raiseScale;

        void Awake()
        {
            if(lowerButton!=null)lowerScale=lowerButton.localScale;
            if(raiseButton!=null)raiseScale=raiseButton.localScale;
        }

        void Update()
        {
            if (table == null || tracking == null || !tracking.Focused) return;
            Touch(lowerButton, lowerScale, -step, ref lowerLatched);
            Touch(raiseButton, raiseScale, step, ref raiseLatched);
        }

        void Touch(Transform button, Vector3 originalScale, float delta, ref bool latched)
        {
            if (button == null) return;
            bool touching = VRButtonTouch.IsTouching(tracking, button);
            if (touching && !latched) table.SetTargetHeight(table.TargetHeight + delta, true);
            latched = touching;
            button.localScale = originalScale * (touching ? .82f : 1f);
        }
    }
}
