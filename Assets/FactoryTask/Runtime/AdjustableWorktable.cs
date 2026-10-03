using UnityEngine;

namespace FactoryTask
{
    // Moves the actual pickup surface and its target positions in room-scale space.
    // The headset and controllers never move with the table.
    [DefaultExecutionOrder(-120)]
    public sealed class AdjustableWorktable : MonoBehaviour
    {
        public const float MinHeight = .55f;
        public const float MaxHeight = 1.20f;
        public const float DefaultHeight = .82f;
        public Transform movingTop;
        public Transform[] legs;
        public XRGrabTaskTracker task;
        public XRTrackingProvider tracking;
        public float metresPerSecond = .08f;
        public float Height => movingTop == null ? DefaultHeight : movingTop.position.y;
        public float TargetHeight { get; private set; } = DefaultHeight;
        public float LastManualAdjustmentTime { get; private set; } = -999f;
        public bool Moving => Mathf.Abs(TargetHeight - Height) > .005f;

        public void SetTargetHeight(float height, bool manual = false)
        {
            TargetHeight = Mathf.Round(Mathf.Clamp(height, MinHeight, MaxHeight) * 100f) / 100f;
            if (manual) LastManualAdjustmentTime = Time.unscaledTime;
        }

        public void ApplyRecordedHeight(float height)
        {
            if(movingTop==null)return;
            height=Mathf.Clamp(height,MinHeight,MaxHeight);
            var position=movingTop.position;
            position.y=height;
            movingTop.position=position;
            TargetHeight=height;
            UpdateLegs(height);
        }

        void FixedUpdate()
        {
            if (movingTop == null || !Moving) return;
            // A held part or a hand on the work surface pauses the motor.
            if (task != null && task.HeldWeight > 0) return;
            if (tracking != null && tracking.Focused &&
                ((tracking.LeftTracked && NearSurface(tracking.leftHand.position)) ||
                 (tracking.RightTracked && NearSurface(tracking.rightHand.position)))) return;
            float next = Mathf.MoveTowards(Height, TargetHeight, metresPerSecond * Time.fixedDeltaTime);
            var position = movingTop.position;
            position.y = next;
            movingTop.position = position;
            UpdateLegs(next);
        }

        bool NearSurface(Vector3 hand)
        {
            Vector3 centre = movingTop.position;
            return Mathf.Abs(hand.x - centre.x) < 1.04f &&
                   Mathf.Abs(hand.z - centre.z) < .49f &&
                   Mathf.Abs(hand.y - (Height + .05f)) < .13f;
        }

        void UpdateLegs(float height)
        {
            if (legs == null) return;
            foreach (var leg in legs)
            {
                if (leg == null) continue;
                var scale = leg.localScale;
                scale.y = Mathf.Max(.1f, height - .05f);
                leg.localScale = scale;
                var position = leg.localPosition;
                position.y = scale.y * .5f;
                leg.localPosition = position;
            }
        }
    }
}
