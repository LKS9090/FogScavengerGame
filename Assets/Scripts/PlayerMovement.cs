using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController controller;
    private float verticalSpeed;

    public Vector2 MoveInput { get; set; }
    public bool ReadKeyboard = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.minMoveDistance = 0f;
    }

    private void Update()
    {
        if (ReadKeyboard) MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        float horizontal = MoveInput.x;
        float vertical = MoveInput.y;

        Vector3 movement = new Vector3(horizontal, 0f, vertical);
        movement = Vector3.ClampMagnitude(movement, 1f);
        if (movement.sqrMagnitude > 0f)
        {
            transform.rotation = Quaternion.LookRotation(movement);
        }

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        verticalSpeed += Physics.gravity.y * Time.deltaTime;

        Vector3 velocity = movement * moveSpeed;
        velocity.y = verticalSpeed;

        controller.Move(velocity * Time.deltaTime);
    }

    public void ResetPose(Vector3 position, Quaternion rotation)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        verticalSpeed = 0f;
        MoveInput = Vector2.zero;
        controller.enabled = true;
    }
}
