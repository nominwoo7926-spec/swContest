using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace ErgoContest
{
    public sealed class PcErgoInputProvider : ErgoInputProvider
    {
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform handTarget;
        [SerializeField] private Transform workerRoot;
        [SerializeField] private Transform supplyPickup;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private Transform restPoint;
        [SerializeField] private bool enableAutoDemo = true;
        [SerializeField] private float moveSpeed = 1.35f;
        [SerializeField] private float verticalSpeed = 1f;
        [SerializeField] private Vector3 minLocalHand = new Vector3(-1.35f, 0.65f, -0.55f);
        [SerializeField] private Vector3 maxLocalHand = new Vector3(1.2f, 2.1f, 1.05f);

        private ErgoInputFrame frame;
        private bool holdingPart;
        private bool autoDemo;
        private int autoStep;
        private float autoTimer;
        private bool approvalPulse;
        private bool cancelPulse;

        public override ErgoInputFrame CurrentFrame => frame;
        public override Transform HandTarget => handTarget;
        public override Transform HeadTarget => headTarget;
        public override string InputModeLabel => autoDemo ? "PC AUTO" : "PC MANUAL";

        private void Reset()
        {
            handTarget = transform;
        }

        private void Update()
        {
            bool grab = false;
            bool release = false;
            bool approve = false;
            bool cancel = false;
            bool pause = false;

            if (enableAutoDemo && KeyDown(KeyCode.P))
            {
                autoDemo = !autoDemo;
                autoStep = 0;
                autoTimer = 0f;
                approvalPulse = false;
                cancelPulse = false;
                if (!autoDemo)
                {
                    release = holdingPart;
                    holdingPart = false;
                }
            }

            if (KeyDown(KeyCode.C))
            {
                cancel = true;
                cancelPulse = true;
            }

            if (KeyDown(KeyCode.Tab))
                pause = true;

            if (autoDemo)
            {
                RunAutoDemo(ref grab, ref release, ref approve, ref cancel);
            }
            else
            {
                grab = KeyDown(KeyCode.Space) || MouseDown(0);
                release = KeyDown(KeyCode.F) || MouseDown(1);
                approve = KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter);

                if (grab)
                    holdingPart = true;
                if (release)
                    holdingPart = false;

                MoveHandManually();
            }

            if (headTarget != null && workerRoot != null)
            {
                Vector3 look = handTarget != null ? handTarget.position : workerRoot.position + workerRoot.forward;
                Vector3 direction = look - headTarget.position;
                if (direction.sqrMagnitude > 0.01f)
                    headTarget.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            Vector3 headPosition = headTarget != null ? headTarget.position : transform.position + Vector3.up * 1.6f;
            Quaternion headRotation = headTarget != null ? headTarget.rotation : Quaternion.identity;
            Vector3 handPosition = handTarget != null ? handTarget.position : transform.position;
            Quaternion handRotation = handTarget != null ? handTarget.rotation : Quaternion.identity;
            frame = new ErgoInputFrame(headPosition, headRotation, handPosition, handRotation, holdingPart, grab, release, approve, cancel || cancelPulse, pause, false);
            cancelPulse = false;
        }

        private void RunAutoDemo(ref bool grab, ref bool release, ref bool approve, ref bool cancel)
        {
            Vector3 pickup = supplyPickup != null ? supplyPickup.position : transform.position + new Vector3(-0.85f, 1.55f, 0.35f);
            Vector3 assembly = assemblyPoint != null ? assemblyPoint.position : transform.position + new Vector3(0.45f, 1.05f, 0.25f);
            Vector3 rest = restPoint != null ? restPoint.position : transform.position + new Vector3(0.45f, 1.05f, -0.22f);

            switch (autoStep)
            {
                case 0:
                    holdingPart = false;
                    if (MoveHandTo(pickup + Vector3.up * 0.1f))
                        NextStep();
                    break;
                case 1:
                    grab = true;
                    holdingPart = true;
                    NextStep();
                    break;
                case 2:
                    holdingPart = true;
                    if (MoveHandTo(assembly + Vector3.up * 0.15f))
                        NextStep();
                    break;
                case 3:
                    holdingPart = true;
                    if (Wait(0.75f))
                        NextStep();
                    break;
                case 4:
                    release = true;
                    holdingPart = false;
                    NextStep();
                    break;
                case 5:
                    holdingPart = false;
                    if (MoveHandTo(pickup + Vector3.up * 0.1f))
                        NextStep();
                    break;
                case 6:
                    holdingPart = false;
                    if (Wait(0.7f))
                        NextStep();
                    break;
                case 7:
                    holdingPart = false;
                    if (MoveHandTo(rest))
                        NextStep();
                    break;
                case 8:
                    if (!approvalPulse)
                    {
                        approve = true;
                        approvalPulse = true;
                    }
                    if (Wait(1.5f))
                        NextStep();
                    break;
                default:
                    approvalPulse = false;
                    autoStep = 0;
                    break;
            }
        }

        private void MoveHandManually()
        {
            if (handTarget == null)
                return;

            Vector3 delta = Vector3.zero;
            if (KeyHeld(KeyCode.A) || KeyHeld(KeyCode.LeftArrow)) delta.x -= 1f;
            if (KeyHeld(KeyCode.D) || KeyHeld(KeyCode.RightArrow)) delta.x += 1f;
            if (KeyHeld(KeyCode.W) || KeyHeld(KeyCode.UpArrow)) delta.z += 1f;
            if (KeyHeld(KeyCode.S) || KeyHeld(KeyCode.DownArrow)) delta.z -= 1f;
            if (KeyHeld(KeyCode.E)) delta.y += verticalSpeed / moveSpeed;
            if (KeyHeld(KeyCode.Q)) delta.y -= verticalSpeed / moveSpeed;
            if (delta.sqrMagnitude <= 0f)
                return;

            Transform frameRoot = workerRoot != null ? workerRoot : transform;
            Vector3 local = frameRoot.InverseTransformPoint(handTarget.position);
            local += Vector3.ClampMagnitude(delta, 1f) * (moveSpeed * Time.deltaTime);
            local.x = Mathf.Clamp(local.x, minLocalHand.x, maxLocalHand.x);
            local.y = Mathf.Clamp(local.y, minLocalHand.y, maxLocalHand.y);
            local.z = Mathf.Clamp(local.z, minLocalHand.z, maxLocalHand.z);
            handTarget.position = frameRoot.TransformPoint(local);
        }

        private bool MoveHandTo(Vector3 target)
        {
            if (handTarget == null)
                return true;

            handTarget.position = Vector3.MoveTowards(handTarget.position, target, moveSpeed * Time.deltaTime);
            return Vector3.Distance(handTarget.position, target) <= 0.025f;
        }

        private bool Wait(float seconds)
        {
            autoTimer += Time.deltaTime;
            return autoTimer >= seconds;
        }

        private void NextStep()
        {
            autoStep++;
            autoTimer = 0f;
            approvalPulse = false;
        }

        private bool KeyHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = GetKey(key);
            if (control != null)
                return control.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        private bool KeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = GetKey(key);
            if (control != null)
                return control.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        private bool MouseDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
                return button == 0 ? mouse.leftButton.wasPressedThisFrame : mouse.rightButton.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl GetKey(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return null;
            switch (key)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.F: return keyboard.fKey;
                case KeyCode.P: return keyboard.pKey;
                case KeyCode.C: return keyboard.cKey;
                case KeyCode.Tab: return keyboard.tabKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.Return: return keyboard.enterKey;
                case KeyCode.KeypadEnter: return keyboard.numpadEnterKey;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                default: return null;
            }
        }
#endif
    }
}
