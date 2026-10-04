using System;
using UnityEngine;

namespace FactoryTask
{
    // Renders 3D human hand models for 1st-person VR controllers with interactive finger curling.
    [DefaultExecutionOrder(150)]
    public sealed class VRHandVisualizer : MonoBehaviour
    {
        public HandSide side;
        public XRTrackingProvider tracking;
        public Material handMaterial;
        public Material accentMaterial;

        Transform palm, thumbBase, thumbTip;
        Transform[] fingerBases = new Transform[4];
        Transform[] fingerTips = new Transform[4];
        Quaternion[] baseRestRotations = new Quaternion[4];
        Quaternion thumbRestRotation;
        float currentCurl = 0f;
        bool built;

        void Awake()
        {
            BuildHand();
        }

        void OnEnable()
        {
            BuildHand();
        }

        public void BuildHand()
        {
            if (built) return;
            built = true;

            // Hide/remove legacy primitive box markers (Grip_Marker, Thumb_Marker)
            foreach (Transform child in transform)
            {
                if (child.name == "Grip_Marker" || child.name == "Thumb_Marker")
                {
                    child.gameObject.SetActive(false);
                }
            }

            float s = (side == HandSide.Left) ? -1f : 1f;

            // Setup builds the hand in the editor and saves it into the scene. At runtime the
            // non-serialized 'built' flag is false again, so re-bind the saved model instead of
            // building a second hand on top of it.
            var existing = transform.Find("Human_Hand_Model");
            if (existing != null) { BindExisting(existing, s); return; }

            // Hand root container
            var modelContainer = new GameObject("Human_Hand_Model");
            modelContainer.transform.SetParent(transform, false);
            modelContainer.transform.localPosition = Vector3.zero;
            modelContainer.transform.localRotation = Quaternion.Euler(0, 0, s * 90f);

            // 1. Wrist Cuff
            var wrist = CreatePrimitive(PrimitiveType.Cube, "Wrist", modelContainer.transform,
                new Vector3(0, -0.015f, -0.015f), new Vector3(0.062f, 0.032f, 0.038f), accentMaterial);

            // 2. Palm (Main hand body)
            palm = CreatePrimitive(PrimitiveType.Cube, "Palm", modelContainer.transform,
                new Vector3(s * 0.003f, -0.008f, 0.035f), new Vector3(0.074f, 0.024f, 0.075f), handMaterial).transform;

            // Palm eminence (thumb pad cushion)
            CreatePrimitive(PrimitiveType.Cube, "Palm_Pad", palm,
                new Vector3(s * 0.028f, -0.005f, -0.015f), new Vector3(0.028f, 0.018f, 0.04f), accentMaterial);

            // 3. Thumb
            var thumbRoot = new GameObject("Thumb_Root").transform;
            thumbRoot.SetParent(modelContainer.transform, false);
            thumbRoot.localPosition = new Vector3(s * 0.034f, 0.002f, 0.018f);
            thumbRestRotation = Quaternion.Euler(-25f, s * 40f, s * 35f);
            thumbRoot.localRotation = thumbRestRotation;
            thumbBase = thumbRoot;

            var thumbSeg1 = CreatePrimitive(PrimitiveType.Cube, "Thumb_Proximal", thumbRoot,
                new Vector3(0, 0, 0.018f), new Vector3(0.020f, 0.018f, 0.032f), handMaterial).transform;

            thumbTip = CreatePrimitive(PrimitiveType.Cube, "Thumb_Distal", thumbSeg1,
                new Vector3(0, 0, 0.026f), new Vector3(0.018f, 0.016f, 0.024f), accentMaterial).transform;
            thumbTip.localRotation = Quaternion.Euler(15f, 0, 0);

            // 4. Four Fingers (Index, Middle, Ring, Pinky)
            float[] xOffsets = new float[] { s * 0.026f, s * 0.008f, s * -0.009f, s * -0.025f };
            float[] lengths = new float[] { 0.038f, 0.042f, 0.038f, 0.032f };
            float[] widths = new float[] { 0.017f, 0.017f, 0.016f, 0.015f };
            string[] names = new string[] { "Index", "Middle", "Ring", "Pinky" };

            for (int i = 0; i < 4; i++)
            {
                var fingerRoot = new GameObject(names[i] + "_Finger").transform;
                fingerRoot.SetParent(modelContainer.transform, false);
                fingerRoot.localPosition = new Vector3(xOffsets[i], -0.005f, 0.072f);
                baseRestRotations[i] = Quaternion.Euler(12f, s * (i - 1.5f) * 2f, 0);
                fingerRoot.localRotation = baseRestRotations[i];
                fingerBases[i] = fingerRoot;

                // Proximal segment
                var pSeg = CreatePrimitive(PrimitiveType.Cube, names[i] + "_Proximal", fingerRoot,
                    new Vector3(0, 0, lengths[i] * 0.45f), new Vector3(widths[i], 0.016f, lengths[i] * 0.85f), handMaterial).transform;

                // Distal segment (tip)
                var dTip = CreatePrimitive(PrimitiveType.Cube, names[i] + "_Distal", pSeg,
                    new Vector3(0, 0, lengths[i] * 0.75f), new Vector3(widths[i] * 0.9f, 0.014f, lengths[i] * 0.65f), accentMaterial).transform;
                dTip.localRotation = Quaternion.Euler(18f, 0, 0);
                fingerTips[i] = dTip;
            }

            // Set all renderers to VrLayer (28) so 1st person camera renders them
            SetLayer(modelContainer, 28);
        }

