using UnityEngine;

namespace ErgoContest
{
    public sealed class AdjustableEquipment : MonoBehaviour
    {
        [SerializeField] private EquipmentType equipmentType;
        [SerializeField] private Transform movingRoot;
        [SerializeField] private Vector3 minPosition;
        [SerializeField] private Vector3 maxPosition;
        [SerializeField] private float moveSpeed = 0.45f;
        [SerializeField] private float speedValue = 1f;
        [SerializeField] private float minSpeedValue = 0.35f;
        [SerializeField] private float maxSpeedValue = 1.2f;

        private Vector3 targetPosition;
        private float targetSpeedValue;
        private bool moving;

        public EquipmentType EquipmentType => equipmentType;
        public Transform MovingRoot => movingRoot != null ? movingRoot : transform;
        public Vector3 CurrentPosition => MovingRoot.position;
        public Vector3 TargetPosition => targetPosition;
        public float SpeedValue => speedValue;
        public float TargetSpeedValue => targetSpeedValue;
        public bool IsMoving => moving;

        private void Awake()
        {
            if (movingRoot == null)
                movingRoot = transform;
            targetPosition = movingRoot.position;
            targetSpeedValue = speedValue;
        }

        private void Update()
        {
            if (movingRoot == null)
                return;

            if (moving)
            {
                movingRoot.position = Vector3.MoveTowards(movingRoot.position, targetPosition, moveSpeed * Time.deltaTime);
                speedValue = Mathf.MoveTowards(speedValue, targetSpeedValue, Time.deltaTime * 0.55f);
                if (Vector3.Distance(movingRoot.position, targetPosition) <= 0.01f && Mathf.Abs(speedValue - targetSpeedValue) <= 0.01f)
                {
                    movingRoot.position = targetPosition;
                    speedValue = targetSpeedValue;
                    moving = false;
                }
            }
        }

        public Vector3 ClampPosition(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp(value.x, minPosition.x, maxPosition.x),
                Mathf.Clamp(value.y, minPosition.y, maxPosition.y),
                Mathf.Clamp(value.z, minPosition.z, maxPosition.z));
        }

        public float ClampSpeed(float value)
        {
            return Mathf.Clamp(value, minSpeedValue, maxSpeedValue);
        }

        public void MoveTo(Vector3 worldPosition)
        {
            targetPosition = ClampPosition(worldPosition);
            moving = true;
        }

        public void SetSpeed(float newSpeed)
        {
            targetSpeedValue = ClampSpeed(newSpeed);
            moving = true;
        }
    }
}
