using System;
using System.Collections;
using System.Collections.Generic;
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
    private InputAction mInteract;
    private bool jumping;

    // Box interation
    [SerializeField] HoldController holdController;
    Holdable heldItem = null;

    // Look At Location
    public LookAtTarget lookAtTarget = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        mMovementAction = InputSystem.actions.FindAction("Move");
        mSprintAction = InputSystem.actions.FindAction("Sprint");
        mJump = InputSystem.actions.FindAction("Jump");
        mInteract = InputSystem.actions.FindAction("Interact");
        holdController = GetComponentInChildren<HoldController>();
        moveSpeed = walkSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        GetInputs();
        Debug.DrawRay(transform.position, -Vector3.up * groundCheckHeight, Color.red);

        // Update LookAt weight
        if (lookAtTarget != null)
        {
            animController._lookAtIKObj.transform.position = lookAtTarget.gameObject.transform.position;
            animController._lookWeight = 1 - Mathf.InverseLerp(lookAtTarget.minDist, lookAtTarget.maxDist, Vector3.Distance(transform.position, lookAtTarget.transform.position));
        }
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

        if (mInteract.WasPressedThisFrame())
            TryInteract();

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

    bool TryInteract()
    {
        //Debug.Log($"Interact pressed {holdController.holdableInCol.name}");

        if (holdController.holdableInCol)
            PickUpObject(holdController.holdableInCol);
        else
            PutDownObject();

        return true;
    }

    void PickUpObject(Holdable obj)
    {
        //Debug.Log($"Picking up {obj.name}");
        holdController.holdableInCol = null;

        heldItem = obj;
        heldItem.isHeld = true;

        // Lerp hand locators to obj / teleport them then lerp weight
        heldItem._pLocL = animController._grabIKObj;
        heldItem.transform.parent = this.transform;

        animController._grabWeight = 1;
    }

    private void PutDownObject()
    {
        Debug.Log($"Putting down {heldItem.name}");

        heldItem.isHeld = false;
        heldItem.transform.parent = null;
        heldItem._pLocL = null;
        heldItem = null;

        animController._grabWeight = 0;
    }

    public void TryLookAt(LookAtTarget newLookAtTarget)
    {
        if (lookAtTarget != null)
        {
            float distToOld = Vector3.Distance(transform.position, lookAtTarget.transform.position);
            float distToNew = Vector3.Distance(transform.position, newLookAtTarget.transform.position);

            if (distToNew < distToOld)
            {
                lookAtTarget = newLookAtTarget;
                animController._lookAtIKObj.transform.position = lookAtTarget.gameObject.transform.position;
            }

            return;
        }

        lookAtTarget = newLookAtTarget;
        animController._lookAtIKObj.transform.position = lookAtTarget.gameObject.transform.position;
    }

    internal void EndLookAt(LookAtTarget lookAtTarget)
    {
        if (this.lookAtTarget == lookAtTarget)
        {
            Debug.Log("Resetting Look At");
            lookAtTarget = null;
            animController._lookWeight = 0f;
            animController._lookAtIKObj.transform.localPosition = UnityEngine.Vector3.zero;
        }
    }
}
