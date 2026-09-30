using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ErgoContest
{
    public sealed class QuestErgoInputProvider : ErgoInputProvider
    {
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform handTarget;
        [SerializeField] private Transform fallbackWorkerRoot;
        [SerializeField] private PcErgoInputProvider editorFallback;
        [SerializeField] private XRNode handNode = XRNode.RightHand;

        private static readonly List<InputDevice> Devices = new List<InputDevice>();
        private ErgoInputFrame frame;
        private bool holdingPart;
        private bool previousGrabIntent;
        private bool previousApprove;
        private bool previousCancel;
        private bool previousPause;

        public override ErgoInputFrame CurrentFrame => frame;
        public override Transform HandTarget => handTarget;
        public override Transform HeadTarget => headTarget;
        public override string InputModeLabel => frame.TrackedVrInput ? "QUEST 2" : (editorFallback != null ? editorFallback.InputModeLabel : "NO INPUT");

        private void Update()
        {
            bool tracked = TryReadVr(out Vector3 headPos, out Quaternion headRot, out Vector3 handPos, out Quaternion handRot, out bool grip, out bool approveHeld, out bool cancelHeld, out bool pauseHeld);
            if (!tracked)
            {
                if (editorFallback != null)
                {
                    frame = editorFallback.CurrentFrame;
                    holdingPart = frame.HoldingPart;
                }
                return;
            }

            bool grabPressed = grip && !previousGrabIntent;
            bool releasePressed = !grip && previousGrabIntent;
            bool approvePressed = approveHeld && !previousApprove;
            bool cancelPressed = cancelHeld && !previousCancel;
            bool pausePressed = pauseHeld && !previousPause;
            holdingPart = grip;

            previousGrabIntent = grip;
            previousApprove = approveHeld;
            previousCancel = cancelHeld;
            previousPause = pauseHeld;

            if (headTarget != null)
            {
                headTarget.position = headPos;
                headTarget.rotation = headRot;
            }

            if (handTarget != null)
            {
                handTarget.position = handPos;
                handTarget.rotation = handRot;
            }

            frame = new ErgoInputFrame(headPos, headRot, handPos, handRot, holdingPart, grabPressed, releasePressed, approvePressed, cancelPressed, pausePressed, true);
        }

        private bool TryReadVr(
            out Vector3 headPos,
            out Quaternion headRot,
            out Vector3 handPos,
            out Quaternion handRot,
            out bool grip,
            out bool approve,
            out bool cancel,
            out bool pause)
        {
            headPos = fallbackWorkerRoot != null ? fallbackWorkerRoot.position + Vector3.up * 1.6f : transform.position + Vector3.up * 1.6f;
            headRot = Quaternion.identity;
            handPos = handTarget != null ? handTarget.position : transform.position;
            handRot = Quaternion.identity;
            grip = false;
            approve = false;
            cancel = false;
            pause = false;

            InputDevices.GetDevicesAtXRNode(XRNode.Head, Devices);
            bool hasHead = false;
            for (int i = 0; i < Devices.Count; i++)
            {
                InputDevice device = Devices[i];
                if (!device.isValid)
                    continue;
                hasHead |= device.TryGetFeatureValue(CommonUsages.devicePosition, out headPos);
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out headRot);
            }

            InputDevices.GetDevicesAtXRNode(handNode, Devices);
            bool hasHand = false;
            for (int i = 0; i < Devices.Count; i++)
            {
                InputDevice device = Devices[i];
                if (!device.isValid)
                    continue;
                hasHand |= device.TryGetFeatureValue(CommonUsages.devicePosition, out handPos);
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out handRot);
                device.TryGetFeatureValue(CommonUsages.gripButton, out grip);
                bool primary = device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryValue) && primaryValue;
                bool secondary = device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryValue) && secondaryValue;
                bool menu = device.TryGetFeatureValue(CommonUsages.menuButton, out bool menuValue) && menuValue;
                approve = primary;
                cancel = secondary;
                pause = menu;
            }

            return hasHead && hasHand;
        }
    }
}
