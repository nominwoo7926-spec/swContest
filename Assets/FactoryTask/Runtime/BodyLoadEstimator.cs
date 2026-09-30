using UnityEngine;

namespace FactoryTask
{
    public struct LoadInput
    {
        public bool holding;
        public float horizontalReach, totalReach, handAboveShoulder, weight, holdSeconds, travelMetres, repeats;
    }
    public sealed class BodyLoadEstimator : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public XRGrabTaskTracker task;
        // Left shoulder, right shoulder, left arm, right arm, left wrist, right wrist, torso.
        readonly float[] load=new float[7], displayed=new float[7], history=new float[7*8], sum=new float[7];
        readonly float[] leftRepetitions=new float[128],rightRepetitions=new float[128];
        int leftCount,rightCount,leftCursor,rightCursor,sampleCursor,sampleCount;
        float sampleClock;
        public float Score(int index)=>displayed[index];
        public void ApplyRecordedScores(float[] scores){for(int i=0;i<7;i++)displayed[i]=Mathf.Clamp(scores[i],0,100);}
        public float Average {get{float total=0;for(int i=0;i<7;i++)total+=displayed[i];return total/7;}}
        public static Vector3 Evaluate(LoadInput input)
        {
            if(!input.holding)return Vector3.zero;
            float reach=Mathf.Clamp01(input.horizontalReach/.7f),height=Mathf.Clamp01((input.handAboveShoulder+.25f)/.75f);
            float mass=Mathf.Clamp01(input.weight/5),duration=Mathf.Clamp01(input.holdSeconds/30),repetitions=Mathf.Clamp01(input.repeats/12),travel=Mathf.Clamp01(input.travelMetres/5);
            float shoulder=100*(.36f*reach+.2f*height+.34f*mass+.1f*repetitions);
            float arm=100*(.35f*Mathf.Clamp01(input.totalReach/.9f)+.25f*duration+.3f*repetitions+.1f*mass);
            float wrist=100*(.45f*mass+.25f*duration+.2f*travel+.1f*repetitions);
            return new Vector3(Mathf.Clamp(shoulder,0,100),Mathf.Clamp(arm,0,100),Mathf.Clamp(wrist,0,100));
        }
        public static float EvaluateTorso(float headDrop,float headForward)=>100*Mathf.Clamp01(.6f*Mathf.Max(0,headDrop)/.4f+.4f*Mathf.Max(0,headForward)/.45f);
        public static float SmoothLoad(float current,float target,float dt)
        {
            float seconds=target>current?.28f:10f;
            return Mathf.Clamp(Mathf.Lerp(current,target,1-Mathf.Exp(-dt/seconds)),0,100);
        }
        void Update(){Tick(Time.deltaTime,Time.time);}
        public void RecordCompletion(HandSide side,float now)
        {
            if(side==HandSide.Left){leftRepetitions[leftCursor]=now;leftCursor=(leftCursor+1)%128;leftCount=Mathf.Min(128,leftCount+1);}
            else if(side==HandSide.Right){rightRepetitions[rightCursor]=now;rightCursor=(rightCursor+1)%128;rightCount=Mathf.Min(128,rightCount+1);}
        }
        public int RecentCount(HandSide side,float now)
        {
            var values=side==HandSide.Left?leftRepetitions:rightRepetitions;int count=side==HandSide.Left?leftCount:rightCount,result=0;
            for(int i=0;i<count;i++)if(now-values[i]<=60&&now>=values[i])result++;return result;
        }
        Vector3 HandLoad(HandSide side,float now)
        {
            var part=task.Held(side);if(part==null||!tracking.Tracked(side))return Vector3.zero;
            Vector3 delta=tracking.Hand(side).position-calibration.Shoulder(side);
            return Evaluate(new LoadInput{holding=true,horizontalReach=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude,totalReach=delta.magnitude,handAboveShoulder=delta.y,weight=part.weightKg,holdSeconds=part.HoldSeconds,travelMetres=part.TravelMetres,repeats=RecentCount(side,now)});
        }
        public void Tick(float dt,float now)
        {
            if(!calibration.IsCalibrated)return;
            bool valid=tracking.HeadTracked&&tracking.Focused;
            Vector3 left=valid?HandLoad(HandSide.Left,now):Vector3.zero,right=valid?HandLoad(HandSide.Right,now):Vector3.zero;
            Vector3 headDelta=tracking.head.position-calibration.BaselineHead;
            float torso=valid?EvaluateTorso(-headDelta.y,Vector3.Dot(headDelta,calibration.Forward)):0;
            for(int i=0;i<7;i++)
            {
                float target=i==0?left.x:i==1?right.x:i==2?left.y:i==3?right.y:i==4?left.z:i==5?right.z:torso;
                load[i]=SmoothLoad(load[i],target,dt);
            }
            sampleClock+=dt;if(sampleClock<.05f)return;sampleClock=0;sampleCount=Mathf.Min(8,sampleCount+1);
            for(int i=0;i<7;i++){int k=i*8+sampleCursor;sum[i]-=history[k];history[k]=load[i];sum[i]+=load[i];displayed[i]=Mathf.Clamp(sum[i]/sampleCount,0,100);}
            sampleCursor=(sampleCursor+1)%8;
        }
    }
}
