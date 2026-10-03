using UnityEngine;

namespace FactoryTask
{
    [DefaultExecutionOrder(-100)]
    public sealed class XRGrabTaskTracker : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public PickupQueue queue;
        public PartSpawner pool;
        public BoxCollider completionVolume;
        public Transform recoveryPoint;
        public BodyLoadEstimator estimator;
        public float gripReach = .16f;
        public ConveyorPart LeftHeld { get; private set; }
        public ConveyorPart RightHeld { get; private set; }
        [SerializeField] int completedLeft,completedRight;
        public int CompletedLeft => completedLeft;
        public int CompletedRight => completedRight;
        public int CompletedTotal => CompletedLeft+CompletedRight;
        public int PickupCount { get; private set; }
        public float LastPickupTime { get; private set; } = -999f;
        public float LastPickupHandHeight { get; private set; }
        public float LastPickupHeadDrop { get; private set; }
        bool replay;
        float replayWeight;
        public float HeldWeight => replay?replayWeight:(LeftHeld!=null?LeftHeld.weightKg:0)+(RightHeld!=null?RightHeld.weightKg:0);
        public void ApplyRecordedTask(float weight,int left,int right){replay=true;replayWeight=weight;completedLeft=left;completedRight=right;}
        bool bothGripWasPressed;
        void FixedUpdate(){Tick(Time.fixedDeltaTime);}
        public void Tick(float dt)
        {
            if(!calibration.IsCalibrated)return;
            if(!tracking.HeadTracked||!tracking.Focused){Freeze(LeftHeld,dt);Freeze(RightHeld,dt);return;}
            bool both=tracking.LeftGrip&&tracking.RightGrip;
            if(both&&!bothGripWasPressed&&LeftHeld==null&&RightHeld==null)TryGrabWithBothGrips();
            ProcessHand(HandSide.Left,tracking.LeftGrip,dt);
            ProcessHand(HandSide.Right,tracking.RightGrip,dt);
            bothGripWasPressed=both;
            for(int i=0;i<pool.Parts.Length;i++)
            {
                var part=pool.Parts[i];if(part.State!=PartState.Dropped)continue;
                if(part.Body.position.y<-.3f)part.Recover(recoveryPoint.position);
                if(IsInside(part))part.InsideSeconds+=dt;else part.InsideSeconds=0;
                if(part.InsideSeconds>=.2f)
                {
                    HandSide side=part.LastHand;if(side==HandSide.None)continue;
                    if(side==HandSide.Left)completedLeft++;else completedRight++;
                    estimator.RecordCompletion(side,Time.time);pool.Recycle(part);
                }
            }
        }
        void Freeze(ConveyorPart part,float dt){if(part!=null)part.Follow(part.Body.position,part.Body.rotation,dt,false);}
        void ProcessHand(HandSide side,bool pressed,float dt)
        {
            var held=Held(side);var hand=tracking.Hand(side);
            if(!tracking.Tracked(side)){Freeze(held,dt);return;}
            held=Held(side);
            if(held!=null)
            {
                held.Follow(hand.position,hand.rotation,dt,true);
                if(!pressed){held.Release();if(side==HandSide.Left)LeftHeld=null;else RightHeld=null;}
            }
        }
        public bool TryGrabWithBothGrips()
        {
            if(!tracking.LeftGrip||!tracking.RightGrip)return false;
            float left=NearestDistanceSquared(HandSide.Left),right=NearestDistanceSquared(HandSide.Right);
            return TryGrab(left<=right?HandSide.Left:HandSide.Right);
        }
        float NearestDistanceSquared(HandSide side)
        {
            Vector3 hand=tracking.Hand(side).position;float best=float.PositiveInfinity;
            for(int i=0;i<pool.Parts.Length;i++)
            { var part=pool.Parts[i];if(part.State!=PartState.Dropped&&!queue.CanPick(part))continue;best=Mathf.Min(best,(part.Shape.ClosestPoint(hand)-hand).sqrMagnitude); }
            return best;
        }
        public ConveyorPart Held(HandSide side)=>side==HandSide.Left?LeftHeld:RightHeld;
        public bool TryGrab(HandSide side)
        {
            if(!calibration.IsCalibrated||!tracking.AllTracked||!tracking.LeftGrip||!tracking.RightGrip||Held(side)!=null||side==HandSide.None)return false;
            Vector3 hand=tracking.Hand(side).position;ConveyorPart nearest=null;float best=gripReach*gripReach;
            for(int i=0;i<pool.Parts.Length;i++)
            {
                var part=pool.Parts[i];
                if(part.State!=PartState.Dropped&&!queue.CanPick(part))continue;
                float distance=(part.Shape.ClosestPoint(hand)-hand).sqrMagnitude;
                if(distance<=best){nearest=part;best=distance;}
            }
            if(nearest==null||!nearest.Grab(side,hand))return false;
            PickupCount++;
            LastPickupTime=Time.unscaledTime;
            LastPickupHandHeight=hand.y;
            LastPickupHeadDrop=Mathf.Max(0,calibration.BaselineHead.y-tracking.head.position.y);
            queue.Remove(nearest);if(side==HandSide.Left)LeftHeld=nearest;else RightHeld=nearest;
            return true;
        }
        public bool IsInside(ConveyorPart part)
        {
            if(part.State!=PartState.Dropped||part.LastHand==HandSide.None)return false;
            Bounds b=part.Shape.bounds;Vector3 half=completionVolume.size*.5f;
            // Require the entire part, not just its centre or a trigger touch, inside the box.
            for(int i=0;i<8;i++)
            {
                Vector3 corner=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);
                Vector3 v=completionVolume.transform.InverseTransformPoint(corner)-completionVolume.center;
                if(Mathf.Abs(v.x)>half.x||Mathf.Abs(v.y)>half.y||Mathf.Abs(v.z)>half.z)return false;
            }
            return true;
        }
    }
}
