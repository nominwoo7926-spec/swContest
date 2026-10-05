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
        // Parts are carried with both hands: both grips must close on the same part, one part at a time.
        public bool requireBothHands = true;
        public bool CarriedWithBothHands => LeftHeld!=null&&LeftHeld==RightHeld;
        public float HeldWeight => replay?replayWeight:(LeftHeld!=null?LeftHeld.weightKg:0)+(RightHeld!=null&&RightHeld!=LeftHeld?RightHeld.weightKg:0);
        // Load borne by one arm: a two-handed carry splits the part's weight between the hands.
        public float HandWeight(HandSide side){var part=Held(side);return part==null?0:CarriedWithBothHands?part.weightKg*.5f:part.weightKg;}
        public void ApplyRecordedTask(float weight,int left,int right){replay=true;replayWeight=weight;completedLeft=left;completedRight=right;}
        bool leftWasPressed,rightWasPressed;
        float leftStrain,rightStrain;
        // Completed parts stay in the bin, oldest first; when it holds binCapacity the oldest fades out.
        public int binCapacity = 4;
        readonly System.Collections.Generic.List<ConveyorPart> binned=new System.Collections.Generic.List<ConveyorPart>();
        public int BinnedCount => binned.Count;
        // Raised once for every part counted as placed in the bin.
        public event System.Action<ConveyorPart> PartCompleted;
        // A new run: let go of anything held and start the counts from zero.
        public void ResetRun()
        {
            if(LeftHeld!=null)LeftHeld.Release();if(RightHeld!=null&&RightHeld!=LeftHeld)RightHeld.Release();
            LeftHeld=RightHeld=null;binned.Clear();completedLeft=completedRight=0;replay=false;
        }
        void FixedUpdate(){Tick(Time.fixedDeltaTime);}
        public void Tick(float dt)
        {
            if(!calibration.IsCalibrated)return;
            if(!tracking.HeadTracked||!tracking.Focused){Freeze(LeftHeld,dt);Freeze(RightHeld,dt);return;}
            if(requireBothHands)ProcessBothHands(dt);
            else
            {
                ProcessHand(HandSide.Left,tracking.LeftGrip,ref leftWasPressed,ref leftStrain,dt);
                ProcessHand(HandSide.Right,tracking.RightGrip,ref rightWasPressed,ref rightStrain,dt);
            }
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
                    // A two-handed carry is one completed part (credited to the right-hand counter)
                    // but a repetition for both arms.
                    if(side==HandSide.Left)completedLeft++;else completedRight++;
                    if(part.CarriedWithBothHands){estimator.RecordCompletion(HandSide.Left,Time.time);estimator.RecordCompletion(HandSide.Right,Time.time);}
                    else estimator.RecordCompletion(side,Time.time);
                    part.MarkCompleted();binned.Add(part);PartCompleted?.Invoke(part);
                }
            }
            UpdateBin(dt);
            UpdateSpectatorVisibility();
        }
        // Third-person view: hide real parts that are held or in the bin (the dummy shows its own).
        // Also called by the session recorder while replaying, when this component is disabled.
        public void UpdateSpectatorVisibility()
        {
            if(pool==null||pool.Parts==null)return;
            for(int i=0;i<pool.Parts.Length;i++)
            {
                var part=pool.Parts[i];if(!part.gameObject.activeSelf)continue;
                part.SetSpectatorHidden(part.State==PartState.Held||InBin(part));
            }
        }
        bool InBin(ConveyorPart part)
        {
            Vector3 half=completionVolume.size*.5f;
            Vector3 v=completionVolume.transform.InverseTransformPoint(part.Shape.bounds.center)-completionVolume.center;
            return Mathf.Abs(v.x)<=half.x&&Mathf.Abs(v.y)<=half.y+.2f&&Mathf.Abs(v.z)<=half.z;
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
        void ProcessBothHands(float dt)
        {
            bool left=tracking.LeftGrip,right=tracking.RightGrip;var held=LeftHeld;
            if(!tracking.Tracked(HandSide.Left)||!tracking.Tracked(HandSide.Right)){Freeze(held,dt);leftWasPressed=left;rightWasPressed=right;return;}
            bool closing=(left&&!leftWasPressed)||(right&&!rightWasPressed);
            if(held==null&&left&&right&&closing){TryGrabBoth();leftStrain=0;}
            held=LeftHeld;
            if(held!=null)
            {
                TwoHandPose(out Vector3 position,out Quaternion rotation);
                held.Follow(position,rotation,dt,true);
                leftStrain=held.Separation>breakawayDistance?leftStrain+dt:0;
                // Opening either hand, or pulling the hands apart wider than the part, lets it go.
                float spread=Vector3.Distance(Palm(HandSide.Left),Palm(HandSide.Right));
                if(!left||!right||leftStrain>breakawaySeconds||spread>held.HalfHeight*2+.25f)DropBoth();
            }
            leftWasPressed=left;rightWasPressed=right;
        }
        // The part sits between the palms: centred on them, its X axis across from left to right palm.
        void TwoHandPose(out Vector3 position,out Quaternion rotation)
        {
            Vector3 left=Palm(HandSide.Left),right=Palm(HandSide.Right);position=(left+right)*.5f;
            Vector3 across=right-left;if(across.sqrMagnitude<1e-6f)across=tracking.Hand(HandSide.Right).right;across.Normalize();
            Vector3 forward=Vector3.ProjectOnPlane(tracking.Hand(HandSide.Left).forward+tracking.Hand(HandSide.Right).forward,across);
            if(forward.sqrMagnitude<1e-4f)forward=Vector3.ProjectOnPlane(tracking.head.forward,across);
            forward.Normalize();rotation=Quaternion.LookRotation(forward,Vector3.Cross(forward,across));
        }
        public bool TryGrabBoth()
        {
            if(!calibration.IsCalibrated||!tracking.AllTracked||LeftHeld!=null||RightHeld!=null)return false;
            Vector3 left=Palm(HandSide.Left),right=Palm(HandSide.Right);ConveyorPart nearest=null;float best=float.MaxValue,reach=gripReach*gripReach;
            for(int i=0;i<pool.Parts.Length;i++)
            {
                var part=pool.Parts[i];if(!part.Free)continue;
                float l=(part.Shape.ClosestPoint(left)-left).sqrMagnitude,r=(part.Shape.ClosestPoint(right)-right).sqrMagnitude;
                // Both palms must be on (or right next to) this same part.
                if(l<=reach&&r<=reach&&l+r<best){nearest=part;best=l+r;}
            }
            if(nearest==null)return false;
            TwoHandPose(out Vector3 position,out Quaternion rotation);
            if(!nearest.Grab(HandSide.Right,position,rotation,true))return false;
            LeftHeld=RightHeld=nearest;
            return true;
        }
        void DropBoth()
        {
            if(LeftHeld==null)return;
            LeftHeld.Release();LeftHeld=RightHeld=null;
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
