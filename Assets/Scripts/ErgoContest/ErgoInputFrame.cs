using UnityEngine;

namespace ErgoContest
{
    public readonly struct ErgoInputFrame
    {
        public ErgoInputFrame(
            Vector3 headPosition,
            Quaternion headRotation,
            Vector3 handPosition,
            Quaternion handRotation,
            bool holdingPart,
            bool grabPressed,
            bool releasePressed,
            bool approvePressed,
            bool cancelPressed,
            bool pausePressed,
            bool trackedVrInput)
        {
            HeadPosition = headPosition;
            HeadRotation = headRotation;
            HandPosition = handPosition;
            HandRotation = handRotation;
            HoldingPart = holdingPart;
            GrabPressed = grabPressed;
            ReleasePressed = releasePressed;
            ApprovePressed = approvePressed;
            CancelPressed = cancelPressed;
            PausePressed = pausePressed;
            TrackedVrInput = trackedVrInput;
        }

        public Vector3 HeadPosition { get; }
        public Quaternion HeadRotation { get; }
        public Vector3 HandPosition { get; }
        public Quaternion HandRotation { get; }
        public bool HoldingPart { get; }
        public bool GrabPressed { get; }
        public bool ReleasePressed { get; }
        public bool ApprovePressed { get; }
        public bool CancelPressed { get; }
        public bool PausePressed { get; }
        public bool TrackedVrInput { get; }
    }
}
