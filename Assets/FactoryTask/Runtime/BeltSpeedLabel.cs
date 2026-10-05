using UnityEngine;
using UnityEngine.UI;

namespace FactoryTask
{
    // Third-person overlay text: the run mode, the conveyor belt speed and what the AI is doing.
    public sealed class BeltSpeedLabel : MonoBehaviour
    {
        public ConveyorController conveyor;
        public WorkSessionController session;
        public Text label;
        float next;
        string shown;

        void Update()
        {
            if (conveyor == null || label == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .2f;
            string text = "벨트 속도  " + conveyor.CurrentSpeed.ToString("0.00") + " m/s";
            if (session != null)
            {
                string head = session.ModeLabel;
                if (session.State != WorkSessionController.RunState.Waiting) head += $"  ·  {session.Completed}/{session.quota}개  ·  {session.Elapsed:0}초";
                text = head + "\n" + text + "\n" + session.StatusText;
            }
            if (text != shown) { label.text = text; shown = text; }
        }
    }
}
