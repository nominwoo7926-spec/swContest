using UnityEngine;

namespace FactoryTask
{
    // Short Y opens this contextual panel; holding Y for a second still recalibrates.
    // Only one five-button panel is visible at any time, and neither is filmed.
    public sealed class PersonalizationConsole : MonoBehaviour
    {
        enum Page { Height, Reach, Feedback, Report }
        public XRTrackingProvider tracking;
        public WorkerProfile profile;
        public ManufacturingDataCollector collector;
        public AdaptiveErgonomicsAgent agent;
        public BodyLoadEstimator estimator;
        public GameObject workConsole,menuConsole;
        public Transform[] buttons;
        public TextMesh[] buttonLabels;
        public TextMesh reportLabel;
        readonly bool[] latched=new bool[5];
        readonly Vector3[] originalScales=new Vector3[5];
        Page page;
        bool open,wasY;
        float pressedAt,nextLabel;
        string message="";

        void Awake()
        {
            if(buttons!=null)for(int i=0;i<Mathf.Min(5,buttons.Length);i++)
                if(buttons[i]!=null)originalScales[i]=buttons[i].localScale;
            SetOpen(false);
        }

        void Update()
        {
            if(tracking==null||profile==null)return;
            profile.SeedFromCalibration();
            bool y=tracking.Recalibrate;
            if(y&&!wasY)pressedAt=Time.unscaledTime;
            if(!y&&wasY&&Time.unscaledTime-pressedAt<.8f)
            {
                SetOpen(!open);
                if(open)SetPage(Page.Report);
            }
            wasY=y;
            if(!open||buttons==null)return;
            for(int i=0;i<Mathf.Min(5,buttons.Length);i++)
            {
                if(buttons[i]==null)continue;
                bool touching=VRButtonTouch.IsTouching(tracking,buttons[i]);
                if(touching&&!latched[i])Press(i);
                latched[i]=touching;
                buttons[i].localScale=originalScales[i]*(touching?.82f:1f);
            }
            if(Time.unscaledTime>=nextLabel)
            {
                nextLabel=Time.unscaledTime+.2f;
                RefreshReport();
            }
        }

        void SetOpen(bool value)
        {
            open=value;
            if(workConsole!=null)workConsole.SetActive(!value);
            if(menuConsole!=null)menuConsole.SetActive(value);
            for(int i=0;i<latched.Length;i++)latched[i]=false;
        }

        void SetPage(Page value)
        {
            page=value;message="";
            string[] labels=value==Page.Height||value==Page.Reach?
                new[]{"-10","-1","+1","+10","NEXT"}:
                value==Page.Feedback?new[]{"1","2","3","4","5"}:
                new[]{"APPLY","SKIP","HEIGHT","RATE","CLOSE"};
            for(int i=0;i<5&&buttonLabels!=null&&i<buttonLabels.Length;i++)
                if(buttonLabels[i]!=null)buttonLabels[i].text=labels[i];
            RefreshReport();
        }

        void Press(int index)
        {
            if(page==Page.Height)
            {
                if(index==4){SetPage(Page.Reach);return;}
                profile.SetHeight(profile.HeightCm+new[]{-10,-1,1,10}[index]);
            }
            else if(page==Page.Reach)
            {
                if(index==4){SetPage(Page.Report);return;}
                profile.SetReach(profile.ReachCm+new[]{-10,-1,1,10}[index]);
            }
            else if(page==Page.Feedback)
            {
                bool saved=collector!=null&&collector.SubmitFeedback(index+1);
                if(saved){SetPage(Page.Report);message="FEEDBACK SAVED: "+(index+1);}
                else message="WORK FIRST: NO SAMPLES";
            }
            else
            {
                if(index==0)message=agent!=null&&agent.ApproveProposal()?"SIM CHANGE APPROVED":"NO SAFE PROPOSAL";
                else if(index==1){if(agent!=null)agent.RejectProposal();message="PROPOSAL SKIPPED";}
                else if(index==2){SetPage(Page.Height);return;}
                else if(index==3){SetPage(Page.Feedback);return;}
                else {SetOpen(false);return;}
            }
            RefreshReport();
        }

        void RefreshReport()
        {
            if(reportLabel==null)return;
            if(page==Page.Height)
                reportLabel.text="PROFILE: HEIGHT\n"+profile.HeightCm+" cm "+(profile.HeightEntered?"ENTERED":"ESTIMATE")+"\nUse buttons, then NEXT";
            else if(page==Page.Reach)
                reportLabel.text="PROFILE: ARM REACH\n"+profile.ReachCm+" cm "+(profile.ReachEntered?"ENTERED":"ESTIMATE")+"\nShoulder to hand, then NEXT";
            else if(page==Page.Feedback)
                reportLabel.text="AFTER A WORK BLOCK\nRate discomfort 1 (low) to 5 (high)\n"+(collector==null?0:collector.PendingSamples)+" sensor seconds pending\n"+message;
            else
                reportLabel.text="WORK REPORT - ESTIMATE\n"+
                    "Height "+profile.HeightCm+"cm  Reach "+profile.ReachCm+"cm\n"+
                    "Load "+Mathf.RoundToInt(estimator==null?0:estimator.Average)+" /100  "+
                    (agent!=null&&agent.riskModel!=null&&agent.riskModel.IsLoaded?"ML VALIDATED":"RULE FALLBACK")+"\n"+
                    (agent!=null&&agent.HasProposal?
                        "Suggest table "+agent.ProposedTableHeight.ToString("0.00")+"m\nSuggest belt "+agent.ProposedSpeed.ToString("0.00")+"m/s":
                        "No pending change\n"+(agent==null?"NO AGENT":agent.Decision))+"\n"+
                    "Physical device: NOT CONNECTED\n"+message;
        }
    }
}
