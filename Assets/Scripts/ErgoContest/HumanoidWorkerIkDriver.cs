using UnityEngine;

namespace ErgoContest
{
    public sealed class HumanoidWorkerIkDriver : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private ErgoLoadEstimator loadEstimator;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform workerRoot;
        [SerializeField] private Renderer[] shoulderArmHandRenderers;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftHandAssistTarget;
        [SerializeField] private Transform headLookTarget;
        [SerializeField] private float rightHandIkWeight = 1f;
        [SerializeField] private float leftHandIkWeight = 0.55f;
        [SerializeField] private float lookAtWeight = 0.75f;

        private MaterialPropertyBlock block;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (loadEstimator != null)
                ApplyLoadColor(loadEstimator.Current.DisplayColor);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || inputProvider == null || !animator.isHuman)
                return;

            ErgoInputFrame input = inputProvider.CurrentFrame;
            Transform frame = workerRoot != null ? workerRoot : transform;
            Vector3 rightHand = rightHandTarget != null ? rightHandTarget.position : input.HandPosition;
            Quaternion rightRotation = rightHandTarget != null ? rightHandTarget.rotation : input.HandRotation;
            Vector3 leftHand = leftHandAssistTarget != null
                ? leftHandAssistTarget.position
                : rightHand + frame.TransformVector(new Vector3(-0.18f, -0.04f, 0.02f));
            Vector3 lookAt = headLookTarget != null
                ? headLookTarget.position
                : Vector3.Lerp(rightHand, input.HeadPosition + input.HeadRotation * Vector3.forward, 0.2f);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandIkWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandIkWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHand);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightRotation);

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandIkWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftHandIkWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHand);
            Vector3 leftDirection = rightHand - leftHand;
            if (leftDirection.sqrMagnitude > 0.001f)
                animator.SetIKRotation(AvatarIKGoal.LeftHand, Quaternion.LookRotation(leftDirection.normalized, Vector3.up));

            animator.SetLookAtWeight(lookAtWeight, 0.35f, 0.85f, 0.4f, 0.5f);
            animator.SetLookAtPosition(lookAt);
        }

        private void ApplyLoadColor(Color color)
        {
            if (shoulderArmHandRenderers == null)
                return;

            foreach (Renderer renderer in shoulderArmHandRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
