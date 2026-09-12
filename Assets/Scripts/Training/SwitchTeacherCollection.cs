using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Explicit command-line batch only. Each decision repeats for five real motor physics steps.
public class SwitchTeacherCollection : MonoBehaviour
{
    [Serializable] class Sample {
        public string schema="switch-demonstration-v1",teacher=SwitchRuleTeacher.Version,split,outcome;
        public int configuration,variant,decision,physics_steps;
        public float[] observation,action,next_observation;
        public float reward;
        public bool terminal,truncated;
    }
    [Serializable] class EpisodeResult {
        public int configuration,variant,decisions,collision_steps;
        public string split,outcome;
        public float elapsed,hold;
        public bool accepted;
    }
    [Serializable] class Report {
        public string teacher=SwitchRuleTeacher.Version,error="",data_directory;
        public bool passed,trained_model=false;
        public int episodes,accepted,training_samples,validation_samples;
        public List<EpisodeResult> results=new List<EpisodeResult>();
    }
    void Start()
    {
        var agent=GetComponent<SwitchTrainingAgent>();
        string directory=Path.Combine(Application.persistentDataPath,"TeacherRuns",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(directory);
        var report=new Report {data_directory=directory};
        try {
            using(var train=new StreamWriter(Path.Combine(directory,"train.jsonl"),false,new UTF8Encoding(false)))
            using(var validation=new StreamWriter(Path.Combine(directory,"validation.jsonl"),false,new UTF8Encoding(false)))
            using(var rejected=new StreamWriter(Path.Combine(directory,"rejected.jsonl"),false,new UTF8Encoding(false))) {
                for(int config=0;config<16;config++) for(int variant=0;variant<16;variant++) {
                    string split=variant<12?"train":"validation";
                    agent.ResetConfiguration(config);
                    var random=new System.Random(42000+config*100+variant);
                    var offset=new Vector3((float)random.NextDouble()*1.2f-.6f,0,(float)random.NextDouble()*1.2f-.6f);
                    agent.Motor.ResetMotor(agent.Motor.transform.position+offset);
                    agent.Motor.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                    Physics.SyncTransforms();
                    var samples=new List<Sample>();
                    for(int decision=0;decision<150 && !agent.EpisodeEnded;decision++) {
                        var before=agent.Observe(); var action=SwitchRuleTeacher.Decide(before);
                        int ticks=0;
                        for(;ticks<5 && !agent.EpisodeEnded;ticks++) {agent.Advance(action); Physics.SyncTransforms();}
                        samples.Add(new Sample {split=split,configuration=config,variant=variant,decision=decision,physics_steps=ticks,
                            observation=before,action=new[]{action.x,action.y},next_observation=agent.Observe(),terminal=agent.EpisodeEnded,
                            truncated=agent.EpisodeEnded && agent.LastOutcome=="timeout",outcome=agent.EpisodeEnded?agent.LastOutcome:"running",
                            reward=agent.EpisodeEnded?(agent.LastOutcome=="success"?1:agent.LastOutcome=="timeout"?0:-1):0});
                    }
                    bool accepted=agent.EpisodeEnded && agent.LastOutcome=="success" && agent.CollisionSteps==0;
                    report.results.Add(new EpisodeResult {configuration=config,variant=variant,split=split,outcome=agent.LastOutcome,
                        elapsed=agent.Elapsed,hold=agent.Hold,collision_steps=agent.CollisionSteps,decisions=samples.Count,accepted=accepted});
                    report.episodes++; if(accepted) report.accepted++;
                    var writer=accepted?(split=="train"?train:validation):rejected;
                    foreach(var sample in samples) writer.WriteLine(JsonUtility.ToJson(sample));
                    if(accepted) {if(split=="train") report.training_samples+=samples.Count; else report.validation_samples+=samples.Count;}
                }
            }
            report.passed=report.accepted==report.episodes && report.episodes==256;
        } catch(Exception e) {report.error=e.ToString();}
        File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
        File.WriteAllText(Path.Combine(Application.persistentDataPath,"teacher-latest.txt"),directory);
        Debug.Log("TEACHER COLLECTION "+report.accepted+"/"+report.episodes+" "+report.error);
        Application.Quit(report.passed?0:1);
    }
}
