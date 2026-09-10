using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController controller;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

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
}