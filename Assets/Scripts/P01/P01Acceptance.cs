using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Activated only by --p01-self-test in an independent build.
public class P01Acceptance : MonoBehaviour
{
    P01Sandbox sandbox;
    readonly List<string> checks = new List<string>();
    [Serializable] class Report { public bool passed; public string error; public string log; public string[] checks; }

    IEnumerator Start()
    {
        sandbox = GetComponent<P01Sandbox>();
        var routine = RunChecks();
        while (true)
        {
            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception error) { Finish(false, error.Message); yield break; }
            if (!more) break;
            yield return routine.Current;
        }
        Finish(true, "");
    }

    void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    IEnumerator RunChecks()
    {
        yield return null;
        var player = sandbox.Player;
        var robot = sandbox.Robot;
        var playerStart = player.transform.position;
        var robotStart = robot.transform.position;
        Require(robot.Agent.isOnNavMesh, "Robot not on NavMesh");
        sandbox.CommandWait();
        player.MoveInput = Vector2.up;
        yield return new WaitForSeconds(1.5f);
        player.MoveInput = Vector2.zero;
        Require(player.transform.position.z > 1f && player.transform.position.z < 2.5f, "Player wall collision failed");
        Require(Vector3.Distance(robotStart, robot.transform.position) < 0.05f, "Waiting robot moved with player");
        checks.Add("Player moves, stops at wall; robot independently waits");

        player.ResetPose(new Vector3(0,1,6), Quaternion.identity);
        sandbox.CommandFollow();
        float deadline = Time.time + 8f, maxX = 0;
        while (Time.time < deadline && Vector2.Distance(new Vector2(robot.transform.position.x,robot.transform.position.z), new Vector2(0,6)) > 1.9f)
        {
            maxX = Mathf.Max(maxX, Mathf.Abs(robot.transform.position.x));
            yield return null;
        }
        Require(Vector2.Distance(new Vector2(robot.transform.position.x,robot.transform.position.z), new Vector2(0,6)) < 2f, "Follow did not reach player");
        Require(maxX > 3.1f, "Robot did not route around wall");
        checks.Add("Follow routes around static wall and stops near player");

        sandbox.CommandWait();
        var waitPosition = robot.transform.position;
        yield return new WaitForSeconds(0.6f);
        Require(Vector3.Distance(waitPosition, robot.transform.position) < 0.05f && !robot.Agent.hasPath, "Wait did not cancel path");
        Require(sandbox.CommandGoTo(new Vector3(-6,0,6)), "Valid goal rejected");
        deadline = Time.time + 8f;
        while (Time.time < deadline && robot.Status != "arrived") yield return null;
        Require(robot.Status == "arrived", "GoTo did not complete");
        Require(Vector3.Distance(robot.transform.position, new Vector3(-6,0,6)) < 0.45f, "Arrival position inaccurate");
        Require(!sandbox.CommandGoTo(new Vector3(50,0,50)) && robot.Status == "unreachable" && !robot.Agent.hasPath, "Unreachable goal not safely rejected");
        checks.Add("Wait cancellation, GoTo arrival, unreachable goal feedback");

        Require(sandbox.CommandGoTo(new Vector3(6,0,-6)), "Switch test goal rejected");
        float originalSpeed = robot.Agent.speed;
        robot.Agent.speed = 0;
        yield return new WaitForSeconds(2.4f);
        Require(robot.Status == "blocked", "Stalled actuator did not produce blocked feedback");
        robot.Agent.speed = originalSpeed;
        yield return new WaitForSeconds(0.5f);
        Require(robot.Status == "going", "Movement did not recover after actuator stall");
        checks.Add("Controlled actuator stall produces blocked feedback and recovers when restored");
        yield return new WaitForSeconds(0.2f);
        sandbox.CommandFollow();
        deadline = Time.time + 8f;
        while (Time.time < deadline && Vector3.Distance(robot.transform.position,player.transform.position) > 2.2f) yield return null;
        Require(robot.Mode == RobotCompanion.CommandMode.Follow && Vector3.Distance(robot.transform.position,player.transform.position) < 2.3f, "Recall failed to replace old target");
        checks.Add("Recall replaces active GoTo command");

        for (int i = 0; i < 10; i++)
        {
            sandbox.CommandGoTo(new Vector3(6,0,-6));
            player.MoveInput = Vector2.right;
            yield return new WaitForSeconds(0.12f);
            sandbox.ResetEpisode();
            Require(Vector3.Distance(player.transform.position,playerStart) < 0.15f, "Player reset position wrong");
            Require(Vector3.Distance(robot.transform.position,robotStart) < 0.15f, "Robot reset position wrong");
            Require(robot.CommandId == 0 && robot.Mode == RobotCompanion.CommandMode.Wait && !robot.Agent.hasPath, "Old command remained after reset");
            Require(robot.Agent.velocity.sqrMagnitude < 0.001f && player.MoveInput == Vector2.zero && !sandbox.TargetMarker.activeSelf, "Velocity, input or marker remained after reset");
            yield return new WaitForSeconds(0.25f);
            Require(Vector3.Distance(robot.transform.position,robotStart) < 0.15f, "Old command restarted after reset");
        }
        checks.Add("10 consecutive resets clear positions, commands, path, velocity, input and marker; no delayed restart");
    }

    void Finish(bool passed, string error)
    {
        var report = new Report { passed = passed, error = error, log = sandbox.LogPath, checks = checks.ToArray() };
        File.WriteAllText(Path.Combine(Application.persistentDataPath,"p01-acceptance.json"), JsonUtility.ToJson(report,true));
        Application.Quit(passed ? 0 : 1);
    }
}
