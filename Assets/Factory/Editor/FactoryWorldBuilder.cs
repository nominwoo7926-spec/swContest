using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Authoring only. All output is ordinary serialized meshes, materials, prefabs and a scene.
public static class FactoryWorldBuilder
{
    const string Root = "Assets/Factory";
    public const string ScenePath = Root + "/Scenes/SmartFactory.unity";
    static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
    static Transform architecture, safety, props, lighting;
    static int[] glyphWidths;
    static Material textMaterial;

    [MenuItem("Tools/Smart Factory/Build Complete Factory")]
    public static void BuildCompleteFactory(){Build();BakeAndCapture();}

    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        foreach (var folder in new[] {"Scenes", "Prefabs", "Materials", "Meshes", "Textures"}) Directory.CreateDirectory(Root + "/" + folder);
        Directory.CreateDirectory("Documentation");
        AssetDatabase.Refresh();
        Mats.Clear(); Meshes.Clear(); Prefabs.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigureMaterials();
        architecture = Group("Factory_Architecture");
        var lineA = Group("Production_Line_A"); var lineB = Group("Production_Line_B");
        var stations = Group("Workstations"); safety = Group("Safety_Equipment"); props = Group("Factory_Props");
        lighting = Group("Lighting"); var cameras = Group("Cameras");
        MakeTemplates();
        BuildArchitecture();
        BuildLine(lineA, -3.05f, "A", "Blue");
        BuildLine(lineB, 3.05f, "B", "Teal");
        Instance("Adjustable_Workbench", stations, new Vector3(5.55f, 0, -3.05f));
        Instance("Adjustable_Workbench", stations, new Vector3(5.55f, 0, 3.05f));
        Populate();
        BuildLighting();
        var cam = Group("MainCamera_Presentation", cameras).gameObject.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.transform.position = new Vector3(-11.55f, 4.45f, -7.75f);
        cam.transform.LookAt(new Vector3(.4f, 1.25f, .65f));
        cam.fieldOfView = 66; cam.nearClipPlane = .1f; cam.farClipPlane = 70;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.37f,.43f,.48f);
        cam.allowHDR = false; cam.allowMSAA = true;
        var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
        QualitySettings.vSyncCount = 1;
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.ImportAsset(ScenePath);
        var entry=new EditorBuildSettingsScene(ScenePath,true);entry.guid=new GUID(AssetDatabase.AssetPathToGUID(ScenePath));EditorBuildSettings.scenes = new[] {entry};
        PlayerSettings.SetPreloadedAssets(Array.Empty<Object>());
        PlayerSettings.companyName="NOVA Manufacturing";PlayerSettings.productName="NOVA Smart Factory";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.nova.smartfactory");
        AssetDatabase.SaveAssets();
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,0), cam.transform.rotation, 19);
        Selection.activeObject = cam.gameObject;
        Validate();
        Debug.Log("FACTORY_BUILD_COMPLETE: " + ScenePath);
    }

    static void ConfigureMaterials()
    {
        Mat("Paint", "60758A", .48f, .32f); Mat("Blue", "2379AC", .35f,.36f);
        Mat("Teal", "329C9A", .3f,.32f); Mat("Steel", "ADB7C0", .72f,.38f);
        Mat("DarkSteel", "263543", .5f,.3f); Mat("Rubber", "202C33", 0,.22f);
        Mat("Floor", "899397", .08f,.27f); Mat("Wall", "D4DBDD", 0,.25f);
        Mat("Panel", "A9B5BC", .28f,.32f); Mat("White", "E7ECEA", .1f,.3f);
        Mat("Yellow", "EFBC38", .18f,.3f); Mat("Black", "182128", .1f,.27f);
        Mat("Red", "BC4141", .18f,.36f); Mat("Green", "308A62", 0,.3f);
        Mat("Wood", "AD8A5D", 0,.2f); Mat("Cardboard", "B5A07B", 0,.14f);
        Mat("Plastic", "386F91", 0,.3f); Mat("Glass", "7999AC", .35f,.64f);
        Mat("Light", "F3F3E6", 0,.4f, true);
        foreach (string name in new[] {"Floor", "Rubber", "Paint", "Wood"}) Texture(name);
        glyphWidths=File.ReadAllLines(Root+"/Editor/SignGlyphWidths.txt").Select(int.Parse).ToArray();
        string atlasPath=Root+"/Textures/SignGlyphs.png";AssetDatabase.ImportAsset(atlasPath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(atlasPath);importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.SaveAndReimport();
        string textPath=Root+"/Materials/Static_Lettering.mat";textMaterial=AssetDatabase.LoadAssetAtPath<Material>(textPath);
        if(textMaterial==null){textMaterial=new Material(Shader.Find("Factory/Static Sign Lettering"));AssetDatabase.CreateAsset(textMaterial,textPath);}textMaterial.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
    }
    static void Mat(string name, string hex, float metal, float smooth, bool emission=false)
    {
        string path = Root + "/Materials/"+name+".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) {m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
        ColorUtility.TryParseHtmlString("#"+hex,out Color color); m.color=color;
        m.SetFloat("_Metallic",metal); m.SetFloat("_Smoothness",smooth); m.enableInstancing=true;
        if (emission) {m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color*1.2f); m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
        Mats[name]=m;
    }
    static void Texture(string name)
    {
        const int size=128; var t=new Texture2D(size,size,TextureFormat.RGB24,false); var pixels=new Color[size*size];
        var rng=new System.Random(71);
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
            float noise=(float)rng.NextDouble(); float value=.91f+noise*.09f;
            if(name=="Rubber") value=(y%8<2?.66f:.93f)+noise*.04f;
            if(name=="Wood") value=.74f+.20f*Mathf.PerlinNoise(x*.12f,y*.008f);
            pixels[y*size+x]=new Color(value,value,value);
        }
        t.SetPixels(pixels);t.Apply(); string path=Root+"/Textures/"+name+"_MicroSurface.png";
        File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode=TextureWrapMode.Repeat; importer.maxTextureSize=128; importer.SaveAndReimport();
        Mats[name].mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Mats[name].mainTextureScale=name=="Floor"?new Vector2(36,28):new Vector2(3,3);
    }
    static Transform Group(string name, Transform parent=null)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}

    // Shared chamfered meshes have real dimensions: bevel widths stay physical, not stretched cube edges.
    static Mesh Bevel(Vector3 size, float bevel)
    {
        string key=string.Format(System.Globalization.CultureInfo.InvariantCulture,"B_{0:F3}_{1:F3}_{2:F3}_{3:F3}",size.x,size.y,size.z,bevel);
        if(Meshes.TryGetValue(key,out Mesh mesh))return mesh;
        string path=Root+"/Meshes/"+key+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var verts=new List<Vector3>();var norms=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        Vector3 half=size*.5f;float b=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.7f);
        Vector3 inner=half-Vector3.one*b;
        Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};
        for(int face=0;face<3;face++)for(int sign=-1;sign<=1;sign+=2) {
            Vector3 normal=axes[face]*sign;Vector3 u=axes[(face+1)%3];Vector3 v=Vector3.Cross(normal,u);
            float hu=Vector3.Dot(Abs(u),half),hv=Vector3.Dot(Abs(v),half),hn=Vector3.Dot(Abs(normal),half);
            float[] us={-hu,-hu+b,hu-b,hu};float[] vs={-hv,-hv+b,hv-b,hv};
            for(int iy=0;iy<3;iy++)for(int ix=0;ix<3;ix++){
                int start=verts.Count;
                foreach(var q in new[]{new Vector2(us[ix],vs[iy]),new Vector2(us[ix+1],vs[iy]),new Vector2(us[ix+1],vs[iy+1]),new Vector2(us[ix],vs[iy+1])}) {
                    Vector3 point=normal*hn+u*q.x+v*q.y;
                    Vector3 core=new Vector3(Mathf.Clamp(point.x,-inner.x,inner.x),Mathf.Clamp(point.y,-inner.y,inner.y),Mathf.Clamp(point.z,-inner.z,inner.z));
                    Vector3 n=b>.00001f?(point-core).normalized:normal;verts.Add(core+n*b);norms.Add(n);uv.Add(new Vector2(q.x/Mathf.Max(size.x,.1f),q.y/Mathf.Max(size.z,.1f)));
                }
                tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }
        mesh=new Mesh {name=key};mesh.SetVertices(verts);mesh.SetNormals(norms);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh);mesh=PersistMesh(mesh,existing,path);Meshes[key]=mesh;return mesh;
    }
    static Mesh PersistMesh(Mesh source,Mesh existing,string path)
    {
        if(existing==null){AssetDatabase.CreateAsset(source,path);return source;}
        existing.Clear();existing.name=source.name;existing.vertices=source.vertices;existing.normals=source.normals;existing.uv=source.uv;existing.uv2=source.uv2;existing.colors=source.colors;existing.triangles=source.triangles;existing.RecalculateBounds();existing.UploadMeshData(false);EditorUtility.SetDirty(existing);Object.DestroyImmediate(source);return existing;
    }
    static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
    static GameObject Box(string name,Transform p,Vector3 pos,Vector3 size,string mat,float bevel=.012f)
    {
        var g=Group(name,p).gameObject;g.transform.localPosition=pos;g.AddComponent<MeshFilter>().sharedMesh=Bevel(size,bevel);
        var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=Mats[mat];r.lightProbeUsage=LightProbeUsage.BlendProbes;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
        return g;
    }
    static GameObject Cylinder(string name,Transform p,Vector3 pos,float radius,float height,string mat,Vector3 rotation=default)
    {
        string key=string.Format(System.Globalization.CultureInfo.InvariantCulture,"C_{0:F3}_{1:F3}",radius,height);
        if(!Meshes.TryGetValue(key,out Mesh mesh)) {
            string path=Root+"/Meshes/"+key+".asset";mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){
                var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>(); const int n=16;
                float b=Mathf.Min(.008f,radius*.18f);
                float[] ys={-height*.5f,-height*.5f+b,height*.5f-b,height*.5f};float[] rs={radius-b,radius,radius,radius-b};
                for(int ring=0;ring<4;ring++)for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;verts.Add(new Vector3(Mathf.Cos(a)*rs[ring],ys[ring],Mathf.Sin(a)*rs[ring]));uv.Add(new Vector2((float)i/n,(float)ring/3));}
                for(int ring=0;ring<3;ring++)for(int i=0;i<n;i++){int k=ring*(n+1)+i;tris.AddRange(new[]{k,k+n+1,k+1,k+1,k+n+1,k+n+2});}
                for(int cap=0;cap<2;cap++){int start=verts.Count;verts.Add(new Vector3(0,ys[cap*3],0));uv.Add(Vector2.one*.5f);for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;verts.Add(new Vector3(Mathf.Cos(a)*rs[cap*3],ys[cap*3],Mathf.Sin(a)*rs[cap*3]));uv.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f+Vector2.one*.5f);}for(int i=0;i<n;i++) if(cap==0)tris.AddRange(new[]{start,start+i+1,start+i+2});else tris.AddRange(new[]{start,start+i+2,start+i+1});}
                mesh=new Mesh{name=key};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,path);
            }Meshes[key]=mesh;
        }
        var g=Group(name,p).gameObject;g.transform.localPosition=pos;g.transform.localEulerAngles=rotation;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=Mats[mat];
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);return g;
    }
    static void Beam(string name,Transform p,Vector3 a,Vector3 b,float width,string mat)
    {var g=Box(name,p,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,.004f);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static Transform Label(Transform p,string words,Vector3 position,float height,string color="White",Vector3 rotation=default)
    {
        var t=Group("Label_"+words.Replace('\n','_'),p);t.localPosition=position;t.localEulerAngles=rotation;
        string safe=string.Concat(words.Select(c=>char.IsLetterOrDigit(c)?c:'_'))+"_"+Mathf.RoundToInt(height*1000)+"_"+color;
        string path=Root+"/Meshes/Label_"+safe+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);Mesh mesh;
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
            string[] rows=words.Split('\n');float scale=height/54f;
            for(int row=0;row<rows.Length;row++){
                float width=0;foreach(char c in rows[row])width+=glyphWidths[c-32]*scale;
                float x=-width*.5f;float y=(rows.Length-1)*height*.7f-row*height*1.4f;
                foreach(char c in rows[row]){int index=c-32;int k=vertices.Count;float left=(index%16)*64f/1024f;float right=left+64f/1024f;float upper=1-(index/16)*96f/1024f;float lower=upper-96f/1024f;
                    vertices.Add(new Vector3(x-3*scale,y-66*scale,0));vertices.Add(new Vector3(x+61*scale,y-66*scale,0));vertices.Add(new Vector3(x+61*scale,y+30*scale,0));vertices.Add(new Vector3(x-3*scale,y+30*scale,0));
                    uv.Add(new Vector2(left,lower));uv.Add(new Vector2(right,lower));uv.Add(new Vector2(right,upper));uv.Add(new Vector2(left,upper));for(int j=0;j<4;j++)colors.Add(Mats[color].color);
                    triangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});x+=glyphWidths[index]*scale;
                }
            }
            mesh=new Mesh{name="Lettering_"+safe};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh=PersistMesh(mesh,existing,path);
        }
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=textMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        return t;
    }
    static void Sign(Transform p,string text,Vector3 pos,Vector2 size,string color="Blue",float letter=.2f)
    {Box("Sign_Backplate",p,pos,new Vector3(size.x,size.y,.055f),color);var label=Label(p,text,pos+Vector3.back*.035f,letter);float w=label.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;label.localScale=Vector3.one*Mathf.Min(1,size.x*.88f/Mathf.Max(w,.01f));}
    static void SaveTemplate(string name,Transform t)
    {Prefabs[name]=PrefabUtility.SaveAsPrefabAsset(t.gameObject,Root+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(t.gameObject);}
    static Transform Instance(string name,Transform p,Vector3 pos,float yaw=0)
    {var g=(GameObject)PrefabUtility.InstantiatePrefab(Prefabs[name],p);g.transform.localPosition=pos;g.transform.localEulerAngles=new Vector3(0,yaw,0);return g.transform;}
    static void MakeTemplates()
    {
        var bin=Group("Parts_Bin");
        Box("Bottom",bin,new Vector3(0,.035f,0),new Vector3(.58f,.055f,.4f),"Plastic");
        foreach(float x in new[]{-.28f,.28f})Box("Sidewall",bin,new Vector3(x,.16f,0),new Vector3(.035f,.28f,.4f),"Plastic");
        foreach(float z in new[]{-.185f,.185f}){Box("Endwall",bin,new Vector3(0,.145f,z),new Vector3(.55f,.23f,.03f),"Plastic");Box("Stacking_Rim",bin,new Vector3(0,.29f,z),new Vector3(.6f,.035f,.05f),"Blue");}
        Box("Inventory_Label",bin,new Vector3(0,.15f,-.204f),new Vector3(.19f,.07f,.004f),"White",.001f);
        for(int i=0;i<5;i++)Box("Label_Barcode",bin,new Vector3(-.055f+i*.021f,.15f,-.208f),new Vector3(.009f,.05f,.002f),"Black",0);
        SaveTemplate("Parts_Bin",bin);
        var pallet=Group("Euro_Pallet");
        foreach(float x in new[]{-.48f,0,.48f})foreach(float z in new[]{-.35f,.35f})Box("Spacer_Block",pallet,new Vector3(x,.07f,z),new Vector3(.15f,.14f,.15f),"Wood");
        for(int i=0;i<6;i++)Box("Deck_Slat",pallet,new Vector3(-.5f+i*.2f,.16f,0),new Vector3(.16f,.055f,.85f),"Wood",.005f);
        foreach(float x in new[]{-.48f,0,.48f})Box("Bottom_Runner",pallet,new Vector3(x,.025f,0),new Vector3(.15f,.045f,.85f),"Wood");
        SaveTemplate("Euro_Pallet",pallet);
        var carton=Group("Finished_Product_Carton");Box("Carton",carton,new Vector3(0,.23f,0),new Vector3(.56f,.46f,.4f),"Cardboard");
        Box("Packing_Tape",carton,new Vector3(0,.464f,0),new Vector3(.08f,.003f,.4f),"Wood",0);Box("Shipping_Label",carton,new Vector3(.1f,.27f,-.203f),new Vector3(.18f,.12f,.002f),"White",0);
        Label(carton,"QC / PASS",new Vector3(.1f,.27f,-.206f),.04f,"Black");SaveTemplate("Finished_Product_Carton",carton);
        var empty=Group("Open_Empty_Carton");Box("Bottom",empty,new Vector3(0,.016f,0),new Vector3(.6f,.03f,.44f),"Cardboard",.003f);
        foreach(float x in new[]{-.29f,.29f}){Box("Side",empty,new Vector3(x,.22f,0),new Vector3(.02f,.44f,.44f),"Cardboard",.003f);var flap=Box("Open_Flap",empty,new Vector3(x+Mathf.Sign(x)*.1f,.465f,0),new Vector3(.22f,.014f,.44f),"Cardboard",.003f);flap.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sign(x)*15);}
        foreach(float z in new[]{-.21f,.21f})Box("End",empty,new Vector3(0,.22f,z),new Vector3(.58f,.44f,.02f),"Cardboard",.003f);SaveTemplate("Open_Empty_Carton",empty);
        var rack=Group("Supply_Rack");
        foreach(float x in new[]{-.95f,.95f})foreach(float z in new[]{-.35f,.35f}){Box("Perforated_Upright",rack,new Vector3(x,1.25f,z),new Vector3(.075f,2.5f,.075f),"Paint");Box("Footplate",rack,new Vector3(x,.025f,z),new Vector3(.16f,.05f,.16f),"DarkSteel");for(int h=0;h<12;h++)Box("Upright_Slot",rack,new Vector3(x,.3f+h*.17f,z-.041f),new Vector3(.019f,.042f,.004f),"Black",.002f);}
        for(int h=0;h<4;h++){float y=.18f+h*.64f;Box("Steel_Shelf",rack,new Vector3(0,y,0),new Vector3(2,.045f,.8f),"Steel");foreach(float z in new[]{-.39f,.39f})Box("Load_Beam",rack,new Vector3(0,y-.06f,z),new Vector3(2,.12f,.055f),"Blue");for(int i=0;i<3;i++)Instance("Parts_Bin",rack,new Vector3(-.65f+i*.65f,y+.025f,0));}
        Beam("Cross_Brace",rack,new Vector3(-.95f,.2f,.38f),new Vector3(.95f,2.3f,.38f),.025f,"Steel");SaveTemplate("Supply_Rack",rack);
        var cart=Group("Transport_Cart");
        foreach(float x in new[]{-.52f,.52f})foreach(float z in new[]{-.28f,.28f}){Cylinder("Rubber_Caster",cart,new Vector3(x,.12f,z),.105f,.065f,"Rubber",new Vector3(90,0,0));Box("Caster_Fork",cart,new Vector3(x,.21f,z),new Vector3(.06f,.13f,.08f),"Steel");Box("Corner_Post",cart,new Vector3(x,.55f,z),new Vector3(.035f,.68f,.035f),"Steel");}
        foreach(float y in new[]{.29f,.86f}){Box("Tray",cart,new Vector3(0,y,0),new Vector3(1.2f,.04f,.72f),"Paint");foreach(float z in new[]{-.35f,.35f})Box("Tray_Lip",cart,new Vector3(0,y+.055f,z),new Vector3(1.2f,.11f,.025f),"Paint");}
        Beam("Handle",cart,new Vector3(.53f,1.05f,-.29f),new Vector3(.53f,1.05f,.29f),.04f,"DarkSteel");Instance("Parts_Bin",cart,new Vector3(-.25f,.89f,0));SaveTemplate("Transport_Cart",cart);
        var cabinet=Group("Tool_Cabinet");Box("Cabinet_Body",cabinet,new Vector3(0,.48f,0),new Vector3(1.15f,.92f,.55f),"Blue");Box("Rubber_Top",cabinet,new Vector3(0,.97f,0),new Vector3(1.2f,.06f,.6f),"Rubber");
        for(int i=0;i<5;i++){Box("Drawer",cabinet,new Vector3(0,.19f+i*.15f,-.287f),new Vector3(1.05f,.135f,.025f),"Paint");Box("Drawer_Handle",cabinet,new Vector3(0,.22f+i*.15f,-.32f),new Vector3(.62f,.026f,.04f),"Steel");}SaveTemplate("Tool_Cabinet",cabinet);
        var bollard=Group("Safety_Bollard");Cylinder("Anchor_Flange",bollard,new Vector3(0,.035f,0),.17f,.07f,"DarkSteel");Cylinder("Yellow_Post",bollard,new Vector3(0,.49f,0),.085f,.92f,"Yellow");foreach(float y in new[]{.24f,.55f,.8f})Cylinder("Black_Band",bollard,new Vector3(0,y,0),.087f,.12f,"Black");for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Cylinder("Anchor_Bolt",bollard,new Vector3(Mathf.Cos(a)*.12f,.08f,Mathf.Sin(a)*.12f),.018f,.025f,"Steel");}SaveTemplate("Safety_Bollard",bollard);
        var luminaire=Group("Ceiling_Luminaire");Box("Aluminium_Housing",luminaire,Vector3.zero,new Vector3(2.1f,.12f,.32f),"Steel");Box("Diffuser",luminaire,new Vector3(0,-.067f,0),new Vector3(1.96f,.035f,.25f),"Light");foreach(float x in new[]{-.8f,.8f})Cylinder("Suspension",luminaire,new Vector3(x,.3f,0),.009f,.6f,"DarkSteel");SaveTemplate("Ceiling_Luminaire",luminaire);
        MakeWorkbench(); MakeConveyor();
    }
    static void MakeConveyor()
    {
        var t=Group("Conveyor_9m");var frame=Group("Frame_Static",t);var belt=Group("Belt_FutureSpeedTarget",t);var rollers=Group("Rollers",t);
        Box("Upper_Belt",belt,new Vector3(0,.99f,0),new Vector3(8.55f,.035f,.9f),"Rubber",.014f);
        Box("Return_Belt",belt,new Vector3(0,.78f,0),new Vector3(8.55f,.025f,.9f),"Rubber");
        foreach(float z in new[]{-.54f,.54f}){
            Box("Side_Channel",frame,new Vector3(0,.89f,z),new Vector3(9,.22f,.12f),"Paint");Box("Rail_Cap",frame,new Vector3(0,1.015f,z),new Vector3(9,.025f,.15f),"Steel");
            for(int i=0;i<13;i++)Cylinder("Frame_Bolt",frame,new Vector3(-4.25f+i*.71f,.9f,z+Mathf.Sign(z)*.067f),.023f,.015f,"Steel",new Vector3(90,0,0));
        }
        for(int i=0;i<35;i++)Cylinder("Idler_"+i.ToString("D2"),rollers,new Vector3(-4.25f+i*.25f,.917f,0),.061f,1.02f,"Steel",new Vector3(90,0,0));
        foreach(float x in new[]{-4.38f,4.38f})Cylinder("End_Drum",rollers,new Vector3(x,.895f,0),.115f,1.03f,"DarkSteel",new Vector3(90,0,0));
        for(int i=0;i<5;i++){float x=-4+i*2;
            foreach(float z in new[]{-.49f,.49f}){Box("Leg",frame,new Vector3(x,.43f,z),new Vector3(.095f,.78f,.095f),"Steel");Box("Levelling_Plate",frame,new Vector3(x,.055f,z),new Vector3(.23f,.07f,.21f),"DarkSteel");Cylinder("Adjuster",frame,new Vector3(x,.12f,z),.025f,.1f,"Steel");}
            Box("Crossmember",frame,new Vector3(x,.28f,0),new Vector3(.09f,.08f,1),"Paint");
            if(i<4)Beam("Diagonal_Brace",frame,new Vector3(x,.23f,.49f),new Vector3(x+1.5f,.77f,.49f),.045f,"Steel");
        }
        Box("Drive_Gearbox_Static",frame,new Vector3(3.8f,.65f,.76f),new Vector3(.38f,.3f,.34f),"Blue");Cylinder("Motor_Static",frame,new Vector3(3.47f,.65f,.76f),.135f,.35f,"DarkSteel",new Vector3(0,0,90));
        for(int i=0;i<7;i++)Cylinder("Cooling_Fin",frame,new Vector3(3.33f+i*.04f,.65f,.76f),.145f,.013f,"Steel",new Vector3(0,0,90));
        SaveTemplate("Conveyor_9m",t);
    }
    static void MakeWorkbench()
    {
        var t=Group("Adjustable_Workbench");var frame=Group("Lower_Frame_Static",t);var top=Group("Tabletop_FutureHeightTarget",t);top.localPosition=new Vector3(0,1.04f,0);
        Box("Thick_Metal_Worktop",top,Vector3.zero,new Vector3(1.8f,.105f,1.05f),"Steel",.025f);Box("ESD_Workmat",top,new Vector3(0,.058f,0),new Vector3(1.25f,.012f,.68f),"Teal",.005f);
        foreach(float x in new[]{-.66f,.66f}){
            Box("Outer_Telescopic_Column",frame,new Vector3(x,.36f,0),new Vector3(.18f,.65f,.25f),"Paint");Box("Inner_Telescopic_Column",top,new Vector3(x,-.25f,0),new Vector3(.115f,.48f,.17f),"Steel");
            Box("Actuator_Housing",frame,new Vector3(x,.18f,.12f),new Vector3(.25f,.22f,.32f),"DarkSteel");Box("Stabilizer_Foot",frame,new Vector3(x,.085f,0),new Vector3(.27f,.09f,1),"Paint");
            foreach(float z in new[]{-.4f,.4f})Cylinder("Rubber_Foot",frame,new Vector3(x,.033f,z),.07f,.05f,"Rubber");
            Box("Pegboard_Upright",top,new Vector3(x,.56f,.46f),new Vector3(.045f,1.1f,.045f),"Steel");
        }
        Box("Frame_Tie",frame,new Vector3(0,.3f,0),new Vector3(1.35f,.09f,.1f),"Paint");Box("Cable_Tray",frame,new Vector3(0,.73f,.28f),new Vector3(1.2f,.07f,.18f),"DarkSteel");
        for(int i=0;i<13;i++)Box("Cable_Chain_Link",frame,new Vector3(.58f,.3f+i*.035f,.3f),new Vector3(.055f,.026f,.055f),"Black",.005f);
        Box("Perforated_Tool_Panel",top,new Vector3(0,.67f,.46f),new Vector3(1.55f,.68f,.04f),"Paint");
        for(int i=0;i<19;i++)for(int j=0;j<7;j++)Cylinder("Peg_Hole",top,new Vector3(-.69f+i*.077f,.41f+j*.08f,.436f),.008f,.004f,"DarkSteel",new Vector3(90,0,0));
        for(int i=0;i<5;i++){
            float x=-.48f+i*.21f;Box("Tool_Handle",top,new Vector3(x,.56f,.398f),new Vector3(.035f,.18f,.027f),i%2==0?"Blue":"Yellow");
            Box("Tool_Shank",top,new Vector3(x,.72f,.398f),new Vector3(.018f,.15f,.019f),"Steel");
            Cylinder("Tool_Head",top,new Vector3(x,.81f,.398f),.035f,.022f,"Steel",new Vector3(90,0,0));
        }
        Box("Overhead_Light_Bracket",top,new Vector3(0,1.14f,.22f),new Vector3(1.55f,.045f,.52f),"Steel");Box("Task_Light_Diffuser",top,new Vector3(0,1.108f,.13f),new Vector3(1.35f,.02f,.09f),"Light");
        Instance("Parts_Bin",top,new Vector3(-.52f,.059f,.06f));
        Box("Parts_Tray",top,new Vector3(.43f,.09f,-.14f),new Vector3(.48f,.055f,.32f),"DarkSteel");
        for(int i=0;i<3;i++)Cylinder("Machined_Component",top,new Vector3(.29f+i*.14f,.15f,-.14f),.047f,.085f,"Steel");
        var control=Box("Control_Button_Box_VisualOnly",top,new Vector3(.64f,-.16f,-.5f),new Vector3(.36f,.2f,.1f),"DarkSteel");
        Cylinder("Stop_Button",control.transform,new Vector3(-.1f,.025f,-.068f),.035f,.04f,"Red",new Vector3(90,0,0));Cylinder("Up_Button",control.transform,new Vector3(.015f,.025f,-.061f),.02f,.025f,"Green",new Vector3(90,0,0));Cylinder("Down_Button",control.transform,new Vector3(.1f,.025f,-.061f),.02f,.025f,"White",new Vector3(90,0,0));
        SaveTemplate("Adjustable_Workbench",t);
    }
    static void BuildArchitecture()
    {
        var floor=Box("Concrete_Slab_25x18m",architecture,new Vector3(0,-.16f,0),new Vector3(25.5f,.32f,18.5f),"Floor",.01f);floor.AddComponent<BoxCollider>().size=new Vector3(25.5f,.32f,18.5f);
        for(int x=-10;x<=10;x+=5)Box("Expansion_Joint",architecture,new Vector3(x,.002f,0),new Vector3(.014f,.002f,18),"Panel",0);
        for(int z=-6;z<=6;z+=3)Box("Expansion_Joint",architecture,new Vector3(0,.002f,z),new Vector3(25,.002f,.014f),"Panel",0);
        Box("Central_Pedestrian_Aisle",architecture,new Vector3(0,.004f,0),new Vector3(22,.005f,2.6f),"Panel",0);
        foreach(float z in new[]{-1.4f,1.4f})Box("Aisle_Safety_Line",safety,new Vector3(0,.009f,z),new Vector3(22,.009f,.065f),"Yellow",0);
        foreach(float z in new[]{-5f,5f}){Box("Equipment_Boundary",safety,new Vector3(.2f,.009f,z),new Vector3(15.8f,.009f,.06f),"Yellow",0);foreach(float x in new[]{-7.7f,8.1f})Box("Zone_Return",safety,new Vector3(x,.009f,Mathf.Sign(z)*3.2f),new Vector3(.06f,.009f,3.6f),"Yellow",0);}
        for(int x=-9;x<=9;x+=3){Label(safety,">",new Vector3(x,.018f,0),.64f,"White",new Vector3(90,-90,0));}
        // Solid perimeter with inset clerestory glazing and a physical open entry.
        foreach(float x in new[]{-12.5f,12.5f}){
            Box("Sidewall_Lower",architecture,new Vector3(x,1.65f,0),new Vector3(.2f,3.3f,18),"Wall");Box("Sidewall_Header",architecture,new Vector3(x,6.22f,0),new Vector3(.2f,1.56f,18),"Wall");
            for(int z=-8;z<=8;z+=2){Box("Window_Glass",architecture,new Vector3(x,4.35f,z),new Vector3(.055f,2.02f,1.91f),"Glass");Box("Window_Mullion",architecture,new Vector3(x,4.35f,z+1),new Vector3(.14f,2.15f,.065f),"DarkSteel");Box("Window_Transom",architecture,new Vector3(x,4.35f,z),new Vector3(.14f,.055f,2),"DarkSteel");}
        }
        Box("Rear_Wall",architecture,new Vector3(0,3.5f,9),new Vector3(25,7,.2f),"Wall");
        foreach(float x in new[]{-8.125f,8.125f})Box("Entry_Wall",architecture,new Vector3(x,3.5f,-9),new Vector3(8.75f,7,.2f),"Wall");
        Box("Entry_Header",architecture,new Vector3(0,5.85f,-9),new Vector3(7.5f,2.3f,.2f),"Wall");
        foreach(float z in new[]{-8.86f,8.86f})Box("Blue_Wall_Datum",architecture,new Vector3(0,1.35f,z),new Vector3(24.8f,.18f,.035f),"Blue");
        for(int x=-10;x<=10;x+=5){
            foreach(float z in new[]{-8.7f,8.7f}){
                Box("Structural_Column",architecture,new Vector3(x,3.4f,z),new Vector3(.3f,6.8f,.36f),"Panel");Box("Column_Base",architecture,new Vector3(x,.17f,z),new Vector3(.45f,.34f,.49f),"DarkSteel");
                for(int k=0;k<3;k++)Box("Column_Flange",architecture,new Vector3(x,2+k*1.8f,z),new Vector3(.35f,.08f,.4f),"Steel");
            }
            foreach(float y in new[]{6.25f,6.85f})Beam("Roof_Truss_Chord",architecture,new Vector3(x,y,-8.8f),new Vector3(x,y,8.8f),.12f,"DarkSteel");
            for(int z=-8;z<8;z+=2){Beam("Truss_Web",architecture,new Vector3(x,6.25f,z),new Vector3(x,6.85f,z+1),.06f,"Steel");Beam("Truss_Web",architecture,new Vector3(x,6.85f,z+1),new Vector3(x,6.25f,z+2),.06f,"Steel");}
        }
        Box("Roof_Deck",architecture,new Vector3(0,7.08f,0),new Vector3(25.3f,.16f,18.3f),"Wall");
        for(int z=-8;z<=8;z+=2)Box("Roof_Purlin",architecture,new Vector3(0,6.93f,z),new Vector3(25,.12f,.07f),"Panel");
        // Loading shutter at the rear and open shutter at the entrance.
        Shutter(new Vector3(8.2f,0,8.8f),false);Shutter(new Vector3(0,0,-8.85f),true);
        Box("Rear_Feature_Panel",architecture,new Vector3(-1.6f,3.5f,8.42f),new Vector3(10.6f,3.2f,.09f),"DarkSteel");
        Label(architecture,"N O V A  /  M A N U F A C T U R I N G",new Vector3(-1.6f,4.27f,8.36f),.42f);
        Label(architecture,"PRECISION ASSEMBLY     /     SMART FACTORY",new Vector3(-1.6f,3.55f,8.36f),.18f,"Panel");
        Box("Feature_Accent",architecture,new Vector3(-1.6f,3.02f,8.35f),new Vector3(7.4f,.035f,.01f),"Blue",0);
        Label(architecture,"01  /  MATERIALS       02  /  INSPECTION       03  /  ASSEMBLY",new Vector3(-1.6f,2.65f,8.35f),.14f,"White");
        for(int x=-10;x<=10;x+=5){
            Box("Ventilation_Trunk",architecture,new Vector3(x,5.85f,0),new Vector3(.65f,.48f,17.3f),"Steel",.035f);
            for(int z=-7;z<=7;z+=2){Box("Duct_Joint",architecture,new Vector3(x,5.85f,z),new Vector3(.69f,.52f,.045f),"Panel");Box("Vent_Grille",architecture,new Vector3(x,5.595f,z),new Vector3(.5f,.018f,.4f),"DarkSteel");for(int k=0;k<5;k++)Box("Grille_Slat",architecture,new Vector3(x,5.58f,z-.16f+k*.08f),new Vector3(.48f,.015f,.025f),"Steel",.002f);}
        }
        foreach(float z in new[]{-7.2f,7.2f}){
            foreach(float offset in new[]{-.21f,.21f})Box("Cable_Tray_Rail",architecture,new Vector3(0,5.18f,z+offset),new Vector3(24,.13f,.035f),"DarkSteel");
            for(int x=-12;x<=12;x++)Box("Cable_Tray_Rung",architecture,new Vector3(x,5.14f,z),new Vector3(.055f,.03f,.45f),"Steel");
            foreach(float offset in new[]{-.1f,0,.1f})Cylinder("Service_Conduit",architecture,new Vector3(0,5.2f,z+offset),.025f,24,"Black",new Vector3(0,0,90));
        }
        for(int i=0;i<3;i++){Cylinder("Wall_Utility_Pipe",architecture,new Vector3(0,5.1f+i*.22f,8.72f),.045f,24,"Steel",new Vector3(0,0,90));for(int x=-10;x<=10;x+=5)Cylinder("Pipe_Band",architecture,new Vector3(x,5.1f+i*.22f,8.72f),.058f,.05f,"Blue",new Vector3(0,0,90));}
        for(int i=0;i<3;i++){float x=-10.8f+i*.92f;Box("Electrical_Cabinet",architecture,new Vector3(x,1.37f,8.64f),new Vector3(.83f,1.55f,.34f),"Panel");Box("Cabinet_Door",architecture,new Vector3(x,1.37f,8.456f),new Vector3(.76f,1.47f,.024f),"Wall");Box("Door_Handle",architecture,new Vector3(x+.26f,1.3f,8.42f),new Vector3(.035f,.2f,.04f),"DarkSteel");Sign(architecture,"!\n400 V",new Vector3(x,1.66f,8.42f),new Vector2(.24f,.3f),"Yellow",.075f);Cylinder("Cabinet_Conduit",architecture,new Vector3(x,3.95f,8.68f),.025f,3.2f,"Steel");}
        Box("Emergency_Door",architecture,new Vector3(-11.2f,1.13f,-8.84f),new Vector3(1.15f,2.26f,.08f),"Green");Box("Panic_Bar",architecture,new Vector3(-11.2f,1.02f,-8.77f),new Vector3(.9f,.06f,.06f),"Steel");
        var exit=Group("Exit_Sign",safety);exit.localPosition=new Vector3(-11.2f,2.65f,-8.73f);exit.localRotation=Quaternion.Euler(0,180,0);Sign(exit,"EXIT  >",Vector3.zero,new Vector2(1.2f,.36f),"Green",.2f);
    }
    static void Shutter(Vector3 pos,bool open)
    {
        var t=Group(open?"Main_Entrance":"Loading_Bay",architecture);t.localPosition=pos;
        foreach(float x in new[]{-2.3f,2.3f})Box("Door_Guide",t,new Vector3(x,2.2f,0),new Vector3(.18f,4.4f,.2f),"DarkSteel");
        Box("Roll_Housing",t,new Vector3(0,4.5f,0),new Vector3(4.9f,.44f,.46f),"Paint");
        for(int i=0;i<(open?4:27);i++)Box("Shutter_Slat",t,new Vector3(0,4.2f-i*.154f,0),new Vector3(4.45f,.14f,.06f),"Panel",.005f);
        if(!open){Sign(t,"DISPATCH  /  02",new Vector3(0,4.95f,-.04f),new Vector2(3.5f,.45f),"DarkSteel",.24f);foreach(float x in new[]{-2.7f,2.7f})Instance("Safety_Bollard",safety,pos+new Vector3(x,0,-.65f));}
    }
    static void BuildLine(Transform parent,float z,string id,string accent)
    {
        var line=Group("Line_"+id+"_Assembly");
        Instance("Conveyor_9m",line,new Vector3(-1,0,0));
        var input=Group("01_Material_Infeed",line);Box("Infeed_Deck",input,new Vector3(-6.1f,.88f,0),new Vector3(1,.1f,1.15f),"Steel");
        foreach(float x in new[]{-6.47f,-5.73f})foreach(float s in new[]{-.43f,.43f})Box("Infeed_Leg",input,new Vector3(x,.42f,s),new Vector3(.07f,.84f,.07f),"Paint");
        Instance("Parts_Bin",input,new Vector3(-6.1f,.94f,0));
        var inspection=Group("02_Inspection_Gantry_Static",line);
        foreach(float s in new[]{-.76f,.76f}){Box("Inspection_Post",inspection,new Vector3(-.4f,1.33f,s),new Vector3(.1f,2.66f,.1f),"Steel");Box("Anchor",inspection,new Vector3(-.4f,.05f,s),new Vector3(.26f,.08f,.26f),"DarkSteel");}
        Box("Inspection_Header",inspection,new Vector3(-.4f,2.59f,0),new Vector3(.45f,.25f,1.7f),accent);
        Box("Optical_Head_Static",inspection,new Vector3(-.4f,2.35f,0),new Vector3(.22f,.21f,.3f),"DarkSteel");Cylinder("Lens",inspection,new Vector3(-.4f,2.225f,0),.07f,.06f,"Glass");
        Box("Inspection_Light",inspection,new Vector3(-.4f,2.42f,-.5f),new Vector3(.25f,.05f,.28f),"Light");
        Sign(line,"LINE "+id,new Vector3(-4.6f,3.42f,.12f),new Vector2(2,.65f),accent,.4f);
        foreach(float x in new[]{-5.35f,-3.85f})Cylinder("Sign_Suspension",line,new Vector3(x,4.5f,.12f),.012f,1.55f,"Steel");
        Sign(line,"01  /  INFEED",new Vector3(-5.7f,1.53f,.66f),new Vector2(1.1f,.24f),"DarkSteel",.1f);
        Box("Infeed_Sign_Post",line,new Vector3(-5.7f,.7f,.69f),new Vector3(.035f,1.4f,.035f),"Steel");Box("Sign_Foot",line,new Vector3(-5.7f,.02f,.69f),new Vector3(.22f,.04f,.2f),"DarkSteel");
        Sign(line,"02  /  INSPECTION",new Vector3(-.4f,2.64f,-.88f),new Vector2(1.5f,.24f),"DarkSteel",.11f);
        for(int i=0;i<4;i++){
            float x=-3.6f+i*1.62f+(id=="B"?.4f:0);Box("Product_Carrier",line,new Vector3(x,1.03f,0),new Vector3(.56f,.055f,.62f),accent);
            Box("Housing_In_Process",line,new Vector3(x,1.16f,0),new Vector3(.38f,.22f,.35f),"Steel",.025f);Cylinder("Product_Cap",line,new Vector3(x,1.3f,0),.095f,.09f,"DarkSteel");
        }
        string name="Production_Line_"+id;SaveTemplate(name,line);Instance(name,parent,new Vector3(0,0,z));
        Sign(parent,"03  /  ASSEMBLY",new Vector3(5.55f,2.55f,z+.5f),new Vector2(2,.32f),accent,.15f);
        foreach(float x in new[]{4.9f,6.2f})Box("Station_Sign_Support",parent,new Vector3(x,2.27f,z+.53f),new Vector3(.025f,.55f,.025f),"Steel");
        Label(safety,"LINE "+id+"   /   ASSEMBLY CELL",new Vector3(-1,.019f,z-1.42f),.27f,accent,new Vector3(90,0,0));
    }
    static void Populate()
    {
        foreach(float z in new[]{-6.25f,6.4f}){
            for(int i=0;i<3;i++)Instance("Supply_Rack",props,new Vector3(-5.5f+i*2.25f,0,z),z<0?180:0);
        }
        Instance("Supply_Rack",props,new Vector3(2.2f,0,6.4f));
        Instance("Transport_Cart",props,new Vector3(-7.2f,0,3.1f),90);Instance("Transport_Cart",props,new Vector3(4.3f,0,4.6f));
        Instance("Transport_Cart",props,new Vector3(7.3f,0,-4.1f),90);
        Instance("Open_Empty_Carton",props,new Vector3(7.4f,0,5.6f));Instance("Open_Empty_Carton",props,new Vector3(7.4f,0,6.4f));
        foreach(float z in new[]{-3.1f,3.1f})Instance("Tool_Cabinet",props,new Vector3(7.3f,0,z+.65f),-90);
        for(int i=0;i<3;i++){
            float z=-3.8f+i*3.6f;Instance("Euro_Pallet",props,new Vector3(9.5f,0,z));
            for(int level=0;level<2;level++)for(int a=0;a<2;a++)for(int b=0;b<2;b++)Instance("Finished_Product_Carton",props,new Vector3(9.2f+a*.59f,.19f+level*.46f,z-.22f+b*.44f));
        }
        Label(safety,"FINISHED GOODS",new Vector3(9.7f,.02f,5.4f),.25f,"White",new Vector3(90,90,0));
        foreach(float x in new[]{8.4f,10.7f})Box("Dispatch_Floor_Marking",safety,new Vector3(x,.01f,.4f),new Vector3(.065f,.009f,11),"Yellow",0);
        for(int i=0;i<3;i++)Instance("Euro_Pallet",props,new Vector3(-9.5f,i*.21f,5.8f));
        Instance("Euro_Pallet",props,new Vector3(-9.5f,0,-4.2f));
        for(int i=0;i<4;i++)Instance("Parts_Bin",props,new Vector3(-9.75f+(i%2)*.62f,.2f+(i/2)*.3f,-4.2f));
        foreach(float z in new[]{-5.15f,5.15f})foreach(float x in new[]{-7.8f,8.1f})Instance("Safety_Bollard",safety,new Vector3(x,0,z));
        foreach(float x in new[]{-7f,5.7f}){
            var extinguisher=Group("Fire_Extinguisher",safety);extinguisher.localPosition=new Vector3(x,0,8.6f);
            Box("Wall_Bracket",extinguisher,new Vector3(0,.8f,0),new Vector3(.29f,.6f,.08f),"DarkSteel");Cylinder("Red_Pressure_Vessel",extinguisher,new Vector3(0,.7f,-.16f),.11f,.5f,"Red");Cylinder("Neck",extinguisher,new Vector3(0,1,-.16f),.035f,.1f,"Steel");Box("Lever",extinguisher,new Vector3(.04f,1.06f,-.16f),new Vector3(.18f,.035f,.045f),"Black");
            Beam("Hose",extinguisher,new Vector3(.12f,1,-.17f),new Vector3(.16f,.6f,-.17f),.025f,"Black");Box("Instruction_Label",extinguisher,new Vector3(0,.73f,-.269f),new Vector3(.12f,.19f,.009f),"White");Sign(extinguisher,"FIRE\nEXTINGUISHER",new Vector3(0,1.75f,-.07f),new Vector2(.66f,.6f),"Red",.09f);
        }
        foreach(float z in new[]{-6.8f,6.8f}){
            var bin=Group("Recycling_Station",props);bin.localPosition=new Vector3(6.2f,0,z);
            for(int i=0;i<2;i++){Cylinder("Waste_Bin",bin,new Vector3(i*.65f,.4f,0),.25f,.75f,i==0?"Paint":"Blue");Cylinder("Bin_Lid",bin,new Vector3(i*.65f,.79f,0),.27f,.08f,"DarkSteel");Box("Slot",bin,new Vector3(i*.65f,.835f,0),new Vector3(.27f,.006f,.12f),"Black");Label(bin,i==0?"METAL":"PAPER",new Vector3(i*.65f,.49f,-.253f),.07f);}
        }
        for(int i=0;i<3;i++){
            var cone=Group("Safety_Cone",safety);cone.localPosition=new Vector3(10.7f,0,6.6f+i*.65f);Box("Rubber_Base",cone,new Vector3(0,.03f,0),new Vector3(.36f,.06f,.36f),"Black");
            // Tapered cone represented by a dedicated profile mesh, not a stack of cylinders.
            var mesh=new Mesh();var v=new List<Vector3>();var tri=new List<int>();
            for(int r=0;r<4;r++)for(int j=0;j<17;j++){float a=j*Mathf.PI/8;float y=r*.17f;float rad=.13f-r*.035f;v.Add(new Vector3(Mathf.Cos(a)*rad,.06f+y,Mathf.Sin(a)*rad));}
            for(int r=0;r<3;r++)for(int j=0;j<16;j++){int k=r*17+j;tri.AddRange(new[]{k,k+17,k+1,k+1,k+17,k+18});}
            mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();string path=Root+"/Meshes/SafetyCone.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);existing=mesh;}else Object.DestroyImmediate(mesh);
            var g=Group("Tapered_Cone",cone);g.gameObject.AddComponent<MeshFilter>().sharedMesh=existing;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mats["Yellow"];
        }
        var warning=Group("Safety_Notice",safety);warning.localPosition=new Vector3(-8.1f,0,7.9f);Box("Stand_Base",warning,new Vector3(0,.04f,0),new Vector3(.6f,.08f,.5f),"DarkSteel");Box("Stand_Post",warning,new Vector3(0,.8f,0),new Vector3(.055f,1.6f,.055f),"Steel");Sign(warning,"!  SAFETY FIRST\nPPE REQUIRED",new Vector3(0,1.7f,0),new Vector2(1.15f,.65f),"Yellow",.115f);
        // Alternating hazard strips at loading apron, outside the central travel path.
        for(int i=0;i<22;i++){var stripe=Box("Loading_Hazard_Stripe",safety,new Vector3(5.6f+i*.24f,.013f,7.65f),new Vector3(.13f,.01f,.5f),i%2==0?"Black":"Yellow",0);stripe.transform.localRotation=Quaternion.Euler(0,-25,0);}
    }
    static void BuildLighting()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.72f,.8f);RenderSettings.ambientEquatorColor=new Color(.46f,.53f,.6f);RenderSettings.ambientGroundColor=new Color(.27f,.31f,.34f);RenderSettings.ambientIntensity=1;
        RenderSettings.reflectionIntensity=.6f;RenderSettings.fog=false;RenderSettings.skybox=null;
        var sun=Group("Soft_Daylight",lighting).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(58,-35,0);sun.color=new Color(.86f,.93f,1);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;sun.shadowBias=.03f;
        // One real-time shadowed light; ceiling fixtures are emissive with baked area illumination.
        for(int x=-8;x<=8;x+=4)foreach(float z in new[]{-5f,0,5f}){
            Instance("Ceiling_Luminaire",lighting,new Vector3(x,5.7f,z));
            var l=Group("Baked_Ceiling_Area",lighting).gameObject.AddComponent<Light>();l.type=LightType.Rectangle;l.lightmapBakeType=LightmapBakeType.Baked;l.transform.position=new Vector3(x,5.48f,z);l.transform.rotation=Quaternion.Euler(90,0,0);l.areaSize=new Vector2(2.1f,.65f);l.color=new Color(1,.96f,.9f);l.intensity=35;l.range=12;
        }
        var lp=Group("Light_Probes_Interior",lighting).gameObject.AddComponent<LightProbeGroup>();var points=new List<Vector3>();
        for(int x=-10;x<=10;x+=4)for(int z=-7;z<=7;z+=3)foreach(float y in new[]{.5f,1.6f,3.8f})points.Add(new Vector3(x,y,z));lp.probePositions=points.ToArray();
        var probe=Group("Reflection_Probe_Interior",lighting).gameObject.AddComponent<ReflectionProbe>();probe.transform.position=new Vector3(0,2.7f,0);probe.mode=ReflectionProbeMode.Baked;probe.size=new Vector3(25,7,18);probe.center=new Vector3(0,.8f,0);probe.boxProjection=true;probe.resolution=128;probe.intensity=.55f;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.4f,.46f,.5f);probe.nearClipPlane=.15f;probe.farClipPlane=40;
        var settings=new LightingSettings();settings.name="Factory_Lighting";settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;settings.lightmapResolution=8;settings.lightmapMaxSize=1024;settings.lightmapPadding=3;settings.directSampleCount=16;settings.indirectSampleCount=32;settings.environmentSampleCount=32;settings.maxBounces=2;settings.ao=true;settings.aoMaxDistance=1.3f;settings.aoExponentIndirect=1.2f;
        string path=Root+"/Factory_Lighting.lighting";var old=AssetDatabase.LoadAssetAtPath<LightingSettings>(path);if(old==null)AssetDatabase.CreateAsset(settings,path);else{EditorUtility.CopySerialized(settings,old);Object.DestroyImmediate(settings);settings=old;}Lightmapping.lightingSettings=settings;
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){
            if(renderer.GetComponent<TextMesh>()!=null)continue;
            var so=new SerializedObject(renderer);var prop=so.FindProperty("m_ScaleInLightmap");if(prop!=null){float size=renderer.bounds.size.magnitude;prop.floatValue=size<.15f?0:size<.6f?.3f:1;so.ApplyModifiedPropertiesWithoutUndo();}
        }
        // Disable expensive renderer effects inherited from the previous demo.
        foreach(var assetPath in new[]{"Assets/Settings/PC_RPAsset.asset","Assets/Settings/Mobile_RPAsset.asset"}){
            var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);if(rp==null)continue;rp.supportsHDR=false;rp.msaaSampleCount=4;rp.shadowDistance=32;var serial=new SerializedObject(rp);serial.FindProperty("m_AdditionalLightsRenderingMode").intValue=0;serial.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(rp);
        }
    }
    [MenuItem("Tools/Smart Factory/Bake Lighting and Capture")]
    public static void BakeAndCapture()
    {
        if(EditorSceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
        bool baked=Lightmapping.Bake();Debug.Log("FACTORY_LIGHT_BAKE: "+baked);
        foreach(var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))Lightmapping.BakeReflectionProbe(probe,Root+"/Textures/InteriorReflection.exr");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();Capture();Validate();
    }
    [MenuItem("Tools/Smart Factory/Capture Presentation")]
    public static void Capture()
    {
        var cam=Camera.main;CaptureView(cam,"Documentation/Factory_Presentation.png",1920,1080);
        var pos=cam.transform.position;var rot=cam.transform.rotation;float fov=cam.fieldOfView;
        cam.transform.position=new Vector3(9.4f,3.4f,-6.5f);cam.transform.LookAt(new Vector3(4.5f,1.1f,-2.8f));cam.fieldOfView=57;CaptureView(cam,"Documentation/Factory_Workstation.png",1600,1000);
        cam.transform.position=new Vector3(-10,2.6f,.2f);cam.transform.LookAt(new Vector3(1,1.2f,0));cam.fieldOfView=66;CaptureView(cam,"Documentation/Factory_Aisle.png",1600,1000);
        cam.transform.SetPositionAndRotation(pos,rot);cam.fieldOfView=fov;
    }
    static void CaptureView(Camera cam,string path,int w,int h)
    {
        Directory.CreateDirectory("Documentation");var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;cam.targetTexture=rt;cam.Render();var prev=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(w,h,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(t);
    }
    [MenuItem("Tools/Smart Factory/Validate Factory")]
    public static void Validate()
    {
        var objects=Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);var errors=new List<string>();
        foreach(var g in objects){if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g)>0)errors.Add("Missing script: "+g.name);foreach(var r in g.GetComponents<Renderer>())foreach(var m in r.sharedMaterials)if(m==null||m.shader==null||!m.shader.isSupported)errors.Add("Broken material: "+g.name);}
        var behaviours=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>b!=null&&b.GetType().Assembly.GetName().Name=="Assembly-CSharp").ToArray();if(behaviours.Length>0)errors.Add("Unexpected runtime scripts");
        if(Camera.main==null)errors.Add("Missing presentation camera");
        foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)){
            if(mf.sharedMesh==null){errors.Add("Missing mesh: "+mf.name);continue;}
            if(mf.sharedMesh.bounds.size.sqrMagnitude<.000001f)errors.Add("Empty mesh: "+mf.name);
        }
        foreach(string required in new[]{"Factory_Architecture","Production_Line_A","Production_Line_B","Workstations","Safety_Equipment","Factory_Props","Lighting","Cameras"})if(!EditorSceneManager.GetActiveScene().GetRootGameObjects().Any(g=>g.name==required))errors.Add("Missing root: "+required);
        string report="Scene: "+ScenePath+"\nObjects: "+objects.Length+"\nMesh renderers: "+Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length+"\nRuntime user scripts: "+behaviours.Length+"\nLightmaps: "+LightmapSettings.lightmaps.Length+"\nErrors: "+errors.Count+"\n"+string.Join("\n",errors);
        File.WriteAllText("Documentation/Factory_Validation.txt",report);Debug.Log(report);if(errors.Count>0)throw new Exception("Factory validation failed");
    }
    public static void BuildBatch(){Build();Capture();BakeAndCapture();}
    public static void ReviewBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);Capture();Validate();
        Debug.Log("FACTORY_REVIEW_COMPLETE");
    }
    public static void OpenForReview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if(SceneView.lastActiveSceneView!=null){var view=SceneView.lastActiveSceneView;view.LookAt(Camera.main.transform.position+Camera.main.transform.forward*14,Camera.main.transform.rotation,14);view.sceneLighting=true;}
        Selection.activeGameObject=Camera.main.gameObject;
        var sceneEntry=new EditorBuildSettingsScene(ScenePath,true);sceneEntry.guid=new GUID(AssetDatabase.AssetPathToGUID(ScenePath));EditorBuildSettings.scenes=new[]{sceneEntry};
        EditorApplication.delayCall+=()=>{Capture();Validate();Debug.Log("FACTORY_REVIEW_COMPLETE");};
    }
}
