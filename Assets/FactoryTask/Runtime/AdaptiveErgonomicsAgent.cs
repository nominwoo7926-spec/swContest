using System.Collections.Generic;
using UnityEngine;

namespace FactoryTask
{
    // Goal: keep observed physical load and queueing low while maintaining throughput.
    // Decisions are transparent heuristics until a validated data model is trained.
    public sealed class AdaptiveErgonomicsAgent : MonoBehaviour
    {
        public BodyLoadEstimator estimator;
        public ConveyorController conveyor;
        public PickupQueue queue;
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public XRGrabTaskTracker task;
        public AdjustableWorktable table;
        public ManufacturingDataCollector collector;
        public WorkerProfile profile;
        public Transform toggleButton;
        public Renderer indicator;
        public TextMesh statusLabel;
        public Material normalMaterial,watchMaterial,interventionMaterial,disabledMaterial;
        public ErgonomicsRiskModel riskModel;
        public bool automatic = true;
        public bool approvalRequired;
        public float sampleInterval = 1f;
        float nextSample,filteredLoad,highSeconds,safeSeconds,nextSpeedAction,nextTableAction;
        struct PendingOutcome
        {
            public int id;
            public string reason;
            public float due,loadBefore;
            public int queueBefore,completedBefore;
        }
        readonly List<PendingOutcome> outcomes=new List<PendingOutcome>(2);
        int nextActionId;
        int evaluatedPickupCount;
        float pickupTarget = -1;
        string outcomeReason;
        bool toggleLatched,feedbackPending;
        Vector3 toggleScale;
        public int LastFeedback { get; private set; }
        public float FilteredLoad => filteredLoad;
        public int PendingOutcomeCount => outcomes.Count;
        public string Decision { get; private set; } = "MONITORING";
        public bool HasProposal { get; private set; }
        public float ProposedTableHeight { get; private set; }
        public float ProposedSpeed { get; private set; }
        public string ProposalReason { get; private set; }

        void Awake(){if(toggleButton!=null)toggleScale=toggleButton.localScale;}

