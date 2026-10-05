using UnityEngine;
using UnityEngine.UI;

namespace FactoryTask
{
    // Load panel shown in the headset and copied onto the spectator view: a small front view of the
    // worker model with the live load colours on its body (rendered by LoadFigureCamera), nothing else.
    public sealed class BodyLoadVisualizer : MonoBehaviour
    {
        public UserCalibration calibration;
        public RawImage figure;
        // Ring that fills while the user stands still for calibration, hidden once calibrated.
        public Image calibrationProgress;
        float nextUpdate;

        public static Color ColorFor(float score)
        {
            Color green=new Color(.18f,.85f,.48f),yellow=new Color(1,.8f,.13f),red=new Color(.97f,.23f,.22f);
            float v=Mathf.Clamp(score,0,100);return v<=50?Color.Lerp(green,yellow,v/50):Color.Lerp(yellow,red,(v-50)/50);
        }

        void Update()
        {
            if(Time.unscaledTime<nextUpdate||calibration==null||calibrationProgress==null)return;
            nextUpdate=Time.unscaledTime+.1f;
            calibrationProgress.enabled=!calibration.IsCalibrated;
            calibrationProgress.fillAmount=calibration.Progress;
        }
    }
}
