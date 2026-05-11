using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class WorktableQuantitySlider : MonoBehaviour
    {
        private WorktableParticleSpawner spawner;
        private Camera cachedCamera;

        public void Configure(WorktableParticleSpawner owner)
        {
            spawner = owner;
            HookXRSelect();
        }

        private void HookXRSelect()
        {
            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            if (spawner == null)
                return;

            spawner.SetQuantityFromWorldPoint(args.interactorObject.transform.position);
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
    }
}
