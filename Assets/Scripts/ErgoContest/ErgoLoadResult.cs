using UnityEngine;

namespace ErgoContest
{
    public enum LoadBand
    {
        Low,
        Medium,
        High
    }

    public readonly struct ErgoLoadResult
    {
        public ErgoLoadResult(float totalScore, float shoulderScore, float armScore, float handScore, float reachMeters, float handHeightFromShoulder, string primaryCause)
        {
            TotalScore = totalScore;
            ShoulderScore = shoulderScore;
            ArmScore = armScore;
            HandScore = handScore;
            ReachMeters = reachMeters;
            HandHeightFromShoulder = handHeightFromShoulder;
            PrimaryCause = primaryCause;
        }

        public float TotalScore { get; }
        public float ShoulderScore { get; }
        public float ArmScore { get; }
        public float HandScore { get; }
        public float ReachMeters { get; }
        public float HandHeightFromShoulder { get; }
        public string PrimaryCause { get; }
        public LoadBand Band => TotalScore >= 72f ? LoadBand.High : TotalScore >= 44f ? LoadBand.Medium : LoadBand.Low;
        public Color DisplayColor => Band == LoadBand.High ? new Color(1f, 0.16f, 0.08f) : Band == LoadBand.Medium ? new Color(1f, 0.78f, 0.12f) : new Color(0.20f, 0.88f, 0.36f);
    }
}
