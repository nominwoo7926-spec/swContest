using ErgoContest;
using UnityEditor;
using UnityEngine;

public static class HumanoidAvatarBinder
{
    private const string BundledFarmerPath = "Assets/ThirdParty/Quaternius/farmer.glb";

    [MenuItem("Tools/Ergo Contest/Place Bundled Farmer Avatar")]
    public static void PlaceBundledFarmerAvatar()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(BundledFarmerPath);
        if (asset == null)
        {
            Debug.LogWarning("Bundled farmer model is present but not imported as a GameObject yet. Wait for com.unity.cloud.gltfast to finish resolving/importing, then run this again.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        if (instance == null)
            instance = Object.Instantiate(asset);
        instance.name = "Humanoid Worker Avatar - Quaternius Farmer";

        GameObject fallback = GameObject.Find("Tracked Worker Diagram");
        if (fallback != null)
        {
            instance.transform.position = fallback.transform.position;
            instance.transform.rotation = fallback.transform.rotation;
            foreach (Renderer renderer in fallback.GetComponentsInChildren<Renderer>())
                renderer.enabled = false;
        }
        else
        {
            instance.transform.position = new Vector3(-0.05f, 0f, -0.38f);
        }

        Selection.activeGameObject = instance;
        BindSelectedHumanoidAvatar();
    }

    [MenuItem("Tools/Ergo Contest/Bind Selected Humanoid Avatar")]
    public static void BindSelectedHumanoidAvatar()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Select a Humanoid avatar GameObject first.");
            return;
        }

        Animator animator = selected.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Selected object has no Animator.");
            return;
        }

        if (!animator.isHuman)
            Debug.LogWarning("Animator is not configured as Humanoid. Set the model Rig import setting to Humanoid.");

        var input = Object.FindFirstObjectByType<ErgoInputProvider>();
        var load = Object.FindFirstObjectByType<ErgoLoadEstimator>();
        if (input == null || load == null)
        {
            Debug.LogWarning("Create the Ergo Contest VR Demo Scene first, then bind the avatar.");
            return;
        }

        HumanoidWorkerIkDriver driver = selected.GetComponent<HumanoidWorkerIkDriver>();
        if (driver == null)
            driver = selected.AddComponent<HumanoidWorkerIkDriver>();

        Transform targetRoot = EnsureChild(selected.transform, "Ergo IK Targets");
        Transform rightHandTarget = EnsureChild(targetRoot, "Right Hand IK Target");
        Transform leftHandTarget = EnsureChild(targetRoot, "Left Hand IK Target");
        Transform headLookTarget = EnsureChild(targetRoot, "Head Look Target");
        rightHandTarget.position = input.CurrentFrame.HandPosition;
        leftHandTarget.position = input.CurrentFrame.HandPosition + selected.transform.TransformVector(new Vector3(-0.18f, -0.04f, 0.02f));
        headLookTarget.position = input.CurrentFrame.HandPosition + Vector3.up * 0.1f;

        HumanoidWorkMotionDirector motion = selected.GetComponent<HumanoidWorkMotionDirector>();
        if (motion == null)
            motion = selected.AddComponent<HumanoidWorkMotionDirector>();

        SerializedObject motionSerialized = new SerializedObject(motion);
        Set(motionSerialized, "inputProvider", input);
        Set(motionSerialized, "rightHandTarget", rightHandTarget);
        Set(motionSerialized, "leftHandTarget", leftHandTarget);
        Set(motionSerialized, "headLookTarget", headLookTarget);
        Transform supply = FindTransform("Supply Pickup Point");
        Transform assembly = FindTransform("Assembly Point");
        Transform rest = FindTransform("Safe Rest Point");
        Set(motionSerialized, "supplyPickupPoint", supply);
        Set(motionSerialized, "assemblyPoint", assembly);
        Set(motionSerialized, "restPoint", rest);
        motionSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serialized = new SerializedObject(driver);
        Set(serialized, "inputProvider", input);
        Set(serialized, "loadEstimator", load);
        Set(serialized, "animator", animator);
        Set(serialized, "workerRoot", selected.transform);
        Set(serialized, "rightHandTarget", rightHandTarget);
        Set(serialized, "leftHandAssistTarget", leftHandTarget);
        Set(serialized, "headLookTarget", headLookTarget);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AddAnimationRiggingHooksIfPackageResolved(selected);
        Debug.Log("Bound selected Humanoid avatar to Ergo Contest input and load visualization.");
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void AddAnimationRiggingHooksIfPackageResolved(GameObject avatarRoot)
    {
        System.Type rigBuilderType = System.Type.GetType("UnityEngine.Animations.Rigging.RigBuilder, Unity.Animation.Rigging");
        System.Type rigType = System.Type.GetType("UnityEngine.Animations.Rigging.Rig, Unity.Animation.Rigging");
        if (rigBuilderType == null || rigType == null)
        {
            Debug.Log("Animation Rigging package is not resolved yet. Unity will load it after Package Manager finishes installing com.unity.animation.rigging.");
            return;
        }

        if (avatarRoot.GetComponent(rigBuilderType) == null)
            avatarRoot.AddComponent(rigBuilderType);

        Transform rigRoot = avatarRoot.transform.Find("Ergo Animation Rig");
        if (rigRoot == null)
        {
            GameObject rigObject = new GameObject("Ergo Animation Rig");
            rigObject.transform.SetParent(avatarRoot.transform, false);
            rigRoot = rigObject.transform;
        }

        if (rigRoot.GetComponent(rigType) == null)
            rigRoot.gameObject.AddComponent(rigType);
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform FindTransform(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.transform : null;
    }
}
