using UnityEngine;
using UnityEngine.InputSystem;

public class FlyCam : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float fastMultiplier = 2f;

    [Header("Mouse Look")]
    public float lookSensitivity = 2f;

    private Vector2 _lookDelta;
    private bool _isRightClickHeld = false;

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb == null || mouse == null)
            return;

        // ----- Right Click Look -----
        _isRightClickHeld = mouse.rightButton.isPressed;

        if (_isRightClickHeld)
        {
            _lookDelta = mouse.delta.ReadValue() * lookSensitivity * Time.deltaTime;

            transform.rotation = Quaternion.Euler(
                transform.eulerAngles.x - _lookDelta.y,
                transform.eulerAngles.y + _lookDelta.x,
                0f
            );
        }

        // ----- Movement -----
        Vector3 dir = Vector3.zero;

        if (kb.wKey.isPressed) dir += transform.forward;
        if (kb.sKey.isPressed) dir -= transform.forward;
        if (kb.aKey.isPressed) dir -= transform.right;
        if (kb.dKey.isPressed) dir += transform.right;

        // Vertical movement (Q = down, E = up)
        if (kb.eKey.isPressed) dir += Vector3.up;
        if (kb.qKey.isPressed) dir -= Vector3.up;

        float speed = moveSpeed;
        if (kb.leftShiftKey.isPressed)
            speed *= fastMultiplier;

        transform.position += dir * speed * Time.deltaTime;
    }
}