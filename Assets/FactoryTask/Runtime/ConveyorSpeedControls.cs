using UnityEngine;

namespace FactoryTask
{
    public sealed class ConveyorSpeedControls : MonoBehaviour
    {
        public ConveyorController conveyor;
        public XRTrackingProvider tracking;
        public Transform slowerButton, fasterButton;
        public TextMesh speedLabel;
        public float step=.1f;
        bool slowerLatched,fasterLatched;
        Vector3 slowerScale,fasterScale;
        void Awake()
        {
            if(slowerButton!=null)slowerScale=slowerButton.localScale;
            if(fasterButton!=null)fasterScale=fasterButton.localScale;
        }
        void Update()
        {
            if(conveyor==null)return;
            if(speedLabel!=null)speedLabel.text="BELT "+conveyor.Speed.ToString("0.00")+" m/s";
            if(tracking==null||!tracking.Focused)return;
            Touch(slowerButton,slowerScale,-step,ref slowerLatched);
            Touch(fasterButton,fasterScale,step,ref fasterLatched);
        }
        void Touch(Transform button,Vector3 originalScale,float delta,ref bool latched)
        {
            if(button==null)return;
            bool touching=VRButtonTouch.IsTouching(tracking,button);
            if(touching&&!latched)conveyor.AdjustSpeed(delta);
            latched=touching;
            button.localScale=originalScale*(touching?.82f:1f);
        }
    }
}
