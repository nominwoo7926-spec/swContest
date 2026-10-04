using System.IO;
using UnityEditor.Android;

// Unity OpenXR and Meta OVRPlugin both ship lib/arm64-v8a/libopenxr_loader.so; keep a single copy in the APK.
public class OpenXRLoaderGradleFix : IPostGenerateGradleAndroidProject
{
    const string Marker="// FactoryTask: openxr loader pickFirst";
    public int callbackOrder=>999;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        var launcher=Path.Combine(Path.GetDirectoryName(unityLibraryPath),"launcher");
        foreach(var name in new[]{"build.gradle","build.gradle.kts"})
        {
            var file=Path.Combine(launcher,name);
            if(!File.Exists(file))continue;
            var text=File.ReadAllText(file);
            if(text.Contains(Marker))return;
            File.AppendAllText(file,"\n"+Marker+"\nandroid { packaging { jniLibs { pickFirsts.add(\"**/libopenxr_loader.so\") } } }\n");
            return;
        }
    }
}
