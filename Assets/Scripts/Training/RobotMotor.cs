using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RobotMotor : MonoBehaviour
{
    public const float MaxSpeed = 6.5f, Acceleration = 18f, TurnSpeed = 540f;
    public Vector2 LastAction { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 RequestedVelocity => requestedVelocity;
    public bool HitObstacle { get; private set; }
    CharacterController body;
    Vector3 requestedVelocity;
    float fallSpeed;
    void Awake() { body=GetComponent<CharacterController>(); body.minMoveDistance=0; }

    // The only movement entry point for manual control, future teacher and learned policy.
    // No NavMeshAgent, teleporting to a target, or hidden route following occurs here.
    public bool Step(Vector2 action,float dt)
    {
        if(float.IsNaN(action.x)||float.IsNaN(action.y)||float.IsInfinity(action.x)||float.IsInfinity(action.y))
        { LastAction=Vector2.zero; return false; }
        action=new Vector2(Mathf.Clamp(action.x,-1,1),Mathf.Clamp(action.y,-1,1));
        LastAction=Vector2.ClampMagnitude(action,1);
        requestedVelocity=Vector3.MoveTowards(requestedVelocity,new Vector3(LastAction.x,0,LastAction.y)*MaxSpeed,Acceleration*dt);
        if(requestedVelocity.sqrMagnitude>.001f)
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(requestedVelocity),TurnSpeed*dt);
        if(body.isGrounded && fallSpeed<0) fallSpeed=-3;
        fallSpeed+=Physics.gravity.y*dt;
        var before=transform.position;
        var flags=body.Move((requestedVelocity+Vector3.up*fallSpeed)*dt);
        Velocity=(transform.position-before)/dt;
        HitObstacle=(flags&CollisionFlags.Sides)!=0;
        return true;
    }
    public void ResetMotor(Vector3 feet)
    {
        if(body==null) body=GetComponent<CharacterController>();
        body.enabled=false; transform.SetPositionAndRotation(feet,Quaternion.identity);
        requestedVelocity=Velocity=Vector3.zero; LastAction=Vector2.zero; fallSpeed=0; HitObstacle=false;
        body.enabled=true;
    }
}
