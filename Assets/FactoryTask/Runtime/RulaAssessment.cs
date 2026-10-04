using System;
using UnityEngine;

namespace FactoryTask
{
    [Serializable]
    public struct RulaSideResult
    {
        public bool valid;
        public int upperArm,lowerArm,wrist,wristTwist,postureA,scoreA;
        public int neck,trunk,legs,postureB,scoreB,grandScore,actionLevel;
        public int muscleUse,forceLoad;
        public float confidence;
    }

    // Automated RULA screening from the inferred avatar pose. Head and controllers are measured;
    // elbows, trunk, legs and feet are IK estimates, so this is not a clinical diagnosis.
    [DefaultExecutionOrder(200)]
    public sealed class RulaAssessment : MonoBehaviour
    {
        public FullBodyAvatarIK avatar;
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public XRGrabTaskTracker task;
        public RulaSideResult Left { get; private set; }
        public RulaSideResult Right { get; private set; }
        public int WorstGrandScore=>Mathf.Max(Left.grandScore,Right.grandScore);
        public bool IsValid=>Left.valid&&Right.valid;

        static readonly int[,] TableA={
            {1,2,2,2,2,3,3,3},{2,2,2,2,3,3,3,3},{2,3,3,3,3,3,4,4},{2,3,3,3,3,4,4,4},{3,3,3,3,3,4,4,4},{3,4,4,4,4,4,5,5},
            {3,3,4,4,4,4,5,5},{3,4,4,4,4,4,5,5},{4,4,4,4,4,5,5,5},{4,4,4,4,4,5,5,5},{4,4,4,4,4,5,5,5},{4,4,4,5,5,5,6,6},
            {5,5,5,5,5,6,6,7},{5,6,6,6,6,7,7,7},{6,6,6,7,7,7,7,8},{7,7,7,7,7,8,8,9},{8,8,8,8,8,9,9,9},{9,9,9,9,9,9,9,9}};
        static readonly int[,] TableB={{1,3,2,3,3,4,5,5,6,6,7,7},{2,3,2,3,4,5,5,5,6,7,7,7},{3,3,3,4,4,5,5,6,6,7,7,7},{5,5,5,6,6,7,7,7,7,7,8,8},{7,7,7,7,7,8,8,8,8,8,8,8},{8,8,8,8,8,8,8,9,9,9,9,9}};
        static readonly int[,] TableC={{1,2,3,3,4,5,5},{2,2,3,4,4,5,5},{3,3,3,4,4,5,6},{3,3,3,4,5,6,6},{4,4,4,5,6,7,7},{4,4,5,6,6,7,7},{5,5,6,6,7,7,7},{5,5,6,7,7,7,7}};

        void LateUpdate(){Evaluate();}
        public void Evaluate()
        {
            if(avatar==null||tracking==null||calibration==null||task==null||!calibration.IsCalibrated||!tracking.HeadTracked){Left=Right=default;return;}
            Left=EvaluateSide(HandSide.Left,avatar.leftArm);Right=EvaluateSide(HandSide.Right,avatar.rightArm);
        }
        RulaSideResult EvaluateSide(HandSide side,AvatarLimb arm)
        {
            Vector3 forward=calibration.Forward,right=calibration.Right*(side==HandSide.Left?-1:1),up=Vector3.up;
            Vector3 upper=(arm.lower.position-arm.upper.position).normalized,lower=(arm.end.position-arm.lower.position).normalized;
            float flex=Mathf.Atan2(Vector3.Dot(upper,forward),-Vector3.Dot(upper,up))*Mathf.Rad2Deg;
            int ua=flex < -20?2:flex<=20?1:flex<=45?2:flex<=90?3:4;
            if(Mathf.Abs(Vector3.Dot(upper,right))>.34f)ua++;
            float elbow=Vector3.Angle(-upper,lower);int la=elbow>=60&&elbow<=100?1:2;
            Vector3 handForward=tracking.Hand(side).forward;
            float wristAngle=Vector3.Angle(lower,Vector3.ProjectOnPlane(handForward,right));int wrist=wristAngle<=5?1:wristAngle<=15?2:3;
            if(Mathf.Abs(Vector3.Dot(handForward,right))>.35f)wrist++;
            int twist=Mathf.Abs(Vector3.Dot(tracking.Hand(side).up,right))<.7f?1:2;
            int postureA=LookupA(ua,la,wrist,twist);

            Vector3 headLocal=Quaternion.Inverse(avatar.chest.rotation)*avatar.head.forward;
            float neckFlex=Mathf.Abs(Mathf.Atan2(headLocal.y,headLocal.z)*Mathf.Rad2Deg);
            int neck=neckFlex<10?1:neckFlex<=20?2:neckFlex<=45?3:4;
            if(Mathf.Abs(headLocal.x)>.17f)neck++;
            Vector3 trunk=(avatar.chest.position-avatar.hips.position).normalized;
            float trunkFlex=Vector3.Angle(up,Vector3.ProjectOnPlane(trunk,right));int trunkScore=trunkFlex<5?1:trunkFlex<=20?2:trunkFlex<=60?3:4;
            if(Mathf.Abs(Vector3.Dot(trunk,right))>.17f)trunkScore++;
            bool grounded=Mathf.Abs(avatar.LeftSoleY-calibration.standingPoint.position.y)<.05f&&Mathf.Abs(avatar.RightSoleY-calibration.standingPoint.position.y)<.05f;
            int legs=grounded?1:2,postureB=LookupB(neck,trunkScore,legs);
            var held=task.Held(side);float weight=held!=null?held.weightKg:0;
            // RULA muscle-use adjustment: static posture >1 minute or repeated action >4/min.
            int muscle=(held!=null&&held.HoldSeconds>=60)||(task.estimator!=null&&task.estimator.RecentCount(side,Time.time)>4)?1:0;
            int force=weight<2?0:weight<=10?(held!=null&&held.HoldSeconds>=60?2:1):3;
            int a=Mathf.Clamp(postureA+muscle+force,1,8),b=Mathf.Clamp(postureB+muscle+force,1,7),grand=Combine(a,b);
            return new RulaSideResult{valid=true,upperArm=ua,lowerArm=la,wrist=wrist,wristTwist=twist,postureA=postureA,scoreA=a,neck=neck,trunk=trunkScore,legs=legs,postureB=postureB,scoreB=b,muscleUse=muscle,forceLoad=force,grandScore=grand,actionLevel=grand<=2?1:grand<=4?2:grand<=6?3:4,confidence=.65f};
        }
        public static int LookupA(int upper,int lower,int wrist,int twist){int row=(Mathf.Clamp(upper,1,6)-1)*3+(Mathf.Clamp(lower,1,3)-1);int col=(Mathf.Clamp(wrist,1,4)-1)*2+(Mathf.Clamp(twist,1,2)-1);return TableA[row,col];}
        public static int LookupB(int neck,int trunk,int legs){int col=(Mathf.Clamp(trunk,1,6)-1)*2+(Mathf.Clamp(legs,1,2)-1);return TableB[Mathf.Clamp(neck,1,6)-1,col];}
        public static int Combine(int scoreA,int scoreB)=>TableC[Mathf.Clamp(scoreA,1,8)-1,Mathf.Clamp(scoreB,1,7)-1];
    }
}
