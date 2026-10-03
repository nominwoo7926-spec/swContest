using UnityEngine;

namespace FactoryTask
{
    public sealed class VRFeedbackControls : MonoBehaviour
    {
        public ManufacturingDataCollector collector;
        public XRTrackingProvider tracking;
        public Transform[] buttons;
        public Renderer status;
        public Material savedMaterial,idleMaterial;
        readonly bool[] latched=new bool[5];
        readonly Vector3[] originalScales=new Vector3[5];
        float savedUntil;
        void Awake()
        {
            if(buttons==null)return;
            for(int i=0;i<buttons.Length&&i<5;i++)if(buttons[i]!=null)originalScales[i]=buttons[i].localScale;
        }
        void Update()
        {
            if(collector==null||tracking==null||buttons==null)return;
            for(int i=0;i<buttons.Length&&i<5;i++)
            {
                var button=buttons[i];if(button==null)continue;
                bool touching=VRButtonTouch.IsTouching(tracking,button);
                if(touching&&!latched[i]&&collector.SubmitFeedback(i+1))savedUntil=Time.unscaledTime+2;
                latched[i]=touching;button.localScale=originalScales[i]*(touching?.82f:1f);
            }
            if(status!=null)status.sharedMaterial=Time.unscaledTime<savedUntil?savedMaterial:idleMaterial;
        }
    }
}
