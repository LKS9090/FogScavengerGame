using System;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class SwitchTrainingAgent : Agent
{
    public RobotMotor Motor;
    public Transform Plate, Obstacle;
    public Camera View;
    public const int ObservationCount=28;
    public const float StepSeconds=.02f, TimeLimit=15f, HoldRequired=3f, RayRange=8f;
    public float Elapsed {get;private set;}
    public float Hold {get;private set;}
    public bool Pressed => RobotPressureSwitch.IsPressed(Motor.transform.position,Plate.position);
    public int Episode {get;private set;}
    public int Configuration {get;private set;}
    public int CollisionSteps {get;private set;}
    public string LastOutcome {get;private set;}="none";
    public string LogPath {get;private set;}
    public bool AcceptanceMode;
    public bool TeacherControl {get;private set;}
    public bool EpisodeEnded => ending;
    public bool ResetInvariantPassed {get;private set;}
    public int ManualConfiguration;
    int step, sequence;
    StreamWriter log;
    bool ending;
    GUIStyle text, title;
    Texture2D background;

    public override void Initialize()
    {
        Motor=GetComponent<RobotMotor>();
        Time.fixedDeltaTime=StepSeconds;
        AcceptanceMode=Array.IndexOf(Environment.GetCommandLineArgs(),"--switch-self-test")>=0;
        if(AcceptanceMode) { Academy.Instance.AutomaticSteppingEnabled=false; gameObject.AddComponent<SwitchEnvironmentAcceptance>(); }
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--teacher-collect")>=0) {
            AcceptanceMode=true; Academy.Instance.AutomaticSteppingEnabled=false;
            gameObject.AddComponent<SwitchTeacherCollection>();
        }
        TeacherControl=Array.IndexOf(Environment.GetCommandLineArgs(),"--teacher-demo")>=0;
        string dir=Path.Combine(Application.persistentDataPath,"SwitchLogs"); Directory.CreateDirectory(dir);
        LogPath=Path.Combine(dir,"session-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".jsonl");
        log=new StreamWriter(LogPath,false,new UTF8Encoding(false)){AutoFlush=true};
    }
    public override void OnEpisodeBegin()
    {
        int config=(int)Academy.Instance.EnvironmentParameters.GetWithDefault("configuration",ManualConfiguration);
        ResetConfiguration(config);
    }
    public void ResetConfiguration(int id)
    {
        if(Episode>0 && !ending) Record("reset_interruption",0);
        Configuration=Mathf.Clamp(id,0,19); Episode++; step=0; Elapsed=Hold=0; CollisionSteps=0; ending=false;
        // A fixed catalog permits reproducible layouts. IDs 16-19 are reserved for checks, not teacher collection.
        var rng=new System.Random(31000+Configuration);
        float jitter=(float)rng.NextDouble()*.6f-.3f;
        var rotation=Quaternion.Euler(0,(Configuration%4)*90,0);
        var spawn=rotation*new Vector3(-4+jitter,0,-4);
        var goal=rotation*new Vector3(4,.04f,4-jitter);
        Motor.ResetMotor(spawn);
        Plate.position=goal;
        Obstacle.gameObject.SetActive(Configuration>=4);
        Obstacle.SetPositionAndRotation(new Vector3(0,1,0),rotation);
        Obstacle.localScale=new Vector3(1,2,3+(Configuration%3)*.3f);
        Physics.SyncTransforms();
        ResetInvariantPassed=Motor.LastAction==Vector2.zero && Motor.Velocity==Vector3.zero && Hold==0 && Elapsed==0
            && CollisionSteps==0 && !Pressed;
        Record("reset",0);
    }
    public float[] Observe()
    {
        var delta=Plate.position-Motor.transform.position;
        var result=new float[ObservationCount];
        result[0]=Mathf.Clamp(delta.x/12,-1,1); result[1]=Mathf.Clamp(delta.y/3,-1,1); result[2]=Mathf.Clamp(delta.z/12,-1,1);
        result[3]=Motor.Velocity.x/RobotMotor.MaxSpeed; result[4]=Motor.Velocity.z/RobotMotor.MaxSpeed;
        result[5]=Motor.LastAction.x; result[6]=Motor.LastAction.y;
        result[7]=Pressed?1:0; result[8]=Mathf.Clamp01(Hold/HoldRequired); result[9]=Mathf.Clamp01(Elapsed/TimeLimit);
        result[10]=Motor.RequestedVelocity.x/RobotMotor.MaxSpeed; result[11]=Motor.RequestedVelocity.z/RobotMotor.MaxSpeed;
        for(int i=0;i<16;i++) {
            var direction=Quaternion.Euler(0,i*22.5f,0)*Vector3.forward;
            result[12+i]=Physics.Raycast(Motor.transform.position+Vector3.up*.6f,direction,out var hit,RayRange,1<<9,QueryTriggerInteraction.Ignore)?hit.distance/RayRange:1;
        }
        return result;
    }
    public override void CollectObservations(VectorSensor sensor) { foreach(float value in Observe()) sensor.AddObservation(value); }
    public override void OnActionReceived(ActionBuffers actions)
    {
        if(!AcceptanceMode) Advance(new Vector2(actions.ContinuousActions[0],actions.ContinuousActions[1]));
    }
    public void Advance(Vector2 action)
    {
        if(ending) return;
        if(!Motor.Step(action,StepSeconds)) {Finish("invalid_action",false,-1); return;}
        step++; Elapsed=step*StepSeconds;
        if(Motor.HitObstacle) CollisionSteps++;
        float speed=new Vector2(Motor.Velocity.x,Motor.Velocity.z).magnitude;
        Hold=Pressed && speed<=.15f ? Hold+StepSeconds : 0;
        // Sparse task reward only. Shaping and teacher design belong to the following step.
        if(Hold+0.0001f>=HoldRequired) {Finish("success",false,1); return;}
        if(Mathf.Abs(Motor.transform.position.x)>6.5f || Mathf.Abs(Motor.transform.position.z)>6.5f || Motor.transform.position.y<-.5f)
        {Finish("out_of_bounds",false,-1); return;}
        if(Elapsed>=TimeLimit) {Finish("timeout",true,0); return;}
        if(step%5==0) Record("transition",0);
    }
    void Finish(string outcome,bool interrupted,float reward)
    {
        ending=true; LastOutcome=outcome; AddReward(reward); Record(outcome,reward);
        if(!AcceptanceMode) {if(interrupted) EpisodeInterrupted(); else EndEpisode();}
    }
    public override void Heuristic(in ActionBuffers actions)
    {
        if(TeacherControl) {
            var teacher=SwitchRuleTeacher.Decide(Observe());
            var controls=actions.ContinuousActions; controls[0]=teacher.x; controls[1]=teacher.y; return;
        }
        // Keyboard movement is screen relative; the shared action protocol itself is world X/Z.
        var forward=Vector3.ProjectOnPlane(View.transform.forward,Vector3.up).normalized;
        var right=Vector3.ProjectOnPlane(View.transform.right,Vector3.up).normalized;
        var direction=Vector3.ClampMagnitude(forward*Input.GetAxisRaw("Vertical")+right*Input.GetAxisRaw("Horizontal"),1);
        var output=actions.ContinuousActions;
        output[0]=direction.x; output[1]=direction.z;
    }
    void Update()
    {
        if(AcceptanceMode || Academy.Instance.IsCommunicatorOn) return;
        if(Input.GetKeyDown(KeyCode.R)) EndEpisode();
        if(Input.GetKeyDown(KeyCode.T)) {TeacherControl=!TeacherControl; EndEpisode();}
        if(Input.GetKeyDown(KeyCode.N)) {ManualConfiguration=(ManualConfiguration+1)%20; EndEpisode();}
        if(Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
    }
    [Serializable] class Entry
    {
        public string environment="switch-local-v1",motor="capsule-motor-v1",type,last_outcome;
        public int episode,configuration,sequence,physics_step,collision_steps;
        public float elapsed,hold,reward; public bool pressed,reset_valid;
        public Vector3 position,goal,velocity; public Vector2 action; public float[] observation;
    }
    void Record(string type,float reward)
    {
        if(log==null) return;
        log.WriteLine(JsonUtility.ToJson(new Entry {type=type,last_outcome=LastOutcome,episode=Episode,configuration=Configuration,
            sequence=sequence++,physics_step=step,collision_steps=CollisionSteps,elapsed=Elapsed,hold=Hold,reward=reward,
            pressed=Pressed,reset_valid=ResetInvariantPassed,position=Motor.transform.position,goal=Plate.position,
            velocity=Motor.Velocity,action=Motor.LastAction,observation=Observe()}));
    }
    void OnGUI()
    {
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/800f,1));
        if(text==null) {
            var font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",18);
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true}; title=new GUIStyle(text){fontSize=23,fontStyle=FontStyle.Bold};
            background=new Texture2D(1,1); background.SetPixel(0,0,new Color(.03f,.045f,.06f,.9f)); background.Apply();
        }
        GUI.DrawTexture(new Rect(16,16,440,200),background);
        GUI.Label(new Rect(30,28,420,40),"动作训练场 · 规则教师",title);
        GUI.Label(new Rect(30,72,415,45),"移动到黄色开关，停稳并保持 3 秒\n当前控制："+(Academy.Instance.IsCommunicatorOn?"Python 接口":TeacherControl?"规则教师（代码控制，未训练）":"键盘遥操作（没有训练模型）"),text);
        GUI.Label(new Rect(30,125,415,76),"配置 "+Configuration+"  ·  回合 "+Episode+"  ·  时间 "+Elapsed.ToString("F1")+" / 15 秒\n压住："+(Pressed?"是":"否")+"  稳定保持："+Hold.ToString("F1")+" 秒\n上次结果："+LastOutcome,text);
        GUI.DrawTexture(new Rect(0,754,1280,46),background);
        GUI.Label(new Rect(25,762,1220,34),"T 切换教师 / 手动  ·  WASD 手动移动  ·  R 重置  ·  N 下一个配置  ·  Esc 退出",text);
    }
    void OnApplicationQuit(){log?.Dispose(); log=null;}
}
