using UnityEngine;

namespace FactoryTask
{
    // Three colour-only push buttons, pressed by touching them with either controller:
    //   green = baseline mode, blue = AI-optimised mode (exclusive, like radio buttons),
    //   red   = start, or restart, the run in the selected mode.
    // The selected mode glows; red glows while a run is in progress. Headset-only (LocalUI layer).
    public sealed class ModeButtonPanel : MonoBehaviour
    {
        public WorkSessionController session;
        public XRTrackingProvider tracking;
        public Transform green, blue, red;
        public Renderer greenCap, blueCap, redCap;
        public float pressRadius = .07f, travel = .012f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly Color Green = new Color(.15f, .8f, .35f), Blue = new Color(.15f, .45f, .95f), Red = new Color(.92f, .2f, .18f);
        MaterialPropertyBlock block;
        Vector3 greenRest, blueRest, redRest;
        bool greenDown, blueDown, redDown;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            greenRest = green.localPosition; blueRest = blue.localPosition; redRest = red.localPosition;
        }

        void Update()
        {
            if (session == null || tracking == null) return;
            if (Pressed(green, greenRest, ref greenDown)) session.SelectMode(WorkSessionController.Mode.Baseline);
            if (Pressed(blue, blueRest, ref blueDown)) session.SelectMode(WorkSessionController.Mode.Optimized);
            if (Pressed(red, redRest, ref redDown)) session.StartRun();
            bool baseline = session.SelectedMode == WorkSessionController.Mode.Baseline;
            Paint(greenCap, Green, baseline);
            Paint(blueCap, Blue, !baseline);
            Paint(redCap, Red, session.State == WorkSessionController.RunState.Running);
        }

        // True on the frame a controller first touches the button; the cap sinks while touched.
        bool Pressed(Transform button, Vector3 rest, ref bool down)
        {
            bool touching = Touching(button, HandSide.Left) || Touching(button, HandSide.Right);
            button.localPosition = rest - Vector3.up * (touching ? travel : 0);
            bool pressed = touching && !down;
            down = touching;
            return pressed;
        }

        bool Touching(Transform button, HandSide side)
        {
            if (!tracking.Tracked(side)) return false;
            return (tracking.Hand(side).position - button.position).sqrMagnitude < pressRadius * pressRadius;
        }

        void Paint(Renderer cap, Color colour, bool lit)
        {
            cap.GetPropertyBlock(block);
            block.SetColor(BaseColor, lit ? colour : colour * .35f);
            block.SetColor(EmissionColor, lit ? colour * 1.6f : Color.black);
            cap.SetPropertyBlock(block);
        }
    }
}
