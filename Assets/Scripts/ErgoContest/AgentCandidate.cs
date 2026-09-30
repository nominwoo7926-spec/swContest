using UnityEngine;

namespace ErgoContest
{
    public readonly struct AgentCandidate
    {
        public AgentCandidate(EquipmentType equipment, Vector3 targetPosition, float targetSpeed, float score, string cause, string action, string evidence)
        {
            Equipment = equipment;
            TargetPosition = targetPosition;
            TargetSpeed = targetSpeed;
            Score = score;
            Cause = cause;
            Action = action;
            Evidence = evidence;
        }

        public EquipmentType Equipment { get; }
        public Vector3 TargetPosition { get; }
        public float TargetSpeed { get; }
        public float Score { get; }
        public string Cause { get; }
        public string Action { get; }
        public string Evidence { get; }
        public bool IsValid => Equipment != EquipmentType.None && Score > 0f;
    }
}
