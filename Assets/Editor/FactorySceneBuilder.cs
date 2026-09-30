using ErgoContest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FactorySceneBuilder
{
    private const string SceneFolder = "Assets/Scenes";
    private const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/Ergo Contest/Create VR Demo Scene")]
    public static void CreateVrDemoScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(SceneFolder);
        EnsureFolder(MaterialFolder);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material floorMat = Mat("EC Floor Graphite", new Color(0.18f, 0.20f, 0.21f));
        Material wallMat = Mat("EC Wall Soft Grey", new Color(0.63f, 0.68f, 0.70f));
        Material steelMat = Mat("EC Brushed Steel", new Color(0.25f, 0.31f, 0.35f));
        Material trayMat = Mat("EC Supply Blue", new Color(0.12f, 0.38f, 0.62f));
        Material benchMat = Mat("EC Bench Teal", new Color(0.10f, 0.46f, 0.42f));
        Material conveyorMat = Mat("EC Conveyor Belt", new Color(0.05f, 0.06f, 0.07f));
        Material warningMat = Mat("EC Safety Amber", new Color(1.0f, 0.68f, 0.08f));
        Material partMat = Mat("EC Part Light Alloy", new Color(0.78f, 0.82f, 0.84f));
        Material bodyMat = Mat("EC Worker Neutral", new Color(0.20f, 0.25f, 0.29f));
        Material greenMat = Mat("EC Load Green", new Color(0.20f, 0.88f, 0.36f));
        Material panelMat = Mat("EC Panel Dark", new Color(0.035f, 0.045f, 0.055f));
        Material ghostMat = TransparentMat("EC Target Ghost Cyan", new Color(0.18f, 0.82f, 1f, 0.28f));
        Material zoneMat = TransparentMat("EC Safety Zone Amber", new Color(1f, 0.62f, 0.05f, 0.20f));
        Material trailMat = Mat("EC Hand Trail", new Color(0.20f, 0.85f, 1f));

        GameObject root = Group("Ergonomic Load Control Cell");
        Transform rootT = root.transform;

        BuildShell(rootT, floorMat, wallMat, warningMat);

        GameObject worker = BuildWorker(rootT, bodyMat, greenMat, out Transform head, out Transform neck, out Transform pelvis, out Transform rightHand, out Transform leftHand,
            out Transform torso, out Transform leftShoulder, out Transform rightShoulder, out Transform leftUpperArm,
            out Transform leftForearm, out Transform rightUpperArm, out Transform rightForearm,
            out Transform leftThigh, out Transform leftShin, out Transform leftFoot, out Transform rightThigh, out Transform rightShin, out Transform rightFoot);

        GameObject handTarget = Sphere("Input Right Hand Target", rootT, new Vector3(-0.95f, 1.58f, 0.32f), new Vector3(0.055f, 0.055f, 0.055f), greenMat);
        GameObject headTarget = Group("Input Head Target", rootT);
        headTarget.transform.position = new Vector3(-0.05f, 1.62f, -0.25f);

        BuildConveyor(rootT, conveyorMat, steelMat, partMat, out GameObject conveyorRoot, out Transform conveyorStart, out Transform conveyorEnd, out Transform[] conveyorParts);
        BuildSupplyTray(rootT, trayMat, steelMat, partMat, warningMat, out GameObject trayRoot, out Transform pickupPoint, out Transform[] trayParts);
        BuildWorkbench(rootT, benchMat, steelMat, partMat, warningMat, out GameObject benchRoot, out Transform assemblyPoint, out GameObject placedPart);
        GameObject carriedPart = Box("Carried Part Visual", rootT, new Vector3(-0.85f, 1.44f, 0.34f), new Vector3(0.17f, 0.13f, 0.19f), partMat);
        carriedPart.SetActive(false);
        BuildBodyLabels(rootT);
        BuildZones(rootT, zoneMat, out Transform supplyZone, out Transform benchZone);
        BuildGhosts(rootT, ghostMat, out Transform supplyGhost, out Transform benchGhost);

        GameObject systems = Group("Systems", rootT);
        var pcInput = systems.AddComponent<PcErgoInputProvider>();
        SetObject(pcInput, "headTarget", headTarget.transform);
        SetObject(pcInput, "handTarget", handTarget.transform);
        SetObject(pcInput, "workerRoot", worker.transform);
        SetObject(pcInput, "supplyPickup", pickupPoint);
        SetObject(pcInput, "assemblyPoint", assemblyPoint);
        SetObject(pcInput, "restPoint", Group("Safe Rest Point", rootT).transform);
        GameObject.Find("Safe Rest Point").transform.position = new Vector3(0.48f, 1.08f, -0.35f);

        var questInput = systems.AddComponent<QuestErgoInputProvider>();
        SetObject(questInput, "headTarget", headTarget.transform);
        SetObject(questInput, "handTarget", handTarget.transform);
        SetObject(questInput, "fallbackWorkerRoot", worker.transform);
        SetObject(questInput, "editorFallback", pcInput);

        var observer = systems.AddComponent<WorkTaskObserver>();
        SetObject(observer, "inputProvider", questInput);
        SetObject(observer, "workerRoot", worker.transform);
        SetObject(observer, "supplyPickupPoint", pickupPoint);
        SetObject(observer, "assemblyPoint", assemblyPoint);
        SetObject(observer, "supplyMoveZone", supplyZone);
        SetObject(observer, "benchMoveZone", benchZone);

        var load = systems.AddComponent<ErgoLoadEstimator>();
        SetObject(load, "inputProvider", questInput);
        SetObject(load, "observer", observer);
        SetObject(load, "workerRoot", worker.transform);

        var trayEquipment = trayRoot.AddComponent<AdjustableEquipment>();
        SetEnum(trayEquipment, "equipmentType", EquipmentType.SupplyTray);
        SetObject(trayEquipment, "movingRoot", trayRoot.transform);
        SetVector(trayEquipment, "minPosition", new Vector3(-0.98f, 0.95f, 0.16f));
        SetVector(trayEquipment, "maxPosition", new Vector3(-0.48f, 1.62f, 0.52f));

        var benchEquipment = benchRoot.AddComponent<AdjustableEquipment>();
        SetEnum(benchEquipment, "equipmentType", EquipmentType.Workbench);
        SetObject(benchEquipment, "movingRoot", benchRoot.transform);
        SetVector(benchEquipment, "minPosition", new Vector3(0.38f, -0.15f, 0.45f));
        SetVector(benchEquipment, "maxPosition", new Vector3(0.38f, 0.28f, 0.45f));

        var conveyorEquipment = conveyorRoot.AddComponent<AdjustableEquipment>();
        SetEnum(conveyorEquipment, "equipmentType", EquipmentType.Conveyor);
        SetObject(conveyorEquipment, "movingRoot", conveyorRoot.transform);
        SetVector(conveyorEquipment, "minPosition", conveyorRoot.transform.position);
        SetVector(conveyorEquipment, "maxPosition", conveyorRoot.transform.position);

        var agent = systems.AddComponent<ErgoAgentController>();
        SetObject(agent, "inputProvider", questInput);
        SetObject(agent, "observer", observer);
        SetObject(agent, "loadEstimator", load);
        SetObject(agent, "supplyTray", trayEquipment);
        SetObject(agent, "workbench", benchEquipment);
        SetObject(agent, "conveyor", conveyorEquipment);

        var flow = conveyorRoot.AddComponent<ConveyorPartFlow>();
        SetObject(flow, "conveyorEquipment", conveyorEquipment);
        SetObject(flow, "observer", observer);
        SetTransformArray(flow, "parts", conveyorParts);
        SetObject(flow, "startPoint", conveyorStart);
        SetObject(flow, "endPoint", conveyorEnd);

        var assemblyViz = systems.AddComponent<AssemblyPartVisualizer>();
        SetObject(assemblyViz, "inputProvider", questInput);
        SetObject(assemblyViz, "handTarget", handTarget.transform);
        SetObject(assemblyViz, "assemblyPoint", assemblyPoint);
        SetObject(assemblyViz, "carriedPart", carriedPart.transform);
        SetObject(assemblyViz, "placedPart", placedPart.transform);
        SetTransformArray(assemblyViz, "trayParts", trayParts);

        var body = systems.AddComponent<WorkerBodyVisualizer>();
        SetObject(body, "inputProvider", questInput);
        SetObject(body, "loadEstimator", load);
        SetObject(body, "workerRoot", worker.transform);
        SetObject(body, "head", head);
        SetObject(body, "neck", neck);
        SetObject(body, "pelvis", pelvis);
        SetObject(body, "torso", torso);
        SetObject(body, "leftShoulder", leftShoulder);
        SetObject(body, "rightShoulder", rightShoulder);
        SetObject(body, "leftUpperArm", leftUpperArm);
        SetObject(body, "leftForearm", leftForearm);
        SetObject(body, "leftHand", leftHand);
        SetObject(body, "rightUpperArm", rightUpperArm);
        SetObject(body, "rightForearm", rightForearm);
        SetObject(body, "rightHand", rightHand);
        SetObject(body, "leftThigh", leftThigh);
        SetObject(body, "leftShin", leftShin);
        SetObject(body, "leftFoot", leftFoot);
        SetObject(body, "rightThigh", rightThigh);
        SetObject(body, "rightShin", rightShin);
        SetObject(body, "rightFoot", rightFoot);
        SetRendererArray(body, "loadRenderers", Renderers(leftShoulder, rightShoulder, leftUpperArm, leftForearm, rightUpperArm, rightForearm, rightHand, leftHand));
        SetRendererArray(body, "neutralRenderers", Renderers(head, neck, pelvis, torso, leftThigh, leftShin, leftFoot, rightThigh, rightShin, rightFoot));

        var trailGo = Group("Hand Movement Trail", rootT);
        var trail = trailGo.AddComponent<LineRenderer>();
        trail.material = trailMat;
        trail.startWidth = 0.025f;
        trail.endWidth = 0.008f;
        trail.positionCount = 0;
        var trailViz = trailGo.AddComponent<HandTrailVisualizer>();
        SetObject(trailViz, "inputProvider", questInput);
        SetObject(trailViz, "lineRenderer", trail);

        BuildPoseGhost(rootT, ghostMat, trailMat, questInput, agent, worker.transform);

        var supplyGhostCmp = supplyGhost.gameObject.AddComponent<EquipmentGhost>();
        SetObject(supplyGhostCmp, "agent", agent);
        SetEnum(supplyGhostCmp, "equipmentType", EquipmentType.SupplyTray);
        SetObject(supplyGhostCmp, "ghostRoot", supplyGhost);
        SetRendererArray(supplyGhostCmp, "renderers", supplyGhost.GetComponentsInChildren<Renderer>());

        var benchGhostCmp = benchGhost.gameObject.AddComponent<EquipmentGhost>();
        SetObject(benchGhostCmp, "agent", agent);
        SetEnum(benchGhostCmp, "equipmentType", EquipmentType.Workbench);
        SetObject(benchGhostCmp, "ghostRoot", benchGhost);
        SetRendererArray(benchGhostCmp, "renderers", benchGhost.GetComponentsInChildren<Renderer>());

        BuildStatusPanel(rootT, panelMat, Camera.main != null ? Camera.main.transform : null, questInput, observer, load, agent, trayEquipment, benchEquipment, conveyorEquipment);
        BuildLightingAndCamera(rootT, headTarget.transform);

        string scenePath = AssetDatabase.GenerateUniqueAssetPath(SceneFolder + "/ErgoContestVrDemo.unity");
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = systems;
        Debug.Log("Created Ergo Contest VR demo scene: " + scenePath);
    }

    private static void BuildShell(Transform parent, Material floorMat, Material wallMat, Material warningMat)
    {
        Box("Floor", parent, new Vector3(0, -0.08f, 0.45f), new Vector3(6.2f, 0.16f, 4.6f), floorMat);
        Box("Back Wall", parent, new Vector3(0, 1.8f, 2.75f), new Vector3(6.2f, 3.6f, 0.12f), wallMat);
        Box("Left Wall", parent, new Vector3(-3.1f, 1.8f, 0.45f), new Vector3(0.12f, 3.6f, 4.6f), wallMat);
        Box("Operator Foot Zone", parent, new Vector3(-0.05f, 0.01f, -0.34f), new Vector3(1.25f, 0.012f, 0.82f), warningMat);
        for (int i = 0; i < 5; i++)
            Box("Floor Distance Mark " + i, parent, new Vector3(-1.5f + i * 0.5f, 0.015f, 0.72f), new Vector3(0.035f, 0.014f, 1.1f), warningMat);
    }

    private static GameObject BuildWorker(Transform parent, Material bodyMat, Material loadMat, out Transform head, out Transform neck, out Transform pelvis, out Transform rightHand, out Transform leftHand,
        out Transform torso, out Transform leftShoulder, out Transform rightShoulder, out Transform leftUpperArm, out Transform leftForearm, out Transform rightUpperArm, out Transform rightForearm,
        out Transform leftThigh, out Transform leftShin, out Transform leftFoot, out Transform rightThigh, out Transform rightShin, out Transform rightFoot)
    {
        GameObject worker = Group("Tracked Worker Diagram", parent);
        worker.transform.position = new Vector3(-0.05f, 0, -0.38f);
        Box("Worker Stance Plate", worker.transform, new Vector3(0, 0.025f, 0), new Vector3(0.64f, 0.05f, 0.46f), bodyMat);
        pelvis = Capsule("Pelvis", worker.transform, new Vector3(0, 0.78f, 0), new Vector3(0.34f, 0.22f, 0.25f), bodyMat).transform;
        torso = Capsule("Chest", worker.transform, new Vector3(0, 1.08f, 0), new Vector3(0.38f, 0.48f, 0.30f), bodyMat).transform;
        neck = Capsule("Neck", worker.transform, new Vector3(0, 1.40f, 0.02f), new Vector3(0.11f, 0.16f, 0.11f), bodyMat).transform;
        head = Sphere("Head Tracker", worker.transform, new Vector3(0, 1.60f, 0.04f), new Vector3(0.24f, 0.28f, 0.22f), bodyMat).transform;
        leftShoulder = Sphere("Left Shoulder Load", worker.transform, new Vector3(-0.26f, 1.32f, 0), new Vector3(0.16f, 0.16f, 0.16f), loadMat).transform;
        rightShoulder = Sphere("Right Shoulder Load", worker.transform, new Vector3(0.26f, 1.32f, 0), new Vector3(0.16f, 0.16f, 0.16f), loadMat).transform;
        leftUpperArm = Capsule("Left Upper Arm Load", worker.transform, new Vector3(-0.40f, 1.10f, 0), new Vector3(0.105f, 0.34f, 0.105f), loadMat).transform;
        leftForearm = Capsule("Left Forearm Load", worker.transform, new Vector3(-0.42f, 0.82f, 0), new Vector3(0.092f, 0.30f, 0.092f), loadMat).transform;
        rightUpperArm = Capsule("Right Upper Arm Load", worker.transform, new Vector3(0.40f, 1.10f, 0), new Vector3(0.105f, 0.34f, 0.105f), loadMat).transform;
        rightForearm = Capsule("Right Forearm Load", worker.transform, new Vector3(0.42f, 0.82f, 0), new Vector3(0.092f, 0.30f, 0.092f), loadMat).transform;
        leftHand = Box("Left Hand Load", worker.transform, new Vector3(-0.42f, 0.62f, 0), new Vector3(0.13f, 0.07f, 0.10f), loadMat).transform;
        rightHand = Box("Right Hand Load", worker.transform, new Vector3(0.42f, 0.62f, 0), new Vector3(0.13f, 0.07f, 0.10f), loadMat).transform;
        leftThigh = Capsule("Left Thigh", worker.transform, new Vector3(-0.13f, 0.53f, 0), new Vector3(0.13f, 0.34f, 0.13f), bodyMat).transform;
        leftShin = Capsule("Left Shin", worker.transform, new Vector3(-0.13f, 0.22f, 0.04f), new Vector3(0.115f, 0.30f, 0.115f), bodyMat).transform;
        leftFoot = Box("Left Foot", worker.transform, new Vector3(-0.13f, 0.055f, 0.12f), new Vector3(0.16f, 0.07f, 0.30f), bodyMat).transform;
        rightThigh = Capsule("Right Thigh", worker.transform, new Vector3(0.13f, 0.53f, 0), new Vector3(0.13f, 0.34f, 0.13f), bodyMat).transform;
        rightShin = Capsule("Right Shin", worker.transform, new Vector3(0.13f, 0.22f, 0.04f), new Vector3(0.115f, 0.30f, 0.115f), bodyMat).transform;
        rightFoot = Box("Right Foot", worker.transform, new Vector3(0.13f, 0.055f, 0.12f), new Vector3(0.16f, 0.07f, 0.30f), bodyMat).transform;
        return worker;
    }

    private static void BuildBodyLabels(Transform parent)
    {
        TextMesh shoulder = TextLine("Load Label Shoulder", parent, new Vector3(-0.58f, 1.52f, -0.30f), 0.025f, Color.white);
        shoulder.text = "SHOULDER";
        TextMesh arm = TextLine("Load Label Arm", parent, new Vector3(-0.58f, 1.27f, -0.30f), 0.025f, Color.white);
        arm.text = "ARM";
        TextMesh hand = TextLine("Load Label Hand", parent, new Vector3(-0.58f, 1.02f, -0.30f), 0.025f, Color.white);
        hand.text = "HAND";
    }

    private static void BuildConveyor(Transform parent, Material beltMat, Material steelMat, Material partMat, out GameObject conveyorRoot, out Transform start, out Transform end, out Transform[] parts)
    {
        conveyorRoot = Group("Adjustable Conveyor", parent);
        conveyorRoot.transform.position = new Vector3(-1.62f, 0, 0.72f);
        Box("Conveyor Frame", conveyorRoot.transform, Vector3.up * 0.78f, new Vector3(1.75f, 0.16f, 0.50f), steelMat);
        Box("Moving Belt", conveyorRoot.transform, new Vector3(0, 0.89f, 0), new Vector3(1.65f, 0.045f, 0.44f), beltMat);
        for (int i = 0; i < 4; i++)
            Box("Conveyor Leg " + i, conveyorRoot.transform, new Vector3(-0.72f + (i % 2) * 1.44f, 0.39f, -0.18f + (i / 2) * 0.36f), new Vector3(0.06f, 0.78f, 0.06f), steelMat);
        start = Group("Conveyor Start", conveyorRoot.transform).transform;
        start.localPosition = new Vector3(-0.72f, 1.02f, 0);
        end = Group("Conveyor End", conveyorRoot.transform).transform;
        end.localPosition = new Vector3(0.72f, 1.02f, 0);
        parts = new Transform[4];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = Box("Incoming Part " + (i + 1), conveyorRoot.transform, start.localPosition, new Vector3(0.16f, 0.13f, 0.18f), partMat).transform;
    }

    private static void BuildSupplyTray(Transform parent, Material trayMat, Material steelMat, Material partMat, Material warningMat, out GameObject trayRoot, out Transform pickupPoint, out Transform[] trayParts)
    {
        trayRoot = Group("Agent Controlled Supply Tray", parent);
        trayRoot.transform.position = new Vector3(-0.82f, 1.48f, 0.38f);
        Box("Tray Shelf", trayRoot.transform, Vector3.zero, new Vector3(0.86f, 0.08f, 0.66f), trayMat);
        Box("Tray Back", trayRoot.transform, new Vector3(0, 0.22f, 0.31f), new Vector3(0.86f, 0.42f, 0.05f), steelMat);
        Box("Tray Height Rail L", trayRoot.transform, new Vector3(-0.53f, -0.58f, 0), new Vector3(0.06f, 1.2f, 0.06f), steelMat);
        Box("Tray Height Rail R", trayRoot.transform, new Vector3(0.53f, -0.58f, 0), new Vector3(0.06f, 1.2f, 0.06f), steelMat);
        Box("Supply Travel Label Bar", trayRoot.transform, new Vector3(0, -0.64f, -0.40f), new Vector3(1.08f, 0.045f, 0.06f), warningMat);
        pickupPoint = Group("Supply Pickup Point", trayRoot.transform).transform;
        pickupPoint.localPosition = new Vector3(0.05f, 0.18f, -0.05f);
        trayParts = new Transform[3];
        for (int i = 0; i < trayParts.Length; i++)
            trayParts[i] = Box("Tray Part " + (i + 1), trayRoot.transform, new Vector3(-0.22f + i * 0.22f, 0.14f, -0.04f), new Vector3(0.15f, 0.13f, 0.18f), partMat).transform;
    }

    private static void BuildWorkbench(Transform parent, Material benchMat, Material steelMat, Material partMat, Material warningMat, out GameObject benchRoot, out Transform assemblyPoint, out GameObject placedPart)
    {
        benchRoot = Group("Agent Controlled Workbench", parent);
        benchRoot.transform.position = new Vector3(0.38f, 0, 0.45f);
        Box("Bench Surface", benchRoot.transform, new Vector3(0, 0.92f, 0), new Vector3(1.38f, 0.10f, 0.88f), benchMat);
        for (int i = 0; i < 4; i++)
            Box("Bench Lift Leg " + i, benchRoot.transform, new Vector3(-0.55f + (i % 2) * 1.10f, 0.44f, -0.34f + (i / 2) * 0.68f), new Vector3(0.07f, 0.88f, 0.07f), steelMat);
        Box("Assembly Fixture Base", benchRoot.transform, new Vector3(0.16f, 1.01f, -0.03f), new Vector3(0.48f, 0.055f, 0.34f), steelMat);
        Box("Fixture Clamp L", benchRoot.transform, new Vector3(-0.07f, 1.085f, -0.03f), new Vector3(0.055f, 0.12f, 0.32f), warningMat);
        Box("Fixture Clamp R", benchRoot.transform, new Vector3(0.39f, 1.085f, -0.03f), new Vector3(0.055f, 0.12f, 0.32f), warningMat);
        assemblyPoint = Group("Assembly Point", benchRoot.transform).transform;
        assemblyPoint.localPosition = new Vector3(0.16f, 1.14f, -0.03f);
        placedPart = Box("Placed Assembly Part", benchRoot.transform, new Vector3(0.16f, 1.14f, -0.03f), new Vector3(0.18f, 0.14f, 0.20f), partMat);
    }

    private static void BuildZones(Transform parent, Material zoneMat, out Transform supplyZone, out Transform benchZone)
    {
        supplyZone = Box("Supply Tray Movement Safety Zone", parent, new Vector3(-0.74f, 1.25f, 0.34f), new Vector3(1.0f, 1.4f, 1.0f), zoneMat).transform;
        benchZone = Box("Workbench Lift Safety Zone", parent, new Vector3(0.38f, 0.95f, 0.45f), new Vector3(1.2f, 1.0f, 1.1f), zoneMat).transform;
    }

    private static void BuildGhosts(Transform parent, Material ghostMat, out Transform supplyGhost, out Transform benchGhost)
    {
        supplyGhost = Group("Supply Target Ghost", parent).transform;
        Box("Supply Ghost Shelf", supplyGhost, Vector3.zero, new Vector3(0.86f, 0.055f, 0.66f), ghostMat);
        supplyGhost.gameObject.SetActive(false);
        benchGhost = Group("Workbench Target Ghost", parent).transform;
        Box("Bench Ghost Surface", benchGhost, new Vector3(0, 0.92f, 0), new Vector3(1.38f, 0.075f, 0.88f), ghostMat);
        benchGhost.gameObject.SetActive(false);
    }

    private static void BuildPoseGhost(Transform parent, Material ghostMat, Material lineMat, ErgoInputProvider input, ErgoAgentController agent, Transform workerRoot)
    {
        Transform before = Sphere("Before Adjustment Hand Ghost", parent, new Vector3(0, -10f, 0), new Vector3(0.12f, 0.12f, 0.12f), ghostMat).transform;
        before.gameObject.SetActive(false);
        Transform after = Sphere("After Adjustment Hand Ghost", parent, new Vector3(0, -10f, 0), new Vector3(0.12f, 0.12f, 0.12f), ghostMat).transform;
        after.gameObject.SetActive(false);
        LineRenderer beforeLine = Group("Before Reach Line", parent).AddComponent<LineRenderer>();
        beforeLine.material = lineMat;
        beforeLine.startWidth = 0.018f;
        beforeLine.endWidth = 0.018f;
        beforeLine.positionCount = 0;
        LineRenderer afterLine = Group("After Reach Line", parent).AddComponent<LineRenderer>();
        afterLine.material = lineMat;
        afterLine.startWidth = 0.018f;
        afterLine.endWidth = 0.018f;
        afterLine.positionCount = 0;
        var ghost = Group("Before After Pose Ghost System", parent).AddComponent<BeforeAfterPoseGhost>();
        SetObject(ghost, "inputProvider", input);
        SetObject(ghost, "agent", agent);
        SetObject(ghost, "workerRoot", workerRoot);
        SetObject(ghost, "beforeHandGhost", before);
        SetObject(ghost, "afterHandGhost", after);
        SetObject(ghost, "beforeReachLine", beforeLine);
        SetObject(ghost, "afterReachLine", afterLine);
    }

    private static void BuildStatusPanel(Transform parent, Material panelMat, Transform faceCamera, ErgoInputProvider input, WorkTaskObserver observer, ErgoLoadEstimator load, ErgoAgentController agent, AdjustableEquipment tray, AdjustableEquipment bench, AdjustableEquipment conveyor)
    {
        GameObject panel = Group("World Status Panel", parent);
        panel.transform.position = new Vector3(1.65f, 1.52f, -0.10f);
        panel.transform.rotation = Quaternion.Euler(0, -38f, 0);
        Box("Panel Back Plate", panel.transform, new Vector3(0, 0, 0.04f), new Vector3(1.55f, 1.12f, 0.035f), panelMat);
        TextMesh title = TextLine("Panel Title", panel.transform, new Vector3(-0.70f, 0.48f, -0.01f), 0.035f, Color.white);
        TextMesh metrics = TextLine("Panel Metrics", panel.transform, new Vector3(-0.70f, 0.34f, -0.01f), 0.021f, Color.white);
        TextMesh agentText = TextLine("Panel Agent", panel.transform, new Vector3(-0.70f, -0.02f, -0.01f), 0.019f, Color.white);
        TextMesh compare = TextLine("Panel Compare", panel.transform, new Vector3(-0.70f, -0.36f, -0.01f), 0.020f, Color.white);
        var status = panel.AddComponent<WorldStatusPanel>();
        SetObject(status, "inputProvider", input);
        SetObject(status, "observer", observer);
        SetObject(status, "loadEstimator", load);
        SetObject(status, "agent", agent);
        SetObject(status, "supplyTray", tray);
        SetObject(status, "workbench", bench);
        SetObject(status, "conveyor", conveyor);
        SetObject(status, "titleText", title);
        SetObject(status, "metricsText", metrics);
        SetObject(status, "agentText", agentText);
        SetObject(status, "compareText", compare);
        SetObject(status, "faceCamera", faceCamera);
    }

    private static void BuildLightingAndCamera(Transform parent, Transform lookAt)
    {
        Light key = Group("Key Light", parent).AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.4f;
        key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light fill = Group("Cell Fill Light", parent).AddComponent<Light>();
        fill.type = LightType.Point;
        fill.intensity = 700f;
        fill.range = 5f;
        fill.transform.position = new Vector3(1.8f, 2.6f, -1.4f);

        GameObject cameraGo = new GameObject("PC Spectator Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetParent(parent, false);
        cameraGo.transform.position = new Vector3(2.05f, 1.85f, -2.8f);
        Vector3 target = lookAt != null ? lookAt.position : new Vector3(0, 1.2f, 0.2f);
        cameraGo.transform.rotation = Quaternion.LookRotation(target - cameraGo.transform.position, Vector3.up);
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 58f;
        cameraGo.AddComponent<AudioListener>();
    }

    private static GameObject Group(string name, Transform parent = null)
    {
        GameObject result = new GameObject(name);
        if (parent != null)
            result.transform.SetParent(parent, false);
        return result;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
        result.name = name;
        result.transform.SetParent(parent, false);
        result.transform.localPosition = localPosition;
        result.transform.localScale = size;
        result.GetComponent<Renderer>().sharedMaterial = material;
        return result;
    }

    private static GameObject Sphere(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject result = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        result.name = name;
        result.transform.SetParent(parent, false);
        result.transform.localPosition = localPosition;
        result.transform.localScale = size;
        result.GetComponent<Renderer>().sharedMaterial = material;
        return result;
    }

    private static GameObject Capsule(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject result = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        result.name = name;
        result.transform.SetParent(parent, false);
        result.transform.localPosition = localPosition;
        result.transform.localScale = size;
        result.GetComponent<Renderer>().sharedMaterial = material;
        return result;
    }

    private static TextMesh TextLine(string name, Transform parent, Vector3 localPosition, float size, Color color)
    {
        GameObject result = new GameObject(name);
        result.transform.SetParent(parent, false);
        result.transform.localPosition = localPosition;
        TextMesh text = result.AddComponent<TextMesh>();
        text.characterSize = size;
        text.fontSize = 64;
        text.anchor = TextAnchor.UpperLeft;
        text.alignment = TextAlignment.Left;
        text.color = color;
        return text;
    }

    private static Renderer[] Renderers(params Transform[] transforms)
    {
        Renderer[] result = new Renderer[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            result[i] = transforms[i] != null ? transforms[i].GetComponent<Renderer>() : null;
        return result;
    }

    private static Material Mat(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        return material;
    }

    private static Material TransparentMat(string name, Color color)
    {
        Material material = Mat(name, color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
        string name = System.IO.Path.GetFileName(folder);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void SetObject(Object target, string name, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
        {
            Debug.LogWarning("Missing serialized property " + name + " on " + target.name);
            return;
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector(Object target, string name, Vector3 value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            return;
        property.vector3Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum<T>(Object target, string name, T value) where T : System.Enum
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            return;
        property.enumValueIndex = System.Convert.ToInt32(value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRendererArray(Object target, string name, Renderer[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            return;
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetTransformArray(Object target, string name, Transform[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            return;
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
