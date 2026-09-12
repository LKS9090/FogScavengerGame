using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

public class P01Sandbox : MonoBehaviour
{
    public PlayerMovement Player;
    public RobotCompanion Robot;
    public Camera ViewCamera;
    public GameObject TargetMarker;
    public bool ManualInput = true;
    public int Episode { get; private set; }
    public string LogPath { get; private set; }
    Vector3 playerSpawn, robotSpawn;
    Quaternion playerRotation, robotRotation;
    StreamWriter writer;
    float episodeStart, nextSample;
    int tick, sequence;
    GUIStyle titleStyle, textStyle, buttonStyle;
    Texture2D panelTexture;

    [Serializable]
    public class Record
    {
        public int schema = 1;
        public string environment = "p01-sandbox-v1";
        public string controller = "rules-navmesh-v1";
        public int episode, sequence, tick, command_id;
        public float time;
        public string type, mode, status;
        public Vector3 player_position, robot_position, robot_velocity, desired_velocity, target;
        public Vector2 player_input;
        public bool has_path;
    }

    void Start()
    {
        Player.ReadKeyboard = false;
        playerSpawn = Player.transform.position;
        playerRotation = Player.transform.rotation;
        robotSpawn = Robot.transform.position;
        robotRotation = Robot.transform.rotation;
        var folder = Path.Combine(Application.persistentDataPath, "P01Logs");
        Directory.CreateDirectory(folder);
        LogPath = Path.Combine(folder, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
        writer = new StreamWriter(LogPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        Robot.StateChanged += OnRobotState;
        ResetEpisode();
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--p01-self-test") >= 0)
        {
            ManualInput = false;
            Time.captureDeltaTime = 1f / 60f;
            gameObject.AddComponent<P01Acceptance>();
        }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--p01-preview") >= 0)
            StartCoroutine(SavePreview());
    }

    IEnumerator SavePreview()
    {
        yield return new WaitForSeconds(1f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "p01-preview.png"));
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }

    void Update()
    {
        if (ManualInput)
        {
            Player.MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (Input.GetKeyDown(KeyCode.F)) CommandFollow();
            if (Input.GetKeyDown(KeyCode.G)) CommandWait();
            if (Input.GetKeyDown(KeyCode.R)) ResetEpisode();
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
            if (Input.GetMouseButtonDown(1) && !OverPanel())
            {
                if (Physics.Raycast(ViewCamera.ScreenPointToRay(Input.mousePosition), out var hit, 150f)
                    && hit.collider.gameObject.layer == 8 && hit.normal.y > 0.9f)
                    CommandGoTo(hit.point);
                else
                    CommandGoTo(new Vector3(50, 0, 50));
            }
        }
        tick++;
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + 0.2f;
            Write("observation");
        }
        if (Player.transform.position.y < -3) ResetEpisode();
    }

    bool OverPanel() => Input.mousePosition.x < 350 && Input.mousePosition.y > Screen.height - 280;

    public void CommandFollow()
    {
        TargetMarker.SetActive(false);
        Robot.Follow();
        Write("command_follow");
    }

    public void CommandWait()
    {
        TargetMarker.SetActive(false);
        Robot.Wait();
        Write("command_wait");
    }

    public bool CommandGoTo(Vector3 point)
    {
        bool accepted = Robot.GoTo(point);
        TargetMarker.SetActive(accepted);
        if (accepted) TargetMarker.transform.position = point + Vector3.up * 0.05f;
        Write(accepted ? "command_goto" : "command_rejected");
        return accepted;
    }

    public void ResetEpisode()
    {
        if (Episode > 0) Write("episode_end");
        Player.ResetPose(playerSpawn, playerRotation);
        Robot.ResetRobot(robotSpawn, robotRotation);
        TargetMarker.SetActive(false);
        Physics.SyncTransforms();
        Episode++;
        episodeStart = Time.time;
        tick = 0;
        nextSample = Time.time;
        Write("reset");
    }

    void OnRobotState(string status) => Write("state_changed");

    void Write(string type)
    {
        if (writer == null) return;
        writer.WriteLine(JsonUtility.ToJson(new Record {
            episode = Episode, sequence = sequence++, tick = tick, time = Time.time - episodeStart,
            command_id = Robot.CommandId, type = type, mode = Robot.Mode.ToString(), status = Robot.Status,
            player_position = Player.transform.position, robot_position = Robot.transform.position,
            robot_velocity = Robot.Agent.velocity, desired_velocity = Robot.Agent.desiredVelocity,
            target = Robot.Goal, player_input = Player.MoveInput,
            has_path = Robot.Agent.hasPath
        }));
    }

    void OnDestroy()
    {
        if (Robot != null) Robot.StateChanged -= OnRobotState;
        writer?.Dispose();
        if (panelTexture != null) Destroy(panelTexture);
    }

    string StatusLabel()
    {
        switch (Robot.Status)
        {
            case "following": return "正在跟随你";
            case "near_player": return "已在你身边";
            case "going": return "正在前往标记点";
            case "arrived": return "已到达标记点";
            case "unreachable": return "目标不可达，请重新指定";
            case "blocked": return "通路受阻，请让出空间或召回";
            default: return "原地等待";
        }
    }

    void OnGUI()
    {
        if (titleStyle == null)
        {
            var font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18);
            titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.035f, 0.06f, 0.09f, 0.94f));
            panelTexture.Apply();
        }
        GUI.DrawTexture(new Rect(16,16,324,254), panelTexture);
        GUI.Label(new Rect(32,28,300,34), "雾境拾荒 · 搭档试验场", titleStyle);
        GUI.Label(new Rect(32,66,292,30), "机器人：" + StatusLabel(), textStyle);
        GUI.Label(new Rect(32,98,292,28), "距离 " + Vector3.Distance(Player.transform.position, Robot.transform.position).ToString("F1") + " 米  ·  第 " + Episode + " 回合", textStyle);
        if (GUI.Button(new Rect(32,138,138,38), "F  跟随 / 召回", buttonStyle)) CommandFollow();
        if (GUI.Button(new Rect(180,138,138,38), "G  原地等待", buttonStyle)) CommandWait();
        GUI.Label(new Rect(32,186,292,52), "WASD 移动 · 右键地面指定目标\nR 重置场景 · Esc 退出", textStyle);
        GUI.Label(new Rect(32,238,292,24), "蓝色：伙伴   绿色：玩家", textStyle);
    }
}