        void Update()
        {
            if (tracking == null) return;
            bool grip = (side == HandSide.Left) ? tracking.LeftGrip : tracking.RightGrip;
            
            // 박스를 잡는 상태(0.0 ~ 1.0) 보간
            float targetState = grip ? 1f : 0f;
            currentCurl = Mathf.Lerp(currentCurl, targetState, 1f - Mathf.Exp(-Time.deltaTime * 18f));

            // 박스 잡기(Box Grab)를 위한 관절별 타겟 각도 세팅
            float proximalAngle = 35f;  // 손가락 첫 마디: 박스 윗면을 덮도록 구부림
            float distalAngle = 55f;    // 손가락 끝 마디: 박스 모서리를 감싸쥐도록 깊게 구부림
            float thumbBaseAngle = 25f; // 엄지 안쪽: 박스 측면을 지지하도록 구부림
            float thumbTipAngle = 20f;  // 엄지 끝: 박스를 꽉 쥐도록 구부림

            // 4개 손가락(검지~새끼) 애니메이션 적용
            for (int i = 0; i < 4; i++)
            {
                if (fingerBases[i] != null)
                {
                    fingerBases[i].localRotation = baseRestRotations[i] * Quaternion.AngleAxis(currentCurl * proximalAngle, Vector3.right);
                }
                if (fingerTips[i] != null)
                {
                    // 끝 마디는 기본 18도 + 그립 시 추가 각도
                    fingerTips[i].localRotation = Quaternion.Euler(18f + (currentCurl * distalAngle), 0, 0);
                }
            }
            
            // 엄지손가락 애니메이션 적용
            if (thumbBase != null)
            {
                thumbBase.localRotation = thumbRestRotation * Quaternion.AngleAxis(currentCurl * thumbBaseAngle, Vector3.right);
            }
            if (thumbTip != null)
            {
                thumbTip.localRotation = Quaternion.Euler(15f + (currentCurl * thumbTipAngle), 0, 0);
            }
        }

        void BindExisting(Transform model, float s)
        {
            string[] names = { "Index", "Middle", "Ring", "Pinky" };
            thumbRestRotation = Quaternion.Euler(-25f, s * 40f, s * 35f);
            palm = model.Find("Palm");
            thumbBase = model.Find("Thumb_Root");
            thumbTip = thumbBase != null ? thumbBase.Find("Thumb_Proximal/Thumb_Distal") : null;
            for (int i = 0; i < 4; i++)
            {
                baseRestRotations[i] = Quaternion.Euler(12f, s * (i - 1.5f) * 2f, 0);
                fingerBases[i] = model.Find(names[i] + "_Finger");
                fingerTips[i] = fingerBases[i] != null ? fingerBases[i].Find(names[i] + "_Proximal/" + names[i] + "_Distal") : null;
            }
        }

        GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            // Remove collider from visual mesh to prevent physics interference
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            return go;
        }

        void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }
}
