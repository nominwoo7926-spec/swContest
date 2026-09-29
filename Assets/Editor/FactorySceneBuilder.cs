// Put this file in Assets/Editor/FactorySceneBuilder.cs.
// In Unity, run Tools > ErgoCell > Create Factory Scene.
// This editor utility creates a separate scene and never overwrites an existing one.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FactorySceneBuilder
{
    private const string SceneFolder = "Assets/Scenes";

    [MenuItem("Tools/ErgoCell/Create Factory Scene")]
    public static void CreateFactoryScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        Material concrete = Mat("Warm Concrete", new Color(0.38f, 0.42f, 0.44f));
        Material wall = Mat("Factory Wall", new Color(0.70f, 0.75f, 0.77f));
        Material steel = Mat("Painted Steel", new Color(0.18f, 0.25f, 0.30f));
        Material tabletop = Mat("Work Surface", new Color(0.25f, 0.45f, 0.56f));
        Material orange = Mat("Machine Orange", new Color(1.0f, 0.43f, 0.10f));
        Material yellow = Mat("Safety Yellow", new Color(1.0f, 0.76f, 0.10f));
        Material belt = Mat("Conveyor Belt", new Color(0.11f, 0.13f, 0.15f));
        Material green = Mat("Go Green", new Color(0.12f, 0.63f, 0.38f));
        Material parts = Mat("Parts", new Color(0.82f, 0.85f, 0.88f));

        var site = Group("ErgoCell - Assembly Factory");
        var architecture = Group("01 Architecture", site.transform);
        Box("Floor", architecture.transform, new Vector3(0, -0.11f, 0), new Vector3(12, 0.2f, 9), concrete);
        Box("Back Wall", architecture.transform, new Vector3(0, 2.5f, 4.5f), new Vector3(12, 5, 0.15f), wall);
        Box("Left Wall", architecture.transform, new Vector3(-6, 2.5f, 0), new Vector3(0.15f, 5, 9), wall);
        Box("Wall Stripe", architecture.transform, new Vector3(0, 1.2f, 4.39f), new Vector3(11.8f, 0.15f, 0.03f), orange);
        for (int i = -5; i <= 5; i++)
            Box("Floor Grid X " + i, architecture.transform, new Vector3(i, 0.004f, 0), new Vector3(0.015f, 0.006f, 8.7f), wall);
        for (int i = -4; i <= 4; i++)
            Box("Floor Grid Z " + i, architecture.transform, new Vector3(0, 0.004f, i), new Vector3(11.7f, 0.006f, 0.015f), wall);

        // Keep this object's transform as the future height-control target.
        var bench = Group("AdjustableWorktable", site.transform);
        bench.transform.position = new Vector3(0, 0, 0.5f);
        Box("Work Surface", bench.transform, new Vector3(0, 0.9f, 0), new Vector3(2.2f, 0.09f, 1.15f), tabletop);
        foreach (float x in new[] { -0.92f, 0.92f })
        foreach (float z in new[] { -0.43f, 0.43f })
            Box("Adjustable Leg", bench.transform, new Vector3(x, 0.44f, z), new Vector3(0.09f, 0.88f, 0.09f), steel);
        Box("Bench Frame", bench.transform, new Vector3(0, 0.32f, 0), new Vector3(2.0f, 0.12f, 0.88f), steel);
        Box("Assembly Fixture", bench.transform, new Vector3(0.40f, 1.0f, 0.08f), new Vector3(0.5f, 0.12f, 0.4f), steel);
        Box("Target Indicator", bench.transform, new Vector3(0.40f, 1.07f, 0.08f), new Vector3(0.32f, 0.008f, 0.22f), green);
        Label("ADJUSTABLE WORKTABLE", bench.transform, new Vector3(0, 1.15f, 0.55f), 0.15f, Color.white);

        // Keep this object's transform as the future supply-position target.
        var supply = Group("MovablePartsSupply", site.transform);
        supply.transform.position = new Vector3(-1.9f, 0, 0.35f);
        Box("Supply Frame", supply.transform, new Vector3(0, 0.45f, 0), new Vector3(0.68f, 0.8f, 0.75f), steel);
        Box("Supply Shelf", supply.transform, new Vector3(0, 0.90f, 0), new Vector3(0.75f, 0.08f, 0.8f), orange);
        for (int i = 0; i < 3; i++)
        {
            var part = Box("Workpiece_3kg_" + (i + 1), supply.transform,
                new Vector3(-0.22f + 0.22f * i, 1.01f, 0), new Vector3(0.16f, 0.15f, 0.18f), parts);
            part.AddComponent<Rigidbody>().isKinematic = true;
        }
        Label("3 kg / simulated", supply.transform, new Vector3(0, 1.22f, 0.44f), 0.11f, Color.white);

        var conveyor = Group("Conveyor - Visual Placeholder", site.transform);
        conveyor.transform.position = new Vector3(2.8f, 0, 0.5f);
        Box("Conveyor Housing", conveyor.transform, new Vector3(0, 0.65f, 0), new Vector3(2.3f, 0.28f, 0.8f), steel);
        Box("Belt", conveyor.transform, new Vector3(0, 0.81f, 0), new Vector3(2.18f, 0.045f, 0.64f), belt);
        foreach (float x in new[] { -0.95f, 0.95f })
        foreach (float z in new[] { -0.28f, 0.28f })
            Box("Conveyor Leg", conveyor.transform, new Vector3(x, 0.31f, z), new Vector3(0.08f, 0.62f, 0.08f), steel);
        Box("Emergency Stop", conveyor.transform, new Vector3(-1.1f, 0.93f, -0.45f), new Vector3(0.18f, 0.18f, 0.15f), orange);

        var safety = Group("03 Safety Floor Markings", site.transform);
        Rectangle("Work Zone", safety.transform, new Vector3(0, 0.017f, 0.25f), 3.3f, 2.8f, yellow);
        Rectangle("Supply Travel Zone", safety.transform, new Vector3(-1.95f, 0.018f, 0.35f), 1.5f, 2.1f, orange);
        for (int i = -2; i <= 2; i++)
            Box("Walkway Stripe " + i, safety.transform, new Vector3(i * 0.70f, 0.018f, -2.6f), new Vector3(0.35f, 0.008f, 0.09f), yellow);

        var decor = Group("04 Factory Details", site.transform);
        Box("Storage Rack", decor.transform, new Vector3(-4.5f, 1.5f, 2.5f), new Vector3(1.6f, 3f, 0.75f), steel);
        for (int i = 0; i < 3; i++)
            Box("Rack Shelf " + i, decor.transform, new Vector3(-4.5f, 0.5f + i, 2.48f), new Vector3(1.7f, 0.05f, 0.85f), orange);
        Box("Control Panel", decor.transform, new Vector3(3.8f, 1.25f, 2.3f), new Vector3(0.8f, 1f, 0.18f), steel);
        Box("Control Screen", decor.transform, new Vector3(3.8f, 1.37f, 2.19f), new Vector3(0.6f, 0.4f, 0.02f), green);
        Label("ERGOCELL  |  ASSEMBLY 01", architecture.transform,
            new Vector3(0, 3.6f, 4.37f), 0.38f, steel.color);

        var lights = Group("05 Lighting and Preview Camera", site.transform);
        Ambient(lights.transform, new Vector3(-3, 4, -2), 900, 3.5f);
        Ambient(lights.transform, new Vector3(2, 4, 1), 850, 3.5f);
        var cameraGo = new GameObject("Preview Camera - replace with XR Origin later");
        cameraGo.transform.SetParent(lights.transform);
        cameraGo.transform.position = new Vector3(0, 1.6f, -3.2f);
        cameraGo.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.0f, 0.5f) - cameraGo.transform.position);
        var camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 70;
        cameraGo.AddComponent<AudioListener>();

        string scenePath = AssetDatabase.GenerateUniqueAssetPath(SceneFolder + "/ErgoCellFactory.unity");
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = bench;
        Debug.Log("Factory scene created: " + scenePath + ". The scene is visual only; add XR input and agent control next.");
    }

    private static GameObject Group(string name, Transform parent = null)
    {
        var result = new GameObject(name);
        if (parent != null) result.transform.SetParent(parent, false);
        return result;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
        result.name = name;
        result.transform.SetParent(parent, false);
        result.transform.localPosition = localPosition;
        result.transform.localScale = size;
        result.GetComponent<Renderer>().sharedMaterial = material;
        return result;
    }

    private static void Rectangle(string name, Transform parent, Vector3 center, float width, float length, Material mat)
    {
        Box(name + " Front", parent, center + new Vector3(0, 0, -length / 2), new Vector3(width, 0.008f, 0.035f), mat);
        Box(name + " Back", parent, center + new Vector3(0, 0, length / 2), new Vector3(width, 0.008f, 0.035f), mat);
        Box(name + " Left", parent, center + new Vector3(-width / 2, 0, 0), new Vector3(0.035f, 0.008f, length), mat);
        Box(name + " Right", parent, center + new Vector3(width / 2, 0, 0), new Vector3(0.035f, 0.008f, length), mat);
    }

    private static void Label(string text, Transform parent, Vector3 position, float characterSize, Color color)
    {
        var result = new GameObject("Label - " + text);
        result.transform.SetParent(parent, false);
        result.transform.localPosition = position;
        result.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var label = result.AddComponent<TextMesh>();
        label.text = text;
        label.characterSize = characterSize;
        label.fontSize = 64;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = color;
    }

    private static Material Mat(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = name, color = color };
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Materials/" + name.Replace(' ', '_') + ".mat");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Ambient(Transform parent, Vector3 position, float intensity, float range)
    {
        var result = new GameObject("Area-style Light");
        result.transform.SetParent(parent, false);
        result.transform.localPosition = position;
        var light = result.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.range = range;
    }
}
