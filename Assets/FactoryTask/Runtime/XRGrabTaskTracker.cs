using UnityEngine;

namespace FactoryTask
{
    [DefaultExecutionOrder(-100)]
    public sealed class XRGrabTaskTracker : MonoBehaviour
    {
        public XRTrackingProvider tracking;
        public UserCalibration calibration;
        public PartSpawner pool;
        public BoxCollider completionVolume;
        public Transform recoveryPoint;
        public BodyLoadEstimator estimator;
        public float gripReach = .16f;
        // A held part kept this far from the hand (blocked by a table or another part) for longer
        // than breakawaySeconds slips out of the grip. Brief lag from fast hand motion does not.
        public float breakawayDistance = .3f, breakawaySeconds = .25f;
        // Never-handled parts that fall off the line are cleared once they settle below this height.
        public float floorHeight = .5f;
        public ConveyorPart LeftHeld { get; private set; }
        public ConveyorPart RightHeld { get; private set; }
        [SerializeField] int completedLeft,completedRight;
        public int CompletedLeft => completedLeft;
        public int CompletedRight => completedRight;
        public int CompletedTotal => CompletedLeft+CompletedRight;
        bool replay;
        float replayWeight;
        public float HeldWeight => replay?replayWeight:(LeftHeld!=null?LeftHeld.weightKg:0)+(RightHeld!=null?RightHeld.weightKg:0);
        public void ApplyRecordedTask(float weight,int left,int right){replay=true;replayWeight=weight;completedLeft=left;completedRight=right;}
        bool leftWasPressed,rightWasPressed;
        float leftStrain,rightStrain;
        // Completed parts stay in the bin, oldest first; when it holds binCapacity the oldest fades out.
        public int binCapacity = 4;
        readonly System.Collections.Generic.List<ConveyorPart> binned=new System.Collections.Generic.List<ConveyorPart>();
        public int BinnedCount => binned.Count;
        void FixedUpdate(){Tick(Time.fixedDeltaTime);}
        public void Tick(float dt)
        {
            if(!calibration.IsCalibrated)return;
            if(!tracking.HeadTracked||!tracking.Focused){Freeze(LeftHeld,dt);Freeze(RightHeld,dt);return;}
            ProcessHand(HandSide.Left,tracking.LeftGrip,ref leftWasPressed,ref leftStrain,dt);
            ProcessHand(HandSide.Right,tracking.RightGrip,ref rightWasPressed,ref rightStrain,dt);
            for(int i=0;i<pool.Parts.Length;i++)
            {
                var part=pool.Parts[i];if(!part.Free)continue;
                if(part.Body.position.y<-.3f){part.Recover(recoveryPoint.position);continue;}
                if(part.State==PartState.Conveying)
                {
                    part.FloorSeconds=part.Body.position.y<floorHeight?part.FloorSeconds+dt:0;
                    if(part.FloorSeconds>=3)pool.Recycle(part);
                    continue;
                }
                if(IsInside(part))part.InsideSeconds+=dt;else part.InsideSeconds=0;
                if(part.InsideSeconds>=.2f)
                {
                    HandSide side=part.LastHand;if(side==HandSide.None)continue;
                    if(side==HandSide.Left)completedLeft++;else completedRight++;
                    estimator.RecordCompletion(side,Time.time);part.MarkCompleted();binned.Add(part);
                }
            }
            UpdateBin(dt);
        }
        void UpdateBin(float dt)
        {
            binned.RemoveAll(p=>p.State==PartState.Pooled);
            // One part at a time, and only once the bin is full, so it reads as clearing space.
            if(binned.Count==0)return;
            var oldest=binned[0];
            if(oldest.Vanishing||binned.Count>=binCapacity)oldest.BeginVanish();
            if(oldest.TickVanish(dt))binned.RemoveAt(0);
        }
        void Freeze(ConveyorPart part,float dt){if(part!=null)part.Follow(part.Body.position,part.Body.rotation,dt,false);}
        void ProcessHand(HandSide side,bool pressed,ref bool wasPressed,ref float strain,float dt)
        {
            var held=Held(side);var hand=tracking.Hand(side);
            if(!tracking.Tracked(side)){Freeze(held,dt);wasPressed=pressed;return;}
            if(pressed&&!wasPressed&&held==null){TryGrab(side);strain=0;}
            held=Held(side);
            if(held!=null)
            {
                held.Follow(Palm(side),hand.rotation,dt,true);
                strain=held.Separation>breakawayDistance?strain+dt:0;
                if(!pressed||strain>breakawaySeconds)Drop(side);
            }
            wasPressed=pressed;
        }
        void Drop(HandSide side)
        {
            var held=Held(side);if(held==null)return;
            held.Release();if(side==HandSide.Left)LeftHeld=null;else RightHeld=null;
        }
        public ConveyorPart Held(HandSide side)=>side==HandSide.Left?LeftHeld:RightHeld;
        // Grip pose sits in the controller handle with the palm facing it (+X for the left hand,
        // -X for the right); the palm surface is about 3 cm out from the handle centre.
        public static Vector3 PalmOffset(HandSide side)=>(side==HandSide.Left?Vector3.left:Vector3.right)*.03f;
        public Vector3 Palm(HandSide side){var hand=tracking.Hand(side);return hand.position+hand.rotation*PalmOffset(side);}
        public bool TryGrab(HandSide side)
        {
            if(!calibration.IsCalibrated||!tracking.AllTracked||Held(side)!=null||side==HandSide.None)return false;
            var hand=tracking.Hand(side);Vector3 position=hand.position;ConveyorPart nearest=null;float best=gripReach*gripReach;
            for(int i=0;i<pool.Parts.Length;i++)
            {
                var part=pool.Parts[i];if(!part.Free)continue;
                float distance=(part.Shape.ClosestPoint(position)-position).sqrMagnitude;
                if(distance<=best){nearest=part;best=distance;}
            }
            if(nearest==null||!nearest.Grab(side,Palm(side),hand.rotation))return false;
            if(side==HandSide.Left)LeftHeld=nearest;else RightHeld=nearest;
            return true;
        }
        public bool IsInside(ConveyorPart part)
        {
            if(part.State!=PartState.Dropped||part.LastHand==HandSide.None)return false;
            // The part's centre must be over the bin floor and below the stacking limit. Requiring every
            // corner inside rejected parts that rested tilted or on top of others, so they never counted.
            Vector3 half=completionVolume.size*.5f;
            Vector3 v=completionVolume.transform.InverseTransformPoint(part.Shape.bounds.center)-completionVolume.center;
            return Mathf.Abs(v.x)<=half.x&&Mathf.Abs(v.y)<=half.y&&Mathf.Abs(v.z)<=half.z;
        }
    }
}
