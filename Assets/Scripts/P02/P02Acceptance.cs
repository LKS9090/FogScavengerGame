using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Only added by --p02-self-test. It drives the same character and command components as play.
public class P02Acceptance : MonoBehaviour
{
    P02World world;
    readonly List<string> checks=new List<string>();
    [Serializable] class Report { public bool passed; public string error, log; public string[] checks; }
    IEnumerator Start()
    {
        world=GetComponent<P02World>();
        var stack=new Stack<IEnumerator>(); stack.Push(Checks());
        while(stack.Count>0) {
            object current=null;
            try { var top=stack.Peek(); if(!top.MoveNext()) {stack.Pop(); continue;} current=top.Current; }
            catch(Exception e) {Finish(false,e.Message); yield break;}
            if(current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        Finish(true,"");
    }
    void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
    IEnumerator Walk(Vector3 feet,float timeout=12)
    {
        float end=Time.time+timeout;
        while(Time.time<end) {
            var delta=feet-(world.Player.transform.position-Vector3.up); delta.y=0;
            if(delta.magnitude<.17f) break;
            var forward=Vector3.ProjectOnPlane(world.View.transform.forward,Vector3.up).normalized;
            var right=Vector3.ProjectOnPlane(world.View.transform.right,Vector3.up).normalized;
            world.Player.InputDirection=new Vector2(Vector3.Dot(delta,right),Vector3.Dot(delta,forward)).normalized;
            yield return null;
        }
        world.Player.InputDirection=Vector2.zero;
        yield return new WaitForSeconds(.25f);
        Require(Vector3.Distance(world.Player.transform.position-Vector3.up,feet)<.35f,"Player route failed at "+feet+" actual "+world.Player.transform.position);
    }
    IEnumerator Send(Vector3 goal)
    {
        Require(world.GoTo(goal),"Robot path rejected "+goal);
        float end=Time.time+18;
        while(Time.time<end && world.Robot.Status!="arrived") yield return null;
        Require(world.Robot.Status=="arrived" && Vector3.Distance(world.Robot.transform.position,goal)<.45f,"Robot arrival failed "+goal+" actual "+world.Robot.transform.position+" state "+world.Robot.Status);
        Require(world.ArrivalPrompt,"Arrival choice missing");
    }
    IEnumerator Checks()
    {
        yield return new WaitForSeconds(.5f);
        Require(world.Robot.Mode==RobotCompanion.CommandMode.Follow,"Default is not follow");
        world.Wait();
        world.Player.ResetPose(new Vector3(-15,1,-5));
        yield return Walk(new Vector3(-15,3,8));
        yield return Walk(new Vector3(-8,3,8));
        yield return Walk(new Vector3(-8,0,-5));
        checks.Add("Player climbs visible stairs and descends terrace ramp through actual controller");
        world.Player.ResetPose(new Vector3(-5,1,-6));
        yield return Walk(new Vector3(-5,-1.5f,-14));
        yield return Walk(new Vector3(-5,0,-6));
        world.Player.ResetPose(new Vector3(4.5f,1,-3));
        yield return Walk(new Vector3(4.5f,-3,-11));
        yield return Walk(new Vector3(10,-3,-11));
        yield return Walk(new Vector3(15.5f,-3,-9));
        yield return Walk(new Vector3(15.5f,0,-17));
        checks.Add("Player enters/exits lowland and crosses dry pool using opposite stair exits");
        world.Player.ResetPose(new Vector3(-19,1,0));
        var start=world.Player.transform.position; world.Player.InputDirection=Vector2.up;
        yield return new WaitForSeconds(.6f); world.Player.InputDirection=Vector2.zero;
        float walked=world.Player.transform.position.z-start.z;
        world.Player.ResetPose(start); world.Player.Running=true; world.Player.InputDirection=Vector2.up;
        yield return new WaitForSeconds(.6f); world.Player.InputDirection=Vector2.zero; world.Player.Running=false;
        Require(world.Player.transform.position.z-start.z > walked*1.35f,"Run speed not higher");
        world.Player.ResetPose(new Vector3(-10,4,15)); world.Player.InputDirection=Vector2.up;
        yield return new WaitForSeconds(1); world.Player.InputDirection=Vector2.zero;
        Require(world.Player.transform.position.z<16 && world.Player.transform.position.y>3.8f,"Terrace guardrail failed");
        checks.Add("Run speed exceeds walking; terrace edge blocks drops");
        world.Player.ResetPose(new Vector3(-19,1,0));
        yield return Send(new Vector3(-12,3,12));
        yield return Send(new Vector3(-10,-1.5f,-13));
        yield return Send(new Vector3(10,-3,-11));
        yield return Send(new Vector3(16,0,-18));
        checks.Add("Robot reaches terrace, lowland, pool bottom and opposite exterior using baked navigation");
        world.Player.ResetPose(new Vector3(-12,4,12)); world.Follow();
        float end=Time.time+20;
        while(Time.time<end && Vector3.Distance(world.Robot.transform.position,new Vector3(-12,3,12))>2) yield return null;
        Require(world.Robot.transform.position.y>2.8f,"Follow still assumes flat ground");
        world.Wait();
        checks.Add("Follow preserves elevation and reaches player on high terrace");
        world.ResetEpisode(); world.Wait(); yield return new WaitForSeconds(.5f);
        Require(world.Roof.enabled,"Roof absent outside");
        Require(!world.Interact() && !world.Collected,"Remote pickup accepted");
        Require(!world.CanSee(new Vector3(8,.85f,10),new Vector3(14,.5f,10),8),"Item visible through solid partition");
        Require(!world.GoTo(new Vector3(14,0,10)),"Closed gate allowed path to storage");
        yield return Send(new Vector3(7,.04f,7));
        yield return new WaitForSeconds(.5f);
        Require(world.DoorOpen,"Pressure switch did not open gate");
        world.Player.ResetPose(new Vector3(4.5f,1,3));
        yield return Walk(new Vector3(4.5f,0,6));
        Require(!world.Roof.enabled,"Roof did not hide on entry");
        yield return Walk(new Vector3(7.8f,0,9));
        yield return Walk(new Vector3(12,0,9));
        yield return Walk(new Vector3(13.2f,0,10));
        Require(world.RobotSeesItem==world.CanSee(world.Robot.transform.position+Vector3.up*.85f,new Vector3(14,.5f,10),8),"Robot perception disagrees with physical sightline");
        Require(world.Interact() && world.Collected && !world.Pickup.activeSelf,"Actual pickup failed");
        Require(!world.Interact(),"Duplicate pickup accepted");
        yield return Walk(new Vector3(12,0,9)); yield return Walk(new Vector3(8,0,9));
        yield return Walk(new Vector3(4.5f,0,6)); yield return Walk(new Vector3(4.5f,0,3));
        Require(world.Roof.enabled && world.Completed,"Exit did not restore roof or complete task");
        world.Follow(); yield return new WaitForSeconds(2);
        Require(!world.DoorOpen && !world.ArrivalPrompt,"Recall failed to release switch");
        checks.Add("Closed gate blocks robot; pressure switch opens it; actual enter/pickup/return completes cooperation; roof restores and recall closes gate");
        Require(world.CanSee(new Vector3(13,.85f,10),new Vector3(14,.5f,10),8),"Unobstructed item not visible");
        Require(!world.CanSee(new Vector3(0,.85f,0),new Vector3(14,.5f,10),8),"Range not enforced");
        checks.Add("Robot sensing enforces distance and solid-wall occlusion independently of hidden roof");
        for(int i=0;i<10;i++) {
            world.GoTo(new Vector3(-12,3,12)); world.Player.Running=true; world.Player.InputDirection=Vector2.right;
            yield return new WaitForSeconds(.1f); world.ResetEpisode();
            Require(!world.Collected && !world.Completed && !world.ArrivalPrompt && !world.DoorOpen && world.Pickup.activeSelf,"Reset retained interaction state");
            Require(world.Player.InputDirection==Vector2.zero && !world.Player.Running && !world.Marker.activeSelf,"Reset retained input");
            Require(world.Robot.Mode==RobotCompanion.CommandMode.Follow && !world.Robot.Agent.hasPath,"Reset retained old command");
            Require(Vector3.Distance(world.Player.transform.position,world.PlayerSpawn)<.05f && Vector3.Distance(world.Robot.transform.position,world.RobotSpawn)<.05f,"Reset pose wrong");
            yield return new WaitForSeconds(.25f);
        }
        checks.Add("Ten resets clear interactions, inventory, arrival choice, target, running and old paths; default follow restored");
    }
    void Finish(bool ok,string error)
    {
        File.WriteAllText(Path.Combine(Application.persistentDataPath,"p02-acceptance.json"),JsonUtility.ToJson(new Report {passed=ok,error=error,log=world.LogPath,checks=checks.ToArray()},true));
        Debug.Log("P02 ACCEPTANCE "+ok+" "+error); Application.Quit(ok?0:1);
    }
}
