using System;
using UnityEngine;
using UnityEngine.UI;

namespace FactoryTask
{
    [Serializable]
    public sealed class NumberReadout
    {
        public Image[] digits;
        int previous=-999;
        public void Set(int value,Sprite[] glyphs)
        {
            value=Mathf.Clamp(value,0,999);if(value==previous)return;previous=value;
            digits[0].enabled=value>=100;digits[1].enabled=value>=10;digits[2].enabled=true;
            digits[0].sprite=glyphs[value/100];digits[1].sprite=glyphs[(value/10)%10];digits[2].sprite=glyphs[value%10];
        }
    }
    public sealed class BodyLoadVisualizer : MonoBehaviour
    {
        public BodyLoadEstimator estimator;
        public XRGrabTaskTracker task;
        public UserCalibration calibration;
        public Image[] regions;
        public NumberReadout[] regionNumbers;
        public NumberReadout weight,average;
        public Sprite[] glyphs;
        public Image calibrationProgress;
        float nextUpdate;
        public static Color ColorFor(float score)
        {
            Color green=new Color(.18f,.85f,.48f),yellow=new Color(1,.8f,.13f),red=new Color(.97f,.23f,.22f);
            float v=Mathf.Clamp(score,0,100);return v<=50?Color.Lerp(green,yellow,v/50):Color.Lerp(yellow,red,(v-50)/50);
        }
        void Update()
        {
            if(Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.1f;
            calibrationProgress.fillAmount=calibration.IsCalibrated?1:calibration.Progress;
            calibrationProgress.color=calibration.IsCalibrated?ColorFor(0):new Color(.4f,.7f,1);
            for(int i=0;i<7;i++){regions[i].color=ColorFor(estimator.Score(i));regionNumbers[i].Set(Mathf.RoundToInt(estimator.Score(i)),glyphs);}
            weight.Set(Mathf.RoundToInt(task.HeldWeight),glyphs);average.Set(Mathf.RoundToInt(estimator.Average),glyphs);
        }
    }
}
