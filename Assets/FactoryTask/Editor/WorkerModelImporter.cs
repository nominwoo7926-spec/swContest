using UnityEditor;
using UnityEngine;

// Restricted to the bundled Rocketbox asset; does not alter other project models.
public sealed class WorkerModelImporter : AssetPostprocessor
{
    const string Folder = "Assets/FactoryTask/Avatar/Rocketbox/";
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.isReadable = true;
        importer.optimizeGameObjects = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 2048;
        importer.mipmapEnabled = true;
        if (assetPath.Contains("_normal")) importer.textureType = TextureImporterType.NormalMap;
    }
    public static void InspectBatch()
    {
        AssetDatabase.Refresh();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Construction_Male_05.fbx");
        if (model == null) throw new System.Exception("Worker FBX not imported");
        var go = Object.Instantiate(model);
        var report = new System.Text.StringBuilder();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            report.AppendLine(t.name + " | " + t.position.ToString("F3") + " | " + t.rotation.eulerAngles.ToString("F1"));
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            report.AppendLine("MESH " + r.name + " vertices=" + r.sharedMesh.vertexCount + " bounds=" + r.bounds);
            foreach (var m in r.sharedMaterials) report.AppendLine("MATERIAL " + m.name + " tex=" + (m.mainTexture != null ? m.mainTexture.name : "null"));
        }
        System.IO.Directory.CreateDirectory("Documentation/Avatar");
        System.IO.File.WriteAllText("Documentation/Avatar/ModelInspection.txt", report.ToString());
        Object.DestroyImmediate(go);
    }
}
