using UnityEngine;

// Stateless local steering: the teacher has exactly the student's observation, no map or config ID.
public static class SwitchRuleTeacher
{
    public const string Version="ray-steering-v1";
    public static Vector2 Decide(float[] observation)
    {
        var delta=new Vector2(observation[0],observation[2])*12;
        float distance=delta.magnitude;
        if(distance<.18f) return Vector2.zero;
        Vector2 direction=delta.normalized;
        // Short-range repulsion from the physical rays creates clearance around walls.
        for(int i=0;i<16;i++) {
            float range=Mathf.Max(.1f,observation[12+i]*8);
            if(range>=1.6f) continue;
            float angle=i*Mathf.PI/8;
            var ray=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
            direction-=ray*((1/range-1/1.6f)*.65f);
        }
        float speed=Mathf.Min(3f,distance*2);
        // Slow down when local clearance requires a sharp change of direction.
        speed*=Mathf.Lerp(.45f,1,Mathf.Clamp01(Vector2.Dot(direction.normalized,delta.normalized)));
        return direction.normalized*(speed/RobotMotor.MaxSpeed);
    }
}
