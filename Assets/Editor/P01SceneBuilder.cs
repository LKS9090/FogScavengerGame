using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class P01SceneBuilder
{
    public static void CreateAndBuild()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/01_PlayerSandbox.unity");
        if (UnityEngine.Object.FindObjectOfType<P01Sandbox>() != null) throw new Exception("Sandbox already configured; use Build only");
        var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
        var oldRobot = GameObject.Find("Robot");
        var geometry = new GameObject("NavigationGeometry");
        var ground = GameObject.Find("Ground");
        var wall = GameObject.Find("Wall");
        ground.transform.SetParent(geometry.transform, true);
        ground.layer = 8;
        wall.transform.SetParent(geometry.transform, true);
        var wallMaterial = wall.GetComponent<Renderer>().sharedMaterial;
        MakeWall("Boundary_N", new Vector3(0,0.6f,10), new Vector3(20,1.2f,0.4f), geometry.transform, wallMaterial);
        MakeWall("Boundary_S", new Vector3(0,0.6f,-10), new Vector3(20,1.2f,0.4f), geometry.transform, wallMaterial);
        MakeWall("Boundary_E", new Vector3(10,0.6f,0), new Vector3(0.4f,1.2f,20), geometry.transform, wallMaterial);
        MakeWall("Boundary_W", new Vector3(-10,0.6f,0), new Vector3(0.4f,1.2f,20), geometry.transform, wallMaterial);
        var surface = geometry.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.1f;
        surface.BuildNavMesh();
        if (!AssetDatabase.IsValidFolder("Assets/Navigation")) AssetDatabase.CreateFolder("Assets","Navigation");
        AssetDatabase.CreateAsset(surface.navMeshData,"Assets/Navigation/P01NavMesh.asset");

        var root = new GameObject("Robot");
        root.transform.position = new Vector3(2,0,0);
        oldRobot.name = "Body";
        oldRobot.transform.SetParent(root.transform, true);
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f; agent.height = 1.2f; agent.baseOffset = 0;
        agent.speed = 5.5f; agent.acceleration = 18; agent.angularSpeed = 540;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        var companion = root.AddComponent<RobotCompanion>();
        companion.Player = player.transform;
        var playerObstacle = player.gameObject.AddComponent<NavMeshObstacle>();
        playerObstacle.shape = NavMeshObstacleShape.Capsule;
        playerObstacle.radius = 0.5f; playerObstacle.height = 2; playerObstacle.carving = false;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "DestinationMarker";
        marker.transform.localScale = new Vector3(0.6f,0.025f,0.6f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(1f,0.65f,0.12f));
        AssetDatabase.CreateAsset(material,"Assets/Materials/Destination_Mat.mat");
        marker.GetComponent<Renderer>().sharedMaterial = material;
        marker.SetActive(false);
        var view = Camera.main;
        view.transform.position = new Vector3(0,17,-15);
        view.transform.LookAt(Vector3.zero);
        view.orthographic = true; view.orthographicSize = 12.5f;
        var sandbox = new GameObject("P01Sandbox").AddComponent<P01Sandbox>();
        sandbox.Player = player; sandbox.Robot = companion; sandbox.ViewCamera = view; sandbox.TargetMarker = marker;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Build();
    }

    static void MakeWall(string name, Vector3 position, Vector3 scale, Transform parent, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
    }

    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/01_PlayerSandbox.unity");
        UnityEngine.Object.FindObjectOfType<NavMeshAgent>().enabled = false;
        Camera.main.orthographicSize = 9.5f;
        Camera.main.rect = new Rect(0.25f, 0, 0.75f, 1);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        PlayerSettings.companyName = "LKS9090";
        PlayerSettings.productName = "FogScavenger P01";
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new [] { "Assets/Scenes/01_PlayerSandbox.unity" },
            locationPathName = "Builds/P01/FogScavenger.exe", target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("P01 build failed");
    }
}
