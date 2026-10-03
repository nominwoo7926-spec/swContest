using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FactoryTask
{
    public sealed class ManufacturingDataCollector : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public BodyLoadEstimator estimator;
        public XRGrabTaskTracker task;
        public ConveyorController conveyor;
        public PickupQueue queue;
        public PartSpawner spawner;
        public AdaptiveErgonomicsAgent agent;
        public AdjustableWorktable table;
        public WorkerProfile profile;
        public float intervalSeconds=1f;
        public int PendingSamples=>pending.Count;
        public string SessionId=>sessionId;
        public string LastSavedPath { get; private set; }
        readonly List<string> pending=new List<string>(600);
        float nextSample,started;
        int trial;
        Vector3 previousLeft,previousRight;
        bool havePrevious;
        string sessionId;
        static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        public static string DataDirectory=>Path.Combine(Application.persistentDataPath,"FactoryML");
        const string Header="session,profile_id,height_cm,reach_cm,height_entered,reach_entered,trial,time,head_drop,head_forward,left_reach,right_reach,left_speed,right_speed,held_weight,belt_speed,table_height,table_target,pickup_hand_height,pickup_head_drop,queue_count,part_mode,load_l_shoulder,load_r_shoulder,load_l_arm,load_r_arm,load_l_wrist,load_r_wrist,load_torso,load_average,completed,agent_auto,agent_decision,feedback";
        const string EventHeader="session,profile_id,trial,time,phase,reason,load,queue_count,belt_speed,table_height,table_target,sensor_valid,model_loaded";

        void Awake(){sessionId=(Application.isEditor?"editor_synthetic_":"")+
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",Invariant)+"_"+Guid.NewGuid().ToString("N").Substring(0,6);}

        void Update()
        {
            if(calibration==null||!calibration.IsCalibrated||tracking==null||!tracking.AllTracked||Time.unscaledTime<nextSample)return;
            if(started==0)started=Time.unscaledTime;
            nextSample=Time.unscaledTime+intervalSeconds;
            Capture();
            if(pending.Count>=3600)SaveUnlabeled();
        }
        void Capture()
        {
            Vector3 left=tracking.leftHand.position,right=tracking.rightHand.position;
            float ls=havePrevious?Vector3.Distance(left,previousLeft)/Mathf.Max(.01f,intervalSeconds):0;
            float rs=havePrevious?Vector3.Distance(right,previousRight)/Mathf.Max(.01f,intervalSeconds):0;
            previousLeft=left;previousRight=right;havePrevious=true;
            Vector3 headDelta=tracking.head.position-calibration.BaselineHead;
            float drop=Mathf.Max(0,-headDelta.y),forward=Vector3.Dot(headDelta,calibration.Forward);
            float leftReach=Vector3.Distance(left,calibration.Shoulder(HandSide.Left));
            float rightReach=Vector3.Distance(right,calibration.Shoulder(HandSide.Right));
            var values=new List<string>(34){sessionId,profile==null?"NONE":profile.Id,
                profile==null?"0":profile.HeightCm.ToString(Invariant),profile==null?"0":profile.ReachCm.ToString(Invariant),
                profile!=null&&profile.HeightEntered?"1":"0",profile!=null&&profile.ReachEntered?"1":"0",
                trial.ToString(Invariant),F(Time.unscaledTime-started),F(drop),F(forward),F(leftReach),F(rightReach),F(ls),F(rs),F(task.HeldWeight),F(conveyor.Speed),F(table==null?0:table.Height),F(table==null?0:table.TargetHeight),F(task.LastPickupHandHeight),F(task.LastPickupHeadDrop),queue.Count.ToString(Invariant),((int)spawner.Mode).ToString(Invariant)};
            for(int i=0;i<7;i++)values.Add(F(estimator.Score(i)));
            values.Add(F(estimator.Average));values.Add(task.CompletedTotal.ToString(Invariant));values.Add(agent!=null&&agent.automatic?"1":"0");values.Add(agent==null?"NONE":agent.Decision.Replace(',','_'));
            pending.Add(string.Join(",",values));
        }
        static string F(float value)=>value.ToString("0.####",Invariant);
        public bool SubmitFeedback(int score)
        {
            score=Mathf.Clamp(score,1,5);if(pending.Count==0)return false;
            if(!WritePending("manufacturing_trials_v3.csv",score))return false;
            if(agent!=null)agent.RecordFeedback(score);
            pending.Clear();trial++;started=Time.unscaledTime;havePrevious=false;
            return true;
        }
        void OnApplicationPause(bool paused){if(paused)SaveUnlabeled();}
        void OnApplicationQuit(){SaveUnlabeled();}
        void SaveUnlabeled()
        {
            if(pending.Count==0||!WritePending("manufacturing_unlabeled_v3.csv",0))return;
            pending.Clear();trial++;started=Time.unscaledTime;havePrevious=false;
        }
        bool WritePending(string fileName,int score)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);LastSavedPath=Path.Combine(DataDirectory,fileName);
                bool header=!File.Exists(LastSavedPath)||new FileInfo(LastSavedPath).Length==0;
                using(var writer=new StreamWriter(LastSavedPath,true))
                {
                    if(header)writer.WriteLine(Header);
                    for(int i=0;i<pending.Count;i++)writer.WriteLine(pending[i]+","+score.ToString(Invariant));
                }
                return true;
            }
            catch(Exception e){Debug.LogError("ML data save failed: "+e.Message);return false;}
        }
        public void LogAgentEvent(string phase,string reason,float load)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                string path=Path.Combine(DataDirectory,"agent_events_v3.csv");
                bool header=!File.Exists(path)||new FileInfo(path).Length==0;
                string line=string.Join(",",sessionId,profile==null?"NONE":profile.Id,trial.ToString(Invariant),F(Time.unscaledTime),phase,
                    reason.Replace(',',';').Replace('\n',' '),F(load),queue==null?"0":queue.Count.ToString(Invariant),
                    F(conveyor==null?0:conveyor.Speed),F(table==null?0:table.Height),F(table==null?0:table.TargetHeight),
                    tracking!=null&&tracking.AllTracked?"1":"0",agent!=null&&agent.riskModel!=null&&agent.riskModel.IsLoaded?"1":"0");
                using(var writer=new StreamWriter(path,true))
                {
                    if(header)writer.WriteLine(EventHeader);
                    writer.WriteLine(line);
                }
            }
            catch(Exception error){Debug.LogWarning("Agent event save failed: "+error.Message);}
        }
    }
}
