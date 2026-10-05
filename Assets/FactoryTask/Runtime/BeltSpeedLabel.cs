using UnityEngine;
using UnityEngine.UI;

namespace FactoryTask
{
    // Third-person overlay text: the current conveyor belt speed (live, or recorded while replaying).
    public sealed class BeltSpeedLabel : MonoBehaviour
    {
        public ConveyorController conveyor;
        public Text label;
        float next;
        string shown;

        void Update()
        {
            if (conveyor == null || label == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .2f;
            string text = "벨트 속도  " + conveyor.CurrentSpeed.ToString("0.00") + " m/s";
            if (text != shown) { label.text = text; shown = text; }
        }
    }
}
