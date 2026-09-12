using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public static class SwitchTrainingBuilder
{
    const string ScenePath="Assets/Scenes/03_SwitchMotorTraining.unity";
    static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/P02Generated/"+name+".mat");
    static GameObject Cube(string name,Vector3 position,Vector3 size,string material,int layer=9)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.layer=layer;
        o.transform.position=position; o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=Material(material); return o;
    }
    public static void CreateAndBuild()
    {
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null) throw new InvalidOperationException("Existing training scene: use Build");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Cube("TrainingFloor",new Vector3(0,-.25f,0),new Vector3(12,.5f,12),"Concrete",8);
        Cube("BoundaryN",new Vector3(0,.6f,6),new Vector3(12,1.2f,.2f),"Walls");
        Cube("BoundaryS",new Vector3(0,.6f,-6),new Vector3(12,1.2f,.2f),"Walls");
        Cube("BoundaryE",new Vector3(6,.6f,0),new Vector3(.2f,1.2f,12),"Walls");
        Cube("BoundaryW",new Vector3(-6,.6f,0),new Vector3(.2f,1.2f,12),"Walls");
        var obstacle=Cube("LayoutObstacle",new Vector3(0,1,0),new Vector3(1,2,3),"Walls");
        var plate=Cube("SharedPressureSwitch",new Vector3(4,.04f,4),new Vector3(1.5f,.08f,1.5f),"Interaction",8);
        var cameraObject=new GameObject("Main Camera"); cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=8.5f;
        camera.transform.position=new Vector3(12,24,-16); camera.transform.LookAt(Vector3.zero);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.17f,.20f,.24f);
        cameraObject.AddComponent<AudioListener>();
        var robot=new GameObject("TrainingRobot"); robot.transform.position=new Vector3(-4,0,-4);
        var body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Body"; body.transform.SetParent(robot.transform,false);
        body.transform.localPosition=Vector3.up*.6f; body.transform.localScale=Vector3.one*.6f;
        body.GetComponent<Renderer>().sharedMaterial=Material("Robot"); UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        var controller=robot.AddComponent<CharacterController>(); controller.center=Vector3.up*.6f;
        controller.height=1.2f; controller.radius=.35f; controller.stepOffset=.3f; controller.slopeLimit=45; controller.skinWidth=.03f;
        var motor=robot.AddComponent<RobotMotor>();
        var behavior=robot.AddComponent<BehaviorParameters>(); behavior.BehaviorName="SwitchMotorV1";
        behavior.BrainParameters.VectorObservationSize=SwitchTrainingAgent.ObservationCount; behavior.BrainParameters.NumStackedVectorObservations=1;
        behavior.BrainParameters.ActionSpec=ActionSpec.MakeContinuous(2); behavior.BehaviorType=BehaviorType.Default;
        var agent=robot.AddComponent<SwitchTrainingAgent>(); agent.Motor=motor; agent.Plate=plate.transform; agent.Obstacle=obstacle.transform; agent.View=camera;
        agent.MaxStep=0; // Time truncation is explicit in simulated seconds, never MaxStep's generic reset.
        var requester=robot.AddComponent<DecisionRequester>(); requester.DecisionPeriod=5; requester.TakeActionsBetweenDecisions=true;
        var sun=new GameObject("Daylight").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.2f;
        sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(50,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
        EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets(); Build();
    }
    [MenuItem("FogScavenger/Build Switch Motor Training")]
    public static void Build()
    {
        PlayerSettings.companyName="LKS9090"; PlayerSettings.productName="FogScavenger Switch Lab";
        PlayerSettings.defaultIsNativeResolution=false; PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},
            locationPathName="Builds/SwitchTraining/FogScavenger.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Training environment build failed");
    }
}
