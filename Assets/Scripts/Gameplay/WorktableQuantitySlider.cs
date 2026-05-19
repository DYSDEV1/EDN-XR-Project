using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class WorktableQuantitySlider : MonoBehaviour
    {
        private const float JoystickDeadzone = 0.18f;
        private const float JoystickNormalizedSpeed = 0.75f;

        private WorktableParticleSpawner spawner;
        private Camera cachedCamera;
        private XRSimpleInteractable interactable;
        private Transform lockedInteractor;
        private bool isVrLocked;
        private float lockStartQuantityNormalized;
        private float lockStartInteractorNormalized;
        private float joystickNormalizedOffset;

        public void Configure(WorktableParticleSpawner owner)
        {
            spawner = owner;
            HookXRSelect();
        }

        private void Update()
        {
            UpdateLockedVrSelection();
        }

        private void OnDisable()
        {
            EndLockedVrSelection();
        }

        private void HookXRSelect()
        {
            interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectExited.RemoveListener(OnDeselected);
            interactable.selectEntered.AddListener(OnSelected);
            interactable.selectExited.AddListener(OnDeselected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            if (spawner == null)
                return;

            lockedInteractor = args != null && args.interactorObject != null
                ? args.interactorObject.transform
                : null;
            isVrLocked = true;
            joystickNormalizedOffset = 0f;
            lockStartQuantityNormalized = spawner.GetQuantityNormalized();
            lockStartInteractorNormalized = lockedInteractor != null
                ? spawner.GetQuantityNormalizedFromWorldPoint(lockedInteractor.position)
                : lockStartQuantityNormalized;

            UpdateLockedVrSelection();
        }

        private void OnDeselected(SelectExitEventArgs args)
        {
            EndLockedVrSelection();
        }

        private void EndLockedVrSelection()
        {
            isVrLocked = false;
            lockedInteractor = null;
            joystickNormalizedOffset = 0f;
        }

        private void UpdateLockedVrSelection()
        {
            if (!isVrLocked || spawner == null)
                return;

            float normalized = lockStartQuantityNormalized;

            if (lockedInteractor != null)
            {
                float currentInteractorNormalized = spawner.GetQuantityNormalizedFromWorldPoint(lockedInteractor.position);
                normalized += currentInteractorNormalized - lockStartInteractorNormalized;
            }

            float joystickX = ReadJoystickX();
            if (Mathf.Abs(joystickX) > 0f)
                joystickNormalizedOffset += joystickX * JoystickNormalizedSpeed * Time.deltaTime;

            spawner.SetQuantityFromNormalized(normalized + joystickNormalizedOffset);
        }

        private void OnMouseDown()
        {
            UpdateQuantityFromMouse();
        }

        private void OnMouseDrag()
        {
            UpdateQuantityFromMouse();
        }

        private void OnMouseOver()
        {
            float wheel = GetMouseScroll();

            Scroll(wheel);
        }

        public void SetQuantityFromPoint(Vector3 worldPoint)
        {
            if (spawner != null)
                spawner.SetQuantityFromWorldPoint(worldPoint);
        }

        public void Scroll(float wheel)
        {
            if (Mathf.Abs(wheel) > 0.01f && spawner != null)
                spawner.ChangeQuantity(wheel > 0f ? 1 : -1);
        }

        private void UpdateQuantityFromMouse()
        {
            if (spawner == null)
                return;

            Camera camera = GetCamera();
            if (camera == null)
                return;

            Plane panelPlane = new Plane(transform.parent.up, transform.position);
            Ray ray = camera.ScreenPointToRay(GetMousePosition());

            if (panelPlane.Raycast(ray, out float enter))
                spawner.SetQuantityFromWorldPoint(ray.GetPoint(enter));
        }

        private Camera GetCamera()
        {
            if (cachedCamera == null)
                cachedCamera = Camera.main;

            return cachedCamera;
        }

        private Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private float GetMouseScroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        private float ReadJoystickX()
        {
            float left = ReadJoystickX(XRNode.LeftHand);
            float right = ReadJoystickX(XRNode.RightHand);
            float strongest = Mathf.Abs(left) >= Mathf.Abs(right) ? left : right;

            if (Mathf.Abs(strongest) <= JoystickDeadzone)
                return 0f;

            float remapped = Mathf.InverseLerp(JoystickDeadzone, 1f, Mathf.Abs(strongest));
            return Mathf.Sign(strongest) * remapped;
        }

        private float ReadJoystickX(XRNode node)
        {
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);

            if (!device.isValid)
                return 0f;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
                return axis.x;

            return 0f;
        }
    }
}
