using Meta.XR.Movement.Retargeting;
using Unity.Collections;
using UnityEngine;
using static Meta.XR.Movement.MSDKUtility;

namespace FactoryTask
{
    // One Movement source is shared by live Quest tracking and headset-free replay.
    // CharacterRetargeter therefore sees the same skeleton layout in both modes.
    [DefaultExecutionOrder(-90)]
    public sealed class MovementBodyPoseStream : MetaSourceDataProvider
    {
        public const int JointCount = (int)SkeletonData.FullBodyTrackingBoneId.End;

        readonly NativeTransform[] recordedPose = new NativeTransform[JointCount];
        readonly NativeTransform[] recordedTPose = new NativeTransform[JointCount];
        bool replaying, replayValid, replayTPoseChanged;

        protected override void Start()
        {
            ProvidedSkeletonType = OVRPlugin.BodyJointSet.FullBody;
            base.Start();
        }

        public bool CaptureLivePose(NativeTransform[] pose, NativeTransform[] tPose)
        {
            if (replaying || pose == null || tPose == null || pose.Length < JointCount || tPose.Length < JointCount)
                return false;

            var source = base.GetSkeletonPose();
            bool valid = base.IsPoseValid() && source.IsCreated && source.Length >= JointCount;
            if (valid)
                for (int i = 0; i < JointCount; i++) pose[i] = source[i];
            if (source.IsCreated) source.Dispose();

            var bind = base.GetSkeletonTPose();
            if (bind.IsCreated && bind.Length >= JointCount)
                for (int i = 0; i < JointCount; i++) tPose[i] = bind[i];
            if (bind.IsCreated) bind.Dispose();
            return valid;
        }

        public void BeginReplay() { replaying = true; replayValid = false; replayTPoseChanged = true; }
        public void EndReplay() { replaying = false; replayValid = false; }

        public void SetReplayPose(NativeTransform[] pose, NativeTransform[] tPose, bool valid)
        {
            if (pose == null || tPose == null) { replayValid = false; return; }
            for (int i = 0; i < JointCount; i++)
            {
                recordedPose[i] = pose[i];
                recordedTPose[i] = tPose[i];
            }
            replayValid = valid;
        }

        public override NativeArray<NativeTransform> GetSkeletonPose()
        {
            if (!replaying) return base.GetSkeletonPose();
            return new NativeArray<NativeTransform>(recordedPose, Allocator.Temp);
        }

        public override NativeArray<NativeTransform> GetSkeletonTPose()
        {
            if (!replaying) return base.GetSkeletonTPose();
            return new NativeArray<NativeTransform>(recordedTPose, Allocator.Temp);
        }

        public override string GetManifestation() => replaying ? null : base.GetManifestation();
        public override bool IsPoseValid() => replaying ? replayValid : base.IsPoseValid();
        public override bool IsNewTPoseAvailable()
        {
            if (!replaying) return base.IsNewTPoseAvailable();
            bool changed = replayTPoseChanged;
            replayTPoseChanged = false;
            return changed;
        }
    }
}
