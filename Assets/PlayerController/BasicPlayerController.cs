using System.Collections;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem;

public class BasicPlayerController : MonoBehaviour
{
    [SerializeField] UnityProgram animController;
    private Rigidbody rb;
    private CapsuleCollider collider;

    private float moveSpeed;
    private Vector3 mMovement;
    private InputAction mMovementAction;
    private InputAction mSprintAction;
    [SerializeField] private float walkSpeed;
    [SerializeField] private float sprintSpeed;
    [SerializeField] private float sprintStartUp;
    private bool sprinting;
    private Vector3 direction;

    [SerializeField] private float jumpHeight;
    [SerializeField] private float groundCheckHeight;
    private InputAction mJump;
    private bool jumping;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        collider = GetComponent<CapsuleCollider>();
        mMovementAction = InputSystem.actions.FindAction("Move");
        mSprintAction = InputSystem.actions.FindAction("Sprint");
        mJump = InputSystem.actions.FindAction("Jump");
        moveSpeed = walkSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        GetInputs();
        Debug.DrawRay(transform.position, -Vector3.up * groundCheckHeight, Color.red);
    }

    private void FixedUpdate()
    {
        if (mMovementAction.IsPressed())
        {
            direction = new Vector3(mMovement.x, 0, mMovement.y);
            direction.Normalize();
            direction *= moveSpeed;
            rb.linearVelocity = new Vector3(direction.x, rb.linearVelocity.y, direction.z);
            transform.forward = direction;
        }
    }

    IEnumerator moveLerp(float speedToLerp)
    {
        float t = 0;
        float moveSpeedStart = moveSpeed;
        while (t < 1)
        {
            moveSpeed = Mathf.Lerp(moveSpeedStart, speedToLerp, t);
            t = t + Time.deltaTime / sprintStartUp;
            yield return null;//Stops here until right after the next update loop, then continues
        }
    }

    void GetInputs()
    {
        mMovement = mMovementAction.ReadValue<Vector2>();
        animController._currentSpeed = Mathf.Abs(rb.linearVelocity.x * rb.linearVelocity.x) + Mathf.Abs(rb.linearVelocity.z * rb.linearVelocity.z);

        if(mJump.IsPressed() && IsGrounded() && !jumping)
        {
            if(animController.jumpAnimation())
            {
                StartCoroutine(jumpDelay());
                jumping = true;
            }
        }

        if(mSprintAction.WasPressedThisFrame() && !sprinting) //Transition lerp for runspeed blend
        {
            StartCoroutine(moveLerp(sprintSpeed));
            sprinting = true;
        }
        else if(mSprintAction.WasReleasedThisFrame() && sprinting) //Transition lerp for walkspeed blend
        {
            StartCoroutine(moveLerp(walkSpeed));
            sprinting = false;
        }
    }

    IEnumerator jumpDelay()
    {
        yield return new WaitForSeconds(0.75f); //Adjust hitbox for jumping
        rb.AddForce(new Vector3(0, jumpHeight, 0), ForceMode.Impulse);
        collider.height = 1f;
        collider.center = new Vector3(0, 1.75f, 0);

        yield return new WaitForSeconds(0.5f); //Adjust hitbox for landing
        collider.height = 2f;
        collider.center = new Vector3(0, 1, 0);

        yield return new WaitForSeconds(0.5f); //To finish the end portion of the animation
        jumping = false;
    }

    bool IsGrounded()
    {
        return Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.25f, transform.position.z), -Vector3.up, groundCheckHeight);
    }
}
