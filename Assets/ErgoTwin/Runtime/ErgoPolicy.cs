using UnityEngine;

namespace ErgoTwin
{
    // Transparent geometric surrogate, not a validated RULA/REBA or medical assessment.
    public static class ErgoPolicy
    {
        public static Bounds TravelVolume => new Bounds(new Vector3(0,1.1f,.65f),new Vector3(2.8f,2.3f,1.6f));
        public struct Load
        {
            public float score, reach, elevation, trunk, moment;
        }

        public static Load Estimate(Vector3 head, Vector3 hand, Vector3 pelvis, float kilograms, bool holding)
        {
            Vector3 shoulder = head + new Vector3(.18f, -.23f, 0);
            Vector3 arm = hand - shoulder;
            float reach = new Vector2(arm.x, arm.z).magnitude;
            float elevation = Vector3.Angle(Vector3.down, arm);
            float trunk = Vector3.Angle(Vector3.up, head - pelvis);
            float moment = (holding ? kilograms : 0) * 9.81f * reach;
            return new Load { reach = reach, elevation = elevation, trunk = trunk, moment = moment,
                score = Mathf.Clamp(8 + Mathf.Max(0, elevation - 30) * .48f +
                    Mathf.Max(0, reach - .35f) * 65 + Mathf.Max(0, trunk - 8) * .7f + moment * .65f, 0, 100) };
        }

        public static float Cost(float height, float distance, float shoulderHeight)
        {
            var load = Estimate(new Vector3(0, shoulderHeight + .23f, 0),
                new Vector3(.18f, height, distance), new Vector3(0, .9f, 0), 3, true);
            return load.score + Mathf.Abs(height - 1.05f) * 8;
        }

        public static Vector2 Optimize(float shoulderHeight)
        {
            float best = float.PositiveInfinity;
            Vector2 result = new Vector2(1.05f, .45f);
            for (float h = .85f; h <= 1.351f; h += .05f)
                for (float d = .38f; d <= .751f; d += .05f)
                {
                    float cost = Cost(h, d, shoulderHeight);
                    if (cost < best) { best = cost; result = new Vector2(h, d); }
                }
            return result;
        }

        public static bool Clear(Vector3 left, Vector3 right, Bounds swept, bool holding, bool tracked)
            => tracked && !holding && !swept.Contains(left) && !swept.Contains(right);
    }
}
