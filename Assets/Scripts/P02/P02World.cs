using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(20)]
public class P02World : MonoBehaviour
{
    public P02Player Player;
    public RobotCompanion Robot;
    public Camera View;
    public Renderer Roof;
    public GameObject Door, Pickup, Marker;
    public Transform Plate;
    public bool ManualInput = true;
    public bool DoorOpen { get; private set; }
    public bool Collected { get; private set; }
    public bool Completed { get; private set; }
    public bool RobotSeesItem { get; private set; }
    public bool ArrivalPrompt { get; private set; }
    public int Episode { get; private set; }
    public string LogPath { get; private set; }
    public Vector3 CameraOffset = new Vector3(12, 24, -16);
    public Vector3 PlayerSpawn = new Vector3(-2,1,1), RobotSpawn = new Vector3(-4,0,1);
    public string Message = "让机器人站在黄色开关上，再进门取物。";
    StreamWriter log;
    float nextSample, started;
    int sequence;
    GUIStyle label, heading, button;
    Texture2D backdrop;
    readonly Vector3 itemPosition = new Vector3(14,0.5f,10);
    public bool Inside => Player.transform.position.x > 1 && Player.transform.position.x < 17
        && Player.transform.position.z > 4 && Player.transform.position.z < 16;

    void Start()
    {
        var folder = Path.Combine(Application.persistentDataPath,"P02Logs"); Directory.CreateDirectory(folder);
        LogPath = Path.Combine(folder,"session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
        log = new StreamWriter(LogPath,false,new UTF8Encoding(false)) { AutoFlush = true };
        Robot.StateChanged += OnState;
        ResetEpisode();
        if (Array.IndexOf(Environment.GetCommandLineArgs(),"--p02-self-test") >= 0)
        { ManualInput = false; Time.captureDeltaTime = 1f/60; gameObject.AddComponent<P02Acceptance>(); }
    }
    void Update()
    {
        if (ManualInput)
        {
            Player.InputDirection = new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            Player.Running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F)) Follow();
            if (Input.GetKeyDown(KeyCode.G)) Wait();
            if (Input.GetKeyDown(KeyCode.R)) ResetEpisode();
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
            if (Input.GetMouseButtonDown(1) && !OverUI())
            {
                if (Physics.Raycast(View.ScreenPointToRay(Input.mousePosition),out var hit,150,~(1<<2))
                    && hit.collider.gameObject.layer == 8 && hit.normal.y > 0.7f) GoTo(hit.point);
                else Message = "请右键可行走地面；屋顶、墙体不是目标。";
            }
        }
        // Visual roof hiding does not disable physical walls or perception occlusion.
        Roof.enabled = !Inside;
        Roof.GetComponent<Collider>().enabled = !Inside;
        bool pressed = Vector3.Distance(Robot.transform.position,Plate.position) < 0.72f;
        // Hold the gate open while either body occupies its threshold: no crushing/soft lock.
        bool threshold = InThreshold(Player.transform.position) || InThreshold(Robot.transform.position);
        SetDoor(pressed || threshold);
        RobotSeesItem = !Collected && CanSee(Robot.transform.position + Vector3.up*0.85f,itemPosition,8f);
        if (Collected && !Inside && !Completed) { Completed = true; Message = "协作完成：物品已带出建筑。按 R 再试一次。"; Record("task_complete"); }
        if (Time.time >= nextSample) { nextSample = Time.time + 0.2f; Record("observation"); }
        if (Player.transform.position.y < -8) { ResetEpisode(); Message = "检测到越界，已恢复起点。"; }
    }
    bool InThreshold(Vector3 p) => Mathf.Abs(p.x-10)<0.85f && Mathf.Abs(p.z-9)<1.3f && Mathf.Abs(p.y)<2;
    void LateUpdate()
    {
        var focus = Player.transform.position - Vector3.up;
        View.transform.position = focus + CameraOffset;
        View.transform.rotation = Quaternion.LookRotation(-CameraOffset);
    }
    public bool CanSee(Vector3 eye, Vector3 target, float range)
    {
        var delta = target-eye;
        return delta.sqrMagnitude <= range*range && !Physics.Raycast(eye,delta.normalized,delta.magnitude,1<<9,QueryTriggerInteraction.Ignore);
    }
    public bool GoTo(Vector3 target)
    {
        ArrivalPrompt = false;
        bool ok = Robot.GoTo(target);
        Marker.SetActive(ok);
        if (ok) Marker.transform.position = target + Vector3.up*0.06f;
        Message = ok ? "机器人正在前往指定地点。" : "目标不可达：请检查门或通路。";
        Record(ok ? "command_goto" : "command_rejected"); return ok;
    }
    public void Follow() { ArrivalPrompt=false; Marker.SetActive(false); Robot.Follow(); Message="机器人跟随 / 召回中。"; Record("command_follow"); }
    public void Wait() { ArrivalPrompt=false; Marker.SetActive(false); Robot.Wait(); Message="机器人继续原地等候。"; Record("command_wait"); }
    void OnState(string state)
    {
        if (state=="arrived") { ArrivalPrompt=true; Marker.SetActive(false); Message="机器人已到达：可以召回或继续等候。"; }
        if (state=="blocked" || state=="unreachable") Message="机器人通路受阻，请重新指定位置或召回。";
        Record("robot_state");
    }
    public bool Interact()
    {
        var feet = Player.transform.position-Vector3.up;
        if (Collected || Vector3.Distance(feet,itemPosition) > 1.8f || !CanSee(Player.transform.position,itemPosition,2.2f))
        { Message="需要靠近且无遮挡，才能拾取黄色物品。"; Record("interaction_failed"); return false; }
        Collected=true; Pickup.SetActive(false); Message="已拾取物品，先返回开关一侧，再召回机器人。"; Record("item_collected"); return true;
    }
    void SetDoor(bool open)
    {
        if (DoorOpen==open) return;
        DoorOpen=open; Door.SetActive(!open); Record(open?"gate_open":"gate_closed");
    }
    public void ResetEpisode()
    {
        if (Episode>0) Record("episode_end");
        Player.ResetPose(PlayerSpawn); Robot.ResetRobot(RobotSpawn,Quaternion.identity);
        Collected=false; Completed=false; RobotSeesItem=false; ArrivalPrompt=false;
        Pickup.SetActive(true); DoorOpen=false; Door.SetActive(true); Marker.SetActive(false); Roof.enabled=true;
        Roof.GetComponent<Collider>().enabled=true;
        Episode++; started=Time.time; nextSample=Time.time;
        Robot.Follow(); Message="默认跟随。去建筑黄色开关处开始协作。";
        Physics.SyncTransforms(); Record("reset");
    }
    [Serializable] class Entry
    {
        public string environment="p02-greybox-v1", controller="rules-navmesh-height-v1", type, mode, status;
        public int episode, sequence; public float time;
        public Vector3 player,robot,goal,velocity; public Vector2 input;
        public bool running,door_open,collected,completed,roof_visible,item_visible_to_robot,arrival_prompt;
    }
    void Record(string type)
    {
        if (log==null) return;
        log.WriteLine(JsonUtility.ToJson(new Entry { type=type,episode=Episode,sequence=sequence++,time=Time.time-started,
            player=Player.transform.position,robot=Robot.transform.position,goal=Robot.Goal,velocity=Robot.Agent.velocity,
            input=Player.InputDirection,running=Player.Running,mode=Robot.Mode.ToString(),status=Robot.Status,
            door_open=DoorOpen,collected=Collected,completed=Completed,roof_visible=Roof.enabled,
            item_visible_to_robot=RobotSeesItem,arrival_prompt=ArrivalPrompt }));
    }
    bool OverUI()
    {
        var p=new Vector2(Input.mousePosition.x/Screen.width*1280,(Screen.height-Input.mousePosition.y)/Screen.height*800);
        return new Rect(16,16,360,190).Contains(p) || new Rect(0,730,1280,70).Contains(p)
            || (ArrivalPrompt && new Rect(870,16,394,130).Contains(p));
    }
    string StateLabel()
    {
        switch(Robot.Status) {
            case "following": return "跟随中"; case "near_player": return "在你身边";
            case "going": return "前往目标"; case "arrived": return "到达 / 等候";
            case "blocked": return "受阻"; case "unreachable": return "目标不可达"; default: return "等候";
        }
    }
    void OnGUI()
    {
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/800f,1));
        if (label==null) {
            var f=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",18);
            label=new GUIStyle(GUI.skin.label){font=f,fontSize=16,wordWrap=true};
            heading=new GUIStyle(label){fontSize=21,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){font=f,fontSize=16};
            backdrop=new Texture2D(1,1); backdrop.SetPixel(0,0,new Color(.03f,.045f,.06f,.88f)); backdrop.Apply();
        }
        GUI.DrawTexture(new Rect(16,16,360,190),backdrop);
        GUI.Label(new Rect(30,26,340,32),"雾境拾荒 · 多层协作灰盒",heading);
        GUI.Label(new Rect(30,65,340,28),"机器人："+StateLabel()+"  |  "+(Inside?"建筑内":"室外"),label);
        GUI.Label(new Rect(30,96,332,46),"任务："+(Completed?"已完成":Collected?"带物品离开建筑":"机器人压住开关，人物进门取物"),label);
        if (GUI.Button(new Rect(30,154,155,36),"F 跟随 / 召回",button)) Follow();
        if (GUI.Button(new Rect(195,154,155,36),"G 原地等待",button)) Wait();
        if (ArrivalPrompt) {
            GUI.DrawTexture(new Rect(870,16,394,130),backdrop);
            GUI.Label(new Rect(886,30,370,35),"机器人已到达，正在等候",heading);
            if (GUI.Button(new Rect(886,83,170,40),"召回",button)) Follow();
            if (GUI.Button(new Rect(1070,83,175,40),"继续等候",button)) Wait();
        }
        GUI.DrawTexture(new Rect(0,730,1280,70),backdrop);
        GUI.Label(new Rect(22,736,1240,28),Message,label);
        GUI.Label(new Rect(22,766,1240,28),"WASD 移动   Shift 跑步   F 召回   G 等待   右键地面 到点   E 拾取   R 重置   Esc 退出",label);
    }
    void OnDestroy() { if(Robot!=null) Robot.StateChanged-=OnState; log?.Dispose(); if(backdrop!=null) Destroy(backdrop); }
}
