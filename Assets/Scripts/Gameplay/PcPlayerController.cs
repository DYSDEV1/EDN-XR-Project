using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class PcPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float sprintMultiplier = 2f;
        [SerializeField] private float mouseSensitivity = 0.08f;
#if !ENABLE_INPUT_SYSTEM
        [SerializeField] private KeyCode unlockCursorKey = KeyCode.Escape;
#endif

        private float yaw;
        private float pitch;
        private float lockedHeight;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = NormalizeAngle(euler.x);
            ResetLockedHeight();
            LockCursor(true);
        }

        private void OnEnable()
        {
            ResetLockedHeight();
        }

        private void Update()
        {
            if (XRSettings.isDeviceActive)
            {
                LockCursor(false);
                enabled = false;
                return;
            }

            if (UnlockWasPressed())
                LockCursor(Cursor.lockState != CursorLockMode.Locked);

            if (Cursor.lockState == CursorLockMode.Locked)
                Look();

            Move();
        }

        private void Look()
        {
            Vector2 mouseDelta = GetMouseDelta();
            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void Move()
        {
            Vector3 move = Vector3.zero;
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            if (IsHeld(KeyCode.W) || IsHeld(KeyCode.UpArrow))
                move += flatForward;
            if (IsHeld(KeyCode.S) || IsHeld(KeyCode.DownArrow))
                move -= flatForward;
            if (IsHeld(KeyCode.D) || IsHeld(KeyCode.RightArrow))
                move += flatRight;
            if (IsHeld(KeyCode.A) || IsHeld(KeyCode.LeftArrow))
                move -= flatRight;

            if (move.sqrMagnitude < 0.0001f)
                return;

            float speed = IsHeld(KeyCode.LeftShift) ? moveSpeed * sprintMultiplier : moveSpeed;
            Vector3 nextPosition = transform.position + move.normalized * speed * Time.deltaTime;
            nextPosition.y = lockedHeight;
            transform.position = nextPosition;
        }

        private bool UnlockWasPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(unlockCursorKey);
#endif
        }

        private Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }

        private bool IsHeld(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
                return false;

            switch (keyCode)
            {
                case KeyCode.W: return Keyboard.current.wKey.isPressed;
                case KeyCode.A: return Keyboard.current.aKey.isPressed;
                case KeyCode.S: return Keyboard.current.sKey.isPressed;
                case KeyCode.D: return Keyboard.current.dKey.isPressed;
                case KeyCode.LeftShift: return Keyboard.current.leftShiftKey.isPressed;
                case KeyCode.UpArrow: return Keyboard.current.upArrowKey.isPressed;
                case KeyCode.DownArrow: return Keyboard.current.downArrowKey.isPressed;
                case KeyCode.LeftArrow: return Keyboard.current.leftArrowKey.isPressed;
                case KeyCode.RightArrow: return Keyboard.current.rightArrowKey.isPressed;
                default: return false;
            }
#else
            return Input.GetKey(keyCode);
#endif
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void ResetLockedHeight()
        {
            lockedHeight = transform.position.y;
        }

        private float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
