using UnityEngine;

public class P02Player : MonoBehaviour
{
    public Camera View;
    public Vector2 InputDirection;
    public bool Running;
    public float WalkSpeed = 4f, RunSpeed = 6f;
    CharacterController body;
    float fallSpeed;
    void Awake() { body = GetComponent<CharacterController>(); body.minMoveDistance = 0; }
    void Update()
    {
        var forward = Vector3.ProjectOnPlane(View.transform.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(View.transform.right, Vector3.up).normalized;
        var direction = Vector3.ClampMagnitude(forward * InputDirection.y + right * InputDirection.x, 1);
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        if (body.isGrounded && fallSpeed < 0) fallSpeed = -3;
        fallSpeed += Physics.gravity.y * Time.deltaTime;
        body.Move((direction * (Running ? RunSpeed : WalkSpeed) + Vector3.up * fallSpeed) * Time.deltaTime);
    }
    public void ResetPose(Vector3 center)
    {
        body.enabled = false;
        transform.SetPositionAndRotation(center, Quaternion.identity);
        InputDirection = Vector2.zero; Running = false; fallSpeed = 0;
        body.enabled = true;
    }
}
