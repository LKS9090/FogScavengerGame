using System;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-50)]
public class RobotCompanion : MonoBehaviour
{
    public enum CommandMode { Wait, Follow, GoTo }
    public NavMeshAgent Agent { get; private set; }
    public CommandMode Mode { get; private set; }
    public string Status { get; private set; } = "waiting";
    public Vector3 Goal { get; private set; }
    public int CommandId { get; private set; }
    public event Action<string> StateChanged;
    public Transform Player;
    float nextPlan;
    float stalledTime;
    Vector3 lastPosition;
    NavMeshPath path;

    void Awake() { Agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); }
    void Start() { Agent.enabled = true; }

    public void ResetRobot(Vector3 position, Quaternion rotation)
    {
        Agent.ResetPath();
        Agent.isStopped = true;
        Agent.velocity = Vector3.zero;
        if (!Agent.Warp(position)) throw new InvalidOperationException("Robot spawn is outside NavMesh");
        transform.rotation = rotation;
        Mode = CommandMode.Wait;
        Status = "waiting";
        Goal = position;
        CommandId = 0;
        stalledTime = 0;
        nextPlan = 0;
        lastPosition = position;
    }

    public void Wait()
    {
        CommandId++;
        Mode = CommandMode.Wait;
        Stop();
        Goal = transform.position;
        ChangeStatus("waiting");
    }

    public void Follow()
    {
        CommandId++;
        Mode = CommandMode.Follow;
        stalledTime = 0;
        nextPlan = 0;
        ChangeStatus("following");
    }

    public bool GoTo(Vector3 point)
    {
        CommandId++;
        Mode = CommandMode.GoTo;
        Goal = point;
        stalledTime = 0;
        Stop();
        Agent.stoppingDistance = 0.2f;
        if (!Plan(point))
        {
            Mode = CommandMode.Wait;
            ChangeStatus("unreachable");
            return false;
        }
        ChangeStatus("going");
        return true;
    }

    bool Plan(Vector3 point)
    {
        if (!NavMesh.SamplePosition(point, out var hit, 0.5f, Agent.areaMask)
            || Mathf.Abs(point.y - hit.position.y) > 0.6f
            || !Agent.CalculatePath(hit.position, path)
            || path.status != NavMeshPathStatus.PathComplete)
            return false;
        Agent.isStopped = false;
        return Agent.SetPath(path);
    }

    void Update()
    {
        if (!Agent.isOnNavMesh) return;
        if (Mode == CommandMode.Follow && Time.time >= nextPlan)
        {
            nextPlan = Time.time + 0.2f;
            Goal = Player.position;
            Goal = new Vector3(Goal.x, 0, Goal.z);
            float distance = Vector3.Distance(transform.position, Goal);
            if (distance < 1.7f)
            {
                Stop();
                ChangeStatus("near_player");
            }
            else if (distance > 2f || !Agent.isStopped)
            {
                Agent.stoppingDistance = 1.55f;
                if (!Plan(Goal)) { Stop(); ChangeStatus("unreachable"); }
                else if (Status != "blocked") ChangeStatus("following");
            }
        }
        if (Mode == CommandMode.GoTo && !Agent.pathPending && Agent.hasPath
            && Agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Stop();
            Mode = CommandMode.Wait;
            ChangeStatus("unreachable");
        }
        if (Mode == CommandMode.GoTo && !Agent.pathPending && Agent.hasPath
            && Agent.remainingDistance <= Agent.stoppingDistance + 0.1f)
        {
            Stop();
            Mode = CommandMode.Wait;
            ChangeStatus("arrived");
        }
        if (Mode != CommandMode.Wait && Agent.hasPath && !Agent.isStopped)
        {
            if (Vector3.Distance(transform.position, lastPosition) < 0.002f)
                stalledTime += Time.deltaTime;
            else stalledTime = 0;
            if (stalledTime > 2f) ChangeStatus("blocked");
            else if (Status == "blocked") ChangeStatus(Mode == CommandMode.Follow ? "following" : "going");
        }
        lastPosition = transform.position;
    }

    void Stop()
    {
        Agent.ResetPath();
        Agent.isStopped = true;
        Agent.velocity = Vector3.zero;
        stalledTime = 0;
    }

    void ChangeStatus(string status)
    {
        if (Status == status) return;
        Status = status;
        StateChanged?.Invoke(status);
    }
}
