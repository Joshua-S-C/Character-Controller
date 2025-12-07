using System.Collections;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem;

public class BasicPlayerController : MonoBehaviour
{
    [SerializeField] UnityProgram animController;
    private Rigidbody rb;
    private CapsuleCollider col;

    private float moveSpeed;
    private Vector3 mMovement;
    private InputAction mMovementAction;
    private InputAction mSprintAction;
    [SerializeField] private float walkSpeed;
    [SerializeField] private float sprintSpeed;
    [SerializeField] private float sprintStartUp;
    [SerializeField] private float turnStrength;
    private bool sprinting;
    private Vector3 direction;
    private Vector3 move;

    [SerializeField] private float jumpHeight;
    [SerializeField] private float groundCheckHeight;
    private InputAction mJump;
    private bool jumping;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
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
            move = transform.TransformDirection(direction);
            move.Normalize();
            move *= moveSpeed;
            rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

            if(mMovement.y != -1)
            {
                if (direction.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(move);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnStrength); // Smooth rotation
                    animController._turnAmount = mMovement.x;
                }
            }
        }
    }

    IEnumerator moveLerp(float speedToLerp)
    {
        float time = 0;
        float moveSpeedStart = moveSpeed;
        while (time < 1)
        {
            moveSpeed = Mathf.Lerp(moveSpeedStart, speedToLerp, time);
            time = time + Time.deltaTime / sprintStartUp;
            yield return null; 
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
            sprinting = true;
            StartCoroutine(moveLerp(sprintSpeed));
        }
        else if(!mSprintAction.IsPressed() && sprinting) //Transition lerp for walkspeed blend
        {
            sprinting = false;
            StartCoroutine(moveLerp(walkSpeed));
        }
    }

    IEnumerator jumpDelay()
    {
        yield return new WaitForSeconds(0.75f); //Adjust hitbox for jumping
        rb.AddForce(new Vector3(0, jumpHeight, 0), ForceMode.Impulse);
        col.height = 1f;
        col.center = new Vector3(0, 1.75f, 0);

        yield return new WaitForSeconds(0.5f); //Adjust hitbox for landing
        col.height = 2f;
        col.center = new Vector3(0, 1, 0);

        yield return new WaitForSeconds(0.5f); //To finish the end portion of the animation
        jumping = false;
    }

    bool IsGrounded()
    {
        return Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.25f, transform.position.z), -Vector3.up, groundCheckHeight);
    }
}
