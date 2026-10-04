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
            if(digits==null||digits.Length<3||glyphs==null||glyphs.Length<10)return;
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
        public Image[] regionBars;   // 각 신체 부위 가로 진행 바 (fill image)
        public Image averageBar;     // 상단 전체 평균 바

        [Header("벨트 속도 (선택 — 미연결 시 무시됨)")]
        public ConveyorController conveyor;
        public Image beltSpeedBar;
        public NumberReadout beltSpeedPct;

        [Header("확장 HUD (선택 — 미연결 시 무시됨)")]
        public ErgonomicReport report;
        public VRSessionRecorder recorder;
        public Image reportHoldProgress;
        public NumberReadout sessionMinutes;
        public NumberReadout sessionSeconds;
        public NumberReadout completedCount;
        public Image dangerOverlay;

        float nextUpdate;
        float dangerFlashTick;

        public static Color ColorFor(float score)
        {
            Color green=new Color(.18f,.85f,.48f),yellow=new Color(1,.8f,.13f),red=new Color(.97f,.23f,.22f);
            float v=Mathf.Clamp(score,0,100);return v<=50?Color.Lerp(green,yellow,v/50):Color.Lerp(yellow,red,(v-50)/50);
        }

        void Update()
        {
            if(Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.1f;

            // ── 기존 HUD ──────────────────────────────────────────────
            calibrationProgress.fillAmount=calibration.IsCalibrated?1:calibration.Progress;
            calibrationProgress.color=calibration.IsCalibrated?ColorFor(0):new Color(.4f,.7f,1);
            float avg=estimator.Average;
            for(int i=0;i<7;i++)
            {
                float s=estimator.Score(i);
                Color col=ColorFor(s);
                regions[i].color=col;
                regionNumbers[i].Set(Mathf.RoundToInt(s),glyphs);
                if(regionBars!=null&&i<regionBars.Length&&regionBars[i]!=null)
                {
                    regionBars[i].fillAmount=s/100f;
                    Color bc=col;
                    bc.a=s>=67f?0.65f+Mathf.Abs(Mathf.Sin(Time.unscaledTime*5f))*0.35f:1f;
                    regionBars[i].color=bc;
                }
            }
            if(averageBar!=null){averageBar.fillAmount=avg/100f;averageBar.color=ColorFor(avg);}
            weight.Set(Mathf.RoundToInt(task.HeldWeight),glyphs);average.Set(Mathf.RoundToInt(avg),glyphs);

            // ── 확장 HUD ──────────────────────────────────────────────

            // B버튼 보고서 홀드 진행률
            if(reportHoldProgress!=null&&report!=null)
                reportHoldProgress.fillAmount=report.HoldProgress;

            // 세션 경과 시간
            if(recorder!=null)
            {
                int total=Mathf.FloorToInt(recorder.Duration);
                if(sessionMinutes!=null)sessionMinutes.Set(total/60,glyphs);
                if(sessionSeconds!=null)sessionSeconds.Set(total%60,glyphs);
            }

            // 완료 부품 수
            if(completedCount!=null)completedCount.Set(task.CompletedTotal,glyphs);

            // 벨트 속도 바 (ratio=1→녹색/가득, ratio<1→주황·빨강/줄어듦)
            if(conveyor!=null&&beltSpeedBar!=null)
            {
                float ratio=conveyor.CurrentSpeed/ConveyorController.FixedSpeed;
                beltSpeedBar.fillAmount=ratio;
                beltSpeedBar.color=ColorFor((1f-ratio)*200f);
                if(beltSpeedPct!=null)beltSpeedPct.Set(Mathf.RoundToInt(ratio*100f),glyphs);
            }

            // 위험 부하 경고 점멸 (평균 ≥67 일 때 오버레이 alpha를 sin파로)
            if(dangerOverlay!=null)
            {
                bool danger=estimator.Average>=67f;
                if(danger)
                {
                    dangerFlashTick+=.1f;
                    dangerOverlay.enabled=true;
                    Color c=dangerOverlay.color;
                    c.a=Mathf.Abs(Mathf.Sin(dangerFlashTick*3f))*0.4f;
                    dangerOverlay.color=c;
                }
                else
                {
                    dangerOverlay.enabled=false;
                    dangerFlashTick=0;
                }
            }
        }
    }
}