        void Update()
        {
            HandleToggle();
            if(Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+sampleInterval;
            ObserveAndAct(sampleInterval);
            Present();
        }

        void HandleToggle()
        {
            if(toggleButton==null||tracking==null)return;
            bool touching=VRButtonTouch.IsTouching(tracking,toggleButton);
            if(touching&&!toggleLatched){automatic=!automatic;Decision=automatic?"AUTO ON":"MANUAL";highSeconds=safeSeconds=0;}
            toggleLatched=touching;
            toggleButton.localScale=toggleScale*(touching?.82f:1f);
        }

        public void RecordFeedback(int score)
        {
            LastFeedback=Mathf.Clamp(score,1,5);
            feedbackPending=LastFeedback>=4;
            if(collector!=null)collector.LogAgentEvent("FEEDBACK",LastFeedback.ToString(),filteredLoad);
        }

        public void ObserveAndAct(float dt)
        {
            if(estimator==null||conveyor==null||queue==null||tracking==null||calibration==null||
               !calibration.IsCalibrated||!tracking.AllTracked||!tracking.Focused)
            {
                if(Decision!="SENSOR WAIT"&&collector!=null)collector.LogAgentEvent("SENSOR","TRACKING_OR_CALIBRATION_UNAVAILABLE",filteredLoad);
                foreach(var outcome in outcomes)
                    if(collector!=null)collector.LogAgentEvent("OUTCOME_INVALID","id="+outcome.id+" action="+outcome.reason+" tracking_lost",filteredLoad);
                outcomes.Clear();
                HasProposal=false;
                Decision="SENSOR WAIT";
                highSeconds=safeSeconds=0;
                return;
            }
            float peak=0;
            for(int i=0;i<7;i++)peak=Mathf.Max(peak,estimator.Score(i));
            filteredLoad=Mathf.Lerp(filteredLoad,peak,1-Mathf.Exp(-dt/5f));
            bool congested=queue.Count>=10;
            float leftReach=tracking.LeftTracked?Vector3.Distance(tracking.leftHand.position,calibration.Shoulder(HandSide.Left)):0;
            float rightReach=tracking.RightTracked?Vector3.Distance(tracking.rightHand.position,calibration.Shoulder(HandSide.Right)):0;
            float risk=0;
            bool predicted=riskModel!=null&&riskModel.TryPredict(new[]{peak,estimator.Average,estimator.Score(6),task!=null?task.HeldWeight:0,
                queue.Count/(float)Mathf.Max(1,queue.Capacity),Mathf.InverseLerp(ConveyorController.MinSpeed,ConveyorController.MaxSpeed,conveyor.Speed),leftReach,rightReach,
                table==null?AdjustableWorktable.DefaultHeight:table.Height,
                profile==null?0:profile.HeightCm,profile==null?0:profile.ReachCm},out risk);
            bool high=predicted?risk>=.7f:filteredLoad>=65;
            bool safe=filteredLoad<=32&&queue.Count<=4&&LastFeedback<4;
            highSeconds=high?highSeconds+dt:Mathf.Max(0,highSeconds-dt);
            safeSeconds=safe?safeSeconds+dt:0;
            for(int i=outcomes.Count-1;i>=0;i--)
            {
                var outcome=outcomes[i];
                if(Time.unscaledTime<outcome.due)continue;
                if(collector!=null)collector.LogAgentEvent("OUTCOME","id="+outcome.id+" action="+outcome.reason+
                    " load_before="+Mathf.RoundToInt(outcome.loadBefore)+" load_after="+Mathf.RoundToInt(filteredLoad)+
                    " queue_before="+outcome.queueBefore+" queue_after="+queue.Count+
                    " completed_before="+outcome.completedBefore+" completed_after="+(task==null?0:task.CompletedTotal),filteredLoad);
                outcomes.RemoveAt(i);
            }
            if(!automatic){Decision="MANUAL";return;}
            if(approvalRequired&&HasProposal){Decision="AWAIT APPROVAL";return;}
            bool manualSpeed=Time.unscaledTime-conveyor.LastManualAdjustmentTime<20;
            bool manualTable=table!=null&&Time.unscaledTime-table.LastManualAdjustmentTime<20;
            string action="";
            if(table!=null&&!manualTable&&Time.unscaledTime>=nextTableAction&&!table.Moving)
            {
                float desired=DesiredTableHeight();
                if(Mathf.Abs(desired-table.TargetHeight)>=.045f)
                {
                    float before=table.TargetHeight;
                    if(approvalRequired)
                    {
                        SetProposal(desired,conveyor.Speed,desired>before?"TABLE RAISE":"TABLE LOWER");
                        Decision="REVIEW TABLE";return;
                    }
                    table.SetTargetHeight(desired);
                    nextTableAction=Time.unscaledTime+8;
                    action=desired>before?"TABLE RAISE":"TABLE LOWER";
                    LogAction(action);
                }
            }
            if(!manualSpeed&&Time.unscaledTime>=nextSpeedAction)
            {
                string speedAction=null;
                float delta=0;
                if(congested){delta=-.1f;speedAction="QUEUE HIGH - SLOW";safeSeconds=0;}
                else if(highSeconds>=5){delta=-.1f;speedAction=predicted?"ML RISK - SLOW":"LOAD HIGH - SLOW";highSeconds=0;}
                else if(feedbackPending){delta=-.05f;speedAction="DISCOMFORT - SLOW";feedbackPending=false;}
                else if(safeSeconds>=10&&conveyor.Speed<ConveyorController.DefaultSpeed)
                {delta=.05f;speedAction="LOAD SAFE - RECOVER";safeSeconds=0;}
                if(speedAction!=null)
                {
                    if(approvalRequired&&Mathf.Abs(Mathf.Clamp(conveyor.Speed+delta,ConveyorController.MinSpeed,ConveyorController.MaxSpeed)-conveyor.Speed)>.001f)
                    {
                        SetProposal(table==null?0:table.TargetHeight,Mathf.Clamp(conveyor.Speed+delta,ConveyorController.MinSpeed,ConveyorController.MaxSpeed),speedAction);
                        Decision="REVIEW SPEED";return;
                    }
                    float before=conveyor.Speed;
                    conveyor.AdjustSpeed(delta,false);
                    nextSpeedAction=Time.unscaledTime+8;
                    if(Mathf.Abs(before-conveyor.Speed)>.001f){action=action.Length==0?speedAction:action+" + SPEED";LogAction(speedAction);}
                }
            }
            Decision=action.Length>0?action:manualSpeed||manualTable?"MANUAL OVERRIDE":high?predicted?"ML RISK WATCH":"LOAD WATCH":
                congested?"QUEUE WATCH":predicted?"ML MONITORING":"MONITORING";
        }

        float DesiredTableHeight()
        {
            float shoulder=calibration.BaselineHead.y-calibration.ShoulderDrop;
            if(profile!=null&&profile.HeightEntered)
                shoulder=Mathf.Lerp(shoulder,calibration.standingPoint.position.y+profile.HeightCm*.01f-.30f,.35f);
            float desired=shoulder-.60f;
            // A tracked pickup corrects the initial stature estimate using this worker's hand height.
            if(task!=null&&task.PickupCount>evaluatedPickupCount)
            {
                evaluatedPickupCount=task.PickupCount;
                float preferredGrip=shoulder-.54f;
                pickupTarget=table.Height+Mathf.Clamp(preferredGrip-task.LastPickupHandHeight,-.15f,.15f);
                if(task.LastPickupHeadDrop>.12f)pickupTarget+=Mathf.Min(.08f,(task.LastPickupHeadDrop-.12f)*.3f);
            }
            if(pickupTarget>0)desired=pickupTarget;
            return Mathf.Clamp(desired,AdjustableWorktable.MinHeight,AdjustableWorktable.MaxHeight);
        }

        void SetProposal(float height,float speed,string reason)
        {
            ProposedTableHeight=height;ProposedSpeed=speed;ProposalReason=reason;HasProposal=true;
            if(collector!=null)collector.LogAgentEvent("PROPOSAL",reason,filteredLoad);
        }

        public bool ApproveProposal()
        {
            if(!HasProposal||!automatic||tracking==null||!tracking.AllTracked||
               calibration==null||!calibration.IsCalibrated)return false;
            string reason=ProposalReason;
            if(table!=null&&Mathf.Abs(ProposedTableHeight-table.TargetHeight)>.005f)
            {
                table.SetTargetHeight(ProposedTableHeight);
                nextTableAction=Time.unscaledTime+8;
            }
            if(Mathf.Abs(ProposedSpeed-conveyor.Speed)>.005f)
            {
                conveyor.AdjustSpeed(ProposedSpeed-conveyor.Speed,false);
                nextSpeedAction=Time.unscaledTime+8;
            }
            HasProposal=false;Decision="APPROVED "+reason;
            LogAction(reason);
            if(collector!=null)collector.LogAgentEvent("APPROVED",reason,filteredLoad);
            return true;
        }

        public void RejectProposal()
        {
            if(!HasProposal)return;
            if(collector!=null)collector.LogAgentEvent("REJECTED",ProposalReason,filteredLoad);
            HasProposal=false;Decision="REJECTED";
            nextTableAction=nextSpeedAction=Time.unscaledTime+20;
        }

        void LogAction(string reason)
        {
            int id=++nextActionId;
            if(collector!=null)collector.LogAgentEvent("ACTION","id="+id+" action="+reason,filteredLoad);
            outcomes.Add(new PendingOutcome{id=id,reason=reason,due=Time.unscaledTime+5,
                loadBefore=filteredLoad,queueBefore=queue.Count,completedBefore=task==null?0:task.CompletedTotal});
        }

        void Present()
        {
            if(statusLabel!=null)statusLabel.text=(automatic?"AUTO":"MANUAL")+"  "+Decision+
                "\nLOAD "+Mathf.RoundToInt(filteredLoad)+"  BELT "+(conveyor==null?"--":conveyor.Speed.ToString("0.00"))+
                "\nTABLE "+(table==null?"--":table.Height.ToString("0.00"))+" -> "+(table==null?"--":table.TargetHeight.ToString("0.00"));
            if(indicator!=null)indicator.sharedMaterial=!automatic?disabledMaterial:Decision.Contains("SLOW")||Decision.Contains("TABLE")?interventionMaterial:
                Decision.Contains("WATCH")||Decision=="SENSOR WAIT"?watchMaterial:normalMaterial;
        }
    }
}
