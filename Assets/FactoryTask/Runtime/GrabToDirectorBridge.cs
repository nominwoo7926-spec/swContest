using UnityEngine;

namespace FactoryTask
{
    // Mirrors the VR user's carries on the spectator dummy: picking up a part starts (or queues) a
    // sequence and letting go of it lets the dummy place its part. Watching the held weight works
    // both live and while replaying a recorded session.
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
            else if (weight <= 0 && previousWeight > 0) director.NotifyRelease();
            previousWeight = weight;
        }
    }
}
