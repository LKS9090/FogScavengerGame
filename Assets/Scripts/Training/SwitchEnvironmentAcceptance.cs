using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

// Deterministic environment fixtures, not a teacher policy or a learned-policy evaluation.
public class SwitchEnvironmentAcceptance : MonoBehaviour
{
    SwitchTrainingAgent agent;
    readonly List<string> checks=new List<string>();
    [Serializable] class Report {public bool passed; public string error,log; public int configurations; public string[] checks;}
    void Start()
    {
        agent=GetComponent<SwitchTrainingAgent>();
        try { Check(); Finish(true,""); } catch(Exception e) { Finish(false,e.ToString()); }
    }
    void Require(bool ok,string message) {if(!ok) throw new InvalidOperationException(message);}
    void Tick(Vector2 action) {agent.Advance(action); Physics.SyncTransforms();}
    void MoveTo(Vector3 target)
    {
        for(int i=0;i<400 && !agent.EpisodeEnded;i++) {
            var d=target-agent.Motor.transform.position; d.y=0;
            if(d.magnitude<.12f) break;
            float speed=Mathf.Min(3.5f,d.magnitude*3);
            Tick(new Vector2(d.x,d.z).normalized*speed/RobotMotor.MaxSpeed);
        }
        var delta=target-agent.Motor.transform.position; delta.y=0;
        Require(delta.magnitude<.2f,"Fixture failed to reach "+target+" actual "+agent.Motor.transform.position);
    }
    void Check()
    {
        Require(GetComponent<NavMeshAgent>()==null,"Hidden navigation agent in motor scene");
        for(int config=0;config<20;config++) {
            agent.ResetConfiguration(config); Require(agent.ResetInvariantPassed,"Reset invariant failed");
            var originalSpawn=agent.Motor.transform.position; var originalGoal=agent.Plate.position;
            agent.ResetConfiguration(config);
            Require(agent.Motor.transform.position==originalSpawn && agent.Plate.position==originalGoal,"Configuration is not deterministic");
            var rotation=Quaternion.Euler(0,(config%4)*90,0);
            MoveTo(rotation*new Vector3(-3,0,4)); MoveTo(agent.Plate.position);
            for(int i=0;i<180 && !agent.EpisodeEnded;i++) Tick(Vector2.zero);
            Require(agent.EpisodeEnded && agent.LastOutcome=="success","No actual hold success config "+config);
            Require(agent.CollisionSteps==0,"Fixture path collided config "+config);
            Require(agent.Elapsed<=15 && agent.Hold>=2.999f,"Incorrect success time");
            foreach(float value in agent.Observe()) Require(!float.IsNaN(value) && !float.IsInfinity(value),"Non-finite observation");
        }
        checks.Add("20 fixed configurations reset reproducibly and complete actual movement + stable switch hold without NavMesh; fixtures only, not teacher performance");
        agent.ResetConfiguration(4);
        agent.Motor.ResetMotor(new Vector3(-1,0,0));
        for(int i=0;i<50;i++) Tick(Vector2.right);
        Require(agent.Motor.transform.position.x<-.75f && agent.CollisionSteps>0,"Obstacle collision failed");
        var observations=agent.Observe(); Require(observations.Length==28 && observations[16]<.2f,"East obstacle ray incorrect");
        checks.Add("Collider blocks commands into wall; 16 physical rays produce finite 28-dimensional observations");
        agent.ResetConfiguration(0);
        Tick(new Vector2(5,5)); Require(agent.Motor.LastAction.magnitude<=1.0001f,"Diagonal action speed exploit");
        for(int i=0;i<10;i++) Tick(Vector2.zero);
        Require(new Vector2(agent.Motor.Velocity.x,agent.Motor.Velocity.z).magnitude<.01f,"Zero action fails to brake");
        agent.ResetConfiguration(0);
        agent.Motor.ResetMotor(agent.Plate.position+Vector3.up*.02f);
        for(int i=0;i<100;i++) Tick(Vector2.zero);
        Require(agent.Hold>1 && !agent.EpisodeEnded,"Hold succeeds too early");
        agent.Motor.ResetMotor(agent.Plate.position+Vector3.right*1.5f); Tick(Vector2.zero);
        Require(agent.Hold==0,"Leaving switch retains hold credit");
        agent.Motor.ResetMotor(agent.Plate.position+Vector3.up*.02f);
        for(int i=0;i<160 && !agent.EpisodeEnded;i++) Tick(Vector2.zero);
        Require(agent.LastOutcome=="success" && agent.EpisodeEnded,"Hold does not finish");
        int before=agent.Episode; Tick(Vector2.zero); Require(agent.Episode==before,"Duplicate completion resets episode");
        checks.Add("Action clamping, braking, interrupted hold reset and single success boundary verified");
        agent.ResetConfiguration(0);
        for(int i=0;i<750;i++) Tick(Vector2.zero);
        Require(agent.LastOutcome=="timeout" && agent.EpisodeEnded,"Timeout not detected");
        agent.ResetConfiguration(0); Tick(new Vector2(float.NaN,0));
        Require(agent.LastOutcome=="invalid_action" && agent.EpisodeEnded,"Invalid numeric action not rejected");
        agent.ResetConfiguration(0); agent.Motor.ResetMotor(new Vector3(7,0,0)); Tick(Vector2.zero);
        Require(agent.LastOutcome=="out_of_bounds" && agent.EpisodeEnded,"Out-of-bounds not detected");
        checks.Add("Timeout, invalid action and out-of-bounds have distinct outcomes");
        for(int i=0;i<10;i++) {
            agent.ResetConfiguration(i); Tick(Vector2.right); agent.ResetConfiguration(i);
            Require(agent.ResetInvariantPassed && agent.Elapsed==0 && agent.Hold==0 && !agent.EpisodeEnded,"Residual state after reset");
        }
        checks.Add("Ten consecutive resets clear action, velocity, hold, timer, collision count and terminal state");
    }
    void Finish(bool ok,string error)
    {
        File.WriteAllText(Path.Combine(Application.persistentDataPath,"switch-acceptance.json"),JsonUtility.ToJson(new Report {
            passed=ok,error=error,log=agent.LogPath,configurations=20,checks=checks.ToArray()},true));
        Debug.Log("SWITCH ENVIRONMENT CHECK "+ok+" "+error); Application.Quit(ok?0:1);
    }
}
