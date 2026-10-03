using UnityEngine;

namespace FactoryTask
{
    public sealed class PartSelectionControls : MonoBehaviour
    {
        public PartSpawner spawner;
        public XRTrackingProvider tracking;
        public Transform[] buttons;
        public Renderer[] indicators;
        public Material selectedMaterial,normalMaterial;
        readonly bool[] latched=new bool[3];
        readonly Vector3[] originalScales=new Vector3[3];
        void Awake()
        {
            if(buttons==null)return;
            for(int i=0;i<buttons.Length&&i<3;i++)if(buttons[i]!=null)originalScales[i]=buttons[i].localScale;
        }
        void Update()
        {
            if(spawner==null||tracking==null||buttons==null)return;
            for(int i=0;i<buttons.Length&&i<3;i++)
            {
                var button=buttons[i];if(button==null)continue;
                bool touching=VRButtonTouch.IsTouching(tracking,button);
                if(touching&&!latched[i])spawner.SetMode((PartSpawner.SupplyMode)i);
                latched[i]=touching;
            }
            int selected=(int)spawner.Mode;
            for(int i=0;i<buttons.Length&&i<3;i++)if(buttons[i]!=null)
                buttons[i].localScale=originalScales[i]*(i==selected ? .74f : latched[i] ? .88f : 1f);
            for(int i=0;i<indicators.Length;i++)if(indicators[i]!=null)indicators[i].sharedMaterial=i==selected?selectedMaterial:normalMaterial;
        }
    }
}
