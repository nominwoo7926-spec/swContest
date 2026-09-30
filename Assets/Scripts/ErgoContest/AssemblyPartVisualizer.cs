using UnityEngine;

namespace ErgoContest
{
    public sealed class AssemblyPartVisualizer : MonoBehaviour
    {
        [SerializeField] private ErgoInputProvider inputProvider;
        [SerializeField] private Transform handTarget;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private Transform carriedPart;
        [SerializeField] private Transform placedPart;
        [SerializeField] private Transform[] trayParts;
        [SerializeField] private float placeDistance = 0.42f;
        [SerializeField] private Vector3 carryOffset = new Vector3(0f, -0.08f, 0f);
        [SerializeField] private Vector3 placedOffset = new Vector3(0f, 0.02f, 0f);

        private bool wasHolding;
        private int trayIndex;

        private void Awake()
        {
            if (carriedPart != null)
                carriedPart.gameObject.SetActive(false);
            if (placedPart != null)
                placedPart.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (inputProvider == null)
                return;

            ErgoInputFrame input = inputProvider.CurrentFrame;
            if (input.HoldingPart && !wasHolding)
                HideNextTrayPart();
            if (!input.HoldingPart && wasHolding)
                TryPlacePart(input.HandPosition);

            if (carriedPart != null)
            {
                carriedPart.gameObject.SetActive(input.HoldingPart);
                if (input.HoldingPart)
                {
                    Transform hand = handTarget != null ? handTarget : inputProvider.HandTarget;
                    carriedPart.position = (hand != null ? hand.position : input.HandPosition) + carryOffset;
                    carriedPart.rotation = input.HandRotation;
                }
            }

            wasHolding = input.HoldingPart;
        }

        private void HideNextTrayPart()
        {
            if (trayParts == null || trayParts.Length == 0)
                return;

            Transform target = trayParts[trayIndex % trayParts.Length];
            if (target != null)
                target.gameObject.SetActive(false);
            trayIndex++;
        }

        private void TryPlacePart(Vector3 handPosition)
        {
            if (assemblyPoint == null || placedPart == null)
                return;
            if (Vector3.Distance(handPosition, assemblyPoint.position) > placeDistance)
                return;

            placedPart.position = assemblyPoint.position + placedOffset;
            placedPart.rotation = assemblyPoint.rotation;
            placedPart.gameObject.SetActive(true);
        }
    }
}
