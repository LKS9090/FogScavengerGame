using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public static class P02SceneBuilder
{
    static Transform geometry;
    static Material gray, dark, light, yellow, green, blue;
    const string Folder="Assets/P02Generated";
    public static void CreateAndBuild()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/02_MultilevelCoop.unity") != null)
            throw new InvalidOperationException("P02 scene already exists. Use Build to preserve scene edits.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets","P02Generated");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        gray=Mat("Concrete",new Color(.49f,.51f,.53f)); dark=Mat("Walls",new Color(.32f,.35f,.39f));
        light=Mat("Steps",new Color(.66f,.68f,.70f)); yellow=Mat("Interaction",new Color(1,.72f,.10f));
        green=Mat("Player",new Color(.20f,.72f,.22f)); blue=Mat("Robot",new Color(.12f,.48f,.90f));
        geometry=new GameObject("NavigationGeometry").transform;
        // Ground tiles leave real holes for the lowland and pool, no hidden ground under stairs.
        float[] xs={-20,-18,0,2,18,20}, zs={-20,-16,-7,-4,20};
        for(int i=0;i<xs.Length-1;i++) for(int j=0;j<zs.Length-1;j++) {
            float x=(xs[i]+xs[i+1])/2,z=(zs[j]+zs[j+1])/2;
            if ((x>-18 && x<0 && z>-16 && z<-7) || (x>2 && x<18 && z>-16 && z<-4)) continue;
            Box("Ground",new Vector3(x,-.25f,z),new Vector3(xs[i+1]-xs[i],.5f,zs[j+1]-zs[j]),gray,8);
        }
        Box("LowlandFloor",new Vector3(-9,-1.75f,-11.5f),new Vector3(18,.5f,9),gray,8);
        Box("PoolFloor",new Vector3(10,-3.25f,-10),new Vector3(16,.5f,12),light,8);
        // Terrace and two independent access routes.
        Box("RaisedTerrace",new Vector3(-12,1.5f,10.5f),new Vector3(12,3,11),gray,8);
        Ramp("TerraceStairs",-15,-4,5,0,3,3,true);
        Ramp("TerraceRamp",-8,-4,5,0,3,3,false);
        RailX(-18,-16.5f,5,3); RailX(-13.5f,-9.5f,5,3); RailX(-6.5f,-6,5,3);
        RailX(-18,-6,16,3); RailZ(-18,5,16,3); RailZ(-6,5,16,3);
        // Lowland entry, within the cutout; descending toward the south.
        Ramp("LowlandStairs",-5,-13,-7,-1.5f,0,3,true);
        Pit("Lowland",-18,0,-16,-7,-1.5f,-6.5f,-3.5f,false);
        // Pool has opposing north and south stair exits, both usable by the robot.
        Ramp("PoolNorthStairs",4.5f,-10,-4,-3,0,3,true);
        Ramp("PoolSouthStairs",15.5f,-16,-10,0,-3,3,true);
        Pit("Pool",2,18,-16,-4,-3,3,6,true);
        // Replace south pool rail opening for its second exit.
        var south=GameObject.Find("PoolSouthRail"); if(south!=null) UnityEngine.Object.DestroyImmediate(south);
        RailX(2,14,-16,0); RailX(17,18,-16,0);
        // Perimeter boundary, never a jumping/falling route.
        RailX(-20,20,-20,0); RailX(-20,20,20,0); RailZ(-20,-20,20,0); RailZ(20,-20,20,0);

        // Building: two rooms. Both exterior exits belong to the first room; no bypass into storage.
        Wall("WestWall",1,10,.3f,12); Wall("EastWall",17,10,.3f,12);
        Wall("SouthLeft",2.25f,4,2.5f,.3f); Wall("SouthRight",11.25f,4,11.5f,.3f);
        Wall("NorthLeft",2.25f,16,2.5f,.3f); Wall("NorthRight",11.25f,16,11.5f,.3f);
        // Door gaps x=3.5..5.5, wide enough for both actors.
        Wall("PartitionSouth",10,6, .3f,4); Wall("PartitionNorth",10,13,.3f,6);
        Box("DoorLintel",new Vector3(10,2.8f,9),new Vector3(.35f,.4f,2),dark,9);
        Box("TableA",new Vector3(3, .55f,12),new Vector3(1.6f,1.1f,2),gray,9);
        Box("TableB",new Vector3(7, .55f,13),new Vector3(1.6f,1.1f,2),gray,9);
        var plate=Box("RobotPressurePlate",new Vector3(7, .04f,7),new Vector3(1.5f,.08f,1.5f),yellow,8);
        Box("WireToDoor",new Vector3(8.5f,.015f,7),new Vector3(3,.03f,.07f),yellow,2,false);
        Box("WireTurn",new Vector3(9.8f,.015f,8),new Vector3(.07f,.03f,2),yellow,2,false);
        var surface=geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects=CollectObjects.Children; surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask=(1<<8)|(1<<9); surface.overrideVoxelSize=true; surface.voxelSize=.075f;
        surface.BuildNavMesh();
        var navpath=Folder+"/Navigation.asset";
        if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(navpath)!=null) AssetDatabase.DeleteAsset(navpath);
        AssetDatabase.CreateAsset(surface.navMeshData,navpath);
        // Dynamic door must be absent from the bake, then carve while closed.
        var door=Box("Gate",new Vector3(10,1.25f,9),new Vector3(.3f,2.5f,2),yellow,9);
        door.transform.SetParent(null);
        var obstacle=door.AddComponent<NavMeshObstacle>(); obstacle.shape=NavMeshObstacleShape.Box;
        obstacle.size=Vector3.one; obstacle.carving=true; obstacle.carveOnlyStationary=false;
        var roof=Box("BuildingRoof",new Vector3(9,3.25f,10),new Vector3(16.5f,.35f,12.5f),dark,9);
        roof.transform.SetParent(null);
        var pickup=Box("ObjectiveItem",new Vector3(14,.5f,10),Vector3.one*.65f,yellow,2,false); pickup.transform.SetParent(null);
        var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name="DestinationMarker";
        marker.transform.localScale=new Vector3(.6f,.025f,.6f); marker.GetComponent<Renderer>().sharedMaterial=yellow;
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>()); marker.SetActive(false);
        var cameraObject=new GameObject("Main Camera"); cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=10.5f;
        camera.nearClipPlane=.1f; camera.farClipPlane=100; camera.backgroundColor=new Color(.17f,.20f,.24f);
        camera.clearFlags=CameraClearFlags.SolidColor; cameraObject.AddComponent<AudioListener>();
        var playerObject=GameObject.CreatePrimitive(PrimitiveType.Capsule); playerObject.name="Player";
        UnityEngine.Object.DestroyImmediate(playerObject.GetComponent<Collider>());
        playerObject.GetComponent<Renderer>().sharedMaterial=green;
        var cc=playerObject.AddComponent<CharacterController>(); cc.height=2; cc.radius=.4f; cc.stepOffset=.3f; cc.slopeLimit=45; cc.skinWidth=.03f;
        var player=playerObject.AddComponent<P02Player>(); player.View=camera; playerObject.transform.position=new Vector3(-2,1,1);
        var avoid=playerObject.AddComponent<NavMeshObstacle>(); avoid.shape=NavMeshObstacleShape.Capsule; avoid.radius=.45f; avoid.height=2;
        var robotObject=new GameObject("Robot"); robotObject.transform.position=new Vector3(-4,0,1);
        var robotBody=GameObject.CreatePrimitive(PrimitiveType.Capsule); robotBody.name="Body";
        robotBody.transform.SetParent(robotObject.transform,false); robotBody.transform.localPosition=Vector3.up*.6f;
        robotBody.transform.localScale=Vector3.one*.6f; robotBody.GetComponent<Renderer>().sharedMaterial=blue;
        var agent=robotObject.AddComponent<NavMeshAgent>(); agent.radius=.35f; agent.height=1.2f; agent.speed=6.5f;
        agent.acceleration=18; agent.angularSpeed=540; agent.enabled=false;
        var robot=robotObject.AddComponent<RobotCompanion>(); robot.Player=playerObject.transform; robot.FollowPlayerElevation=true;
        var world=new GameObject("P02World").AddComponent<P02World>();
        world.Player=player; world.Robot=robot; world.View=camera; world.Roof=roof.GetComponent<Renderer>();
        world.Door=door; world.Pickup=pickup; world.Plate=plate.transform; world.Marker=marker;
        camera.transform.position=playerObject.transform.position-Vector3.up+world.CameraOffset;
        camera.transform.rotation=Quaternion.LookRotation(-world.CameraOffset);
        var sun=new GameObject("Daylight").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.2f;
        sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(50,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/02_MultilevelCoop.unity"); AssetDatabase.SaveAssets();
        Build();
    }
    static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) {m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color); EditorUtility.SetDirty(m); return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,int layer,bool collision=true)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.layer=layer;
        o.transform.SetParent(geometry); o.transform.position=p; o.transform.localScale=size;
        o.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>()); return o;
    }
    static void Wall(string name,float x,float z,float width,float depth) => Box(name,new Vector3(x,1.5f,z),new Vector3(width,3,depth),dark,9);
    static void RailX(float a,float b,float z,float y,string name="Guardrail")
    {
        if(b-a<.05f) return;
        var wall=Box(name,new Vector3((a+b)/2,y+.55f,z),new Vector3(b-a,1.1f,.16f),dark,9);
        wall.GetComponent<Renderer>().enabled=false;
        Box("RailTop",new Vector3((a+b)/2,y+1.05f,z),new Vector3(b-a,.09f,.12f),dark,2,false);
        for(float x=a;x<=b+.01f;x+=Mathf.Max(.1f,(b-a)/Mathf.Ceil((b-a)/2))) Box("RailPost",new Vector3(x,y+.52f,z),new Vector3(.1f,1.04f,.1f),dark,2,false);
    }
    static void RailZ(float x,float a,float b,float y)
    {
        var wall=Box("Guardrail",new Vector3(x,y+.55f,(a+b)/2),new Vector3(.16f,1.1f,b-a),dark,9); wall.GetComponent<Renderer>().enabled=false;
        Box("RailTop",new Vector3(x,y+1.05f,(a+b)/2),new Vector3(.12f,.09f,b-a),dark,2,false);
        for(float z=a;z<=b+.01f;z+=Mathf.Max(.1f,(b-a)/Mathf.Ceil((b-a)/2))) Box("RailPost",new Vector3(x,y+.52f,z),new Vector3(.1f,1.04f,.1f),dark,2,false);
    }
    static void Pit(string name,float x0,float x1,float z0,float z1,float bottom,float opening0,float opening1,bool pool)
    {
        Box(name+"West",new Vector3(x0,bottom/2,(z0+z1)/2),new Vector3(.2f,-bottom,z1-z0),gray,9);
        Box(name+"East",new Vector3(x1,bottom/2,(z0+z1)/2),new Vector3(.2f,-bottom,z1-z0),gray,9);
        Box(name+"South",new Vector3((x0+x1)/2,bottom/2,z0),new Vector3(x1-x0,-bottom,.2f),gray,9);
        Box(name+"North",new Vector3((x0+x1)/2,bottom/2,z1),new Vector3(x1-x0,-bottom,.2f),gray,9);
        RailZ(x0,z0,z1,0); RailZ(x1,z0,z1,0);
        if(!pool) RailX(x0,x1,z0,0);
        RailX(x0,opening0,z1,0); RailX(opening1,x1,z1,0);
    }
    static void Ramp(string name,float x,float z0,float z1,float y0,float y1,float width,bool steps)
    {
        float lo=Mathf.Min(y0,y1)-.25f;
        var vertices=new [] {new Vector3(-width/2,lo,z0),new Vector3(width/2,lo,z0),new Vector3(-width/2,lo,z1),new Vector3(width/2,lo,z1),
            new Vector3(-width/2,y0,z0),new Vector3(width/2,y0,z0),new Vector3(-width/2,y1,z1),new Vector3(width/2,y1,z1)};
        var mesh=new Mesh {vertices=vertices,triangles=new [] {4,6,5,5,6,7,0,1,2,1,3,2,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7}};
        mesh.RecalculateNormals(); var path=Folder+"/"+name+".asset";
        if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh,path);
        var o=new GameObject(name); o.layer=8; o.transform.SetParent(geometry); o.transform.position=new Vector3(x,0,0);
        o.AddComponent<MeshFilter>().sharedMesh=mesh; o.AddComponent<MeshRenderer>().sharedMaterial=light; o.AddComponent<MeshCollider>().sharedMesh=mesh;
        if(steps) {
            o.GetComponent<Renderer>().enabled=false;
            int n=Mathf.CeilToInt(Mathf.Abs(y1-y0)/.15f);
            for(int i=0;i<n;i++) {
                float t=(i+.5f)/n, y=Mathf.Lerp(y0,y1,t); float baseY=lo;
                Box(name+"Tread",new Vector3(x,(y+baseY)/2,Mathf.Lerp(z0,z1,t)),new Vector3(width,y-baseY,(z1-z0)/n),light,2,false);
            }
        }
        // Continuous side barriers follow the incline, including descending access routes.
        for(int side=-1;side<=1;side+=2) {
            var bar=Box(name+"Side",new Vector3(x+side*width/2,(y0+y1)/2+.55f,(z0+z1)/2),new Vector3(.12f,1.1f,Vector2.Distance(new Vector2(z0,y0),new Vector2(z1,y1))),dark,9);
            bar.transform.rotation=Quaternion.Euler(-Mathf.Atan2(y1-y0,z1-z0)*Mathf.Rad2Deg,0,0);
        }
    }
    [MenuItem("FogScavenger/Build P02 Greybox")]
    public static void Build()
    {
        PlayerSettings.companyName="LKS9090"; PlayerSettings.productName="FogScavenger P02";
        PlayerSettings.defaultIsNativeResolution=false; PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/02_MultilevelCoop.unity"},
            locationPathName="Builds/P02/FogScavenger.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("P02 build failed");
    }
}
