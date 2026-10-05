using UnityEngine;

namespace FactoryTask
{
    // Starts the spectator dummy's pick-and-place whenever the VR user picks up a part.
    // Watching the held weight (0 -> >0) works both live and while replaying a recorded session.
    public sealed class GrabToDirectorBridge : MonoBehaviour
    {
        public XRGrabTaskTracker task;
        public ThirdPersonAvatarDirector director;
        float previousWeight;

        void Update()
        {
            if (task == null || director == null) return;
            float weight = task.HeldWeight;
            if (weight > 0 && previousWeight <= 0) director.TriggerPickAndPlaceSequence();
            previousWeight = weight;
        }
    }
}
