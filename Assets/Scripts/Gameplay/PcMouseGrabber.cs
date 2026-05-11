using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class PcMouseGrabber : MonoBehaviour
    {
        [SerializeField] private float grabDistance = 5f;
        [SerializeField] private float holdDistance = 1.1f;
        [SerializeField] private float minHoldDistance = 0.35f;
        [SerializeField] private float maxHoldDistance = 4f;
        [SerializeField] private float scrollSpeed = 0.18f;
        [SerializeField] private float followSpeed = 18f;
        [SerializeField] private float uiInteractionDistance = 8f;

        private Camera playerCamera;
        private Rigidbody grabbedBody;
        private PcGrabbableObject grabbedGrabbable;
        private bool previousUseGravity;
        private bool previousIsKinematic;
        private static Texture2D crosshairTexture;

        private void Awake()
        {
            playerCamera = GetComponent<Camera>();
            grabDistance = Mathf.Max(grabDistance, 8f);
        }

        private void Update()
        {
            if (XRSettings.isDeviceActive)
            {
                Release();
                enabled = false;
                return;
            }

            float scroll = GetMouseScroll();

            if (InteractKeyWasPressed())
            {
                TryPressAimedInteract();
            }

            if (MouseWasPressed())
            {
                if (!TryPressAimedControl())
                    TryGrab();
            }

            if (MouseWasReleased())
                Release();

            if (grabbedBody == null)
            {
                TryScrollAimedSlider(scroll);
                return;
            }

            holdDistance = Mathf.Clamp(
                holdDistance + scroll * scrollSpeed,
                minHoldDistance,
                maxHoldDistance);
        }

        private void FixedUpdate()
        {
            if (grabbedBody == null || playerCamera == null)
                return;

            Vector3 targetPosition = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
            Vector3 nextPosition = Vector3.Lerp(grabbedBody.position, targetPosition, followSpeed * Time.fixedDeltaTime);
            grabbedBody.MovePosition(nextPosition);
        }

        private void TryGrab()
        {
            if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            RaycastHit[] hits = Physics.RaycastAll(ray, grabDistance, ~0, QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return;

            RaycastHit bestHit = default;
            Rigidbody bestBody = null;
            PcGrabbableObject bestGrabbable = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null)
                    continue;

                if (hits[i].collider.GetComponentInParent<WorktableParticleButton>() != null
                    || hits[i].collider.GetComponentInParent<WorktableQuantitySlider>() != null)
                {
                    continue;
                }

                if (!TryResolveGrabHit(hits[i], out Rigidbody body, out IngredientBall ingredient, out PcGrabbableObject grabbable))
                    continue;

                if (ingredient != null && ingredient.IsConsumed)
                    continue;

                if (hits[i].distance >= bestDistance)
                    continue;

                bestHit = hits[i];
                bestBody = body;
                bestGrabbable = grabbable;
                bestDistance = hits[i].distance;
            }

            if (bestBody == null)
                return;

            grabbedBody = bestBody;
            grabbedGrabbable = bestGrabbable;
            previousUseGravity = grabbedBody.useGravity;
            previousIsKinematic = grabbedBody.isKinematic;
            holdDistance = Mathf.Clamp(bestHit.distance, minHoldDistance, maxHoldDistance);

            grabbedBody.velocity = Vector3.zero;
            grabbedBody.angularVelocity = Vector3.zero;
            grabbedBody.useGravity = false;
            grabbedBody.isKinematic = true;

            Debug.Log($"[PcMouseGrabber] Grabbed '{grabbedBody.name}' via collider '{bestHit.collider.name}' distance={bestHit.distance:F2}.");

            if (bestGrabbable != null)
                bestGrabbable.NotifyPcGrabbed();
        }

        private bool TryResolveGrabHit(RaycastHit hit, out Rigidbody body, out IngredientBall ingredient, out PcGrabbableObject grabbable)
        {
            body = hit.rigidbody;
            ingredient = hit.collider != null ? hit.collider.GetComponentInParent<IngredientBall>() : null;
            grabbable = hit.collider != null ? hit.collider.GetComponentInParent<PcGrabbableObject>() : null;

            if (body == null && grabbable != null)
                body = grabbable.GetComponent<Rigidbody>() != null ? grabbable.GetComponent<Rigidbody>() : grabbable.GetComponentInParent<Rigidbody>();

            if (body == null && ingredient != null)
                body = ingredient.GetComponent<Rigidbody>() != null ? ingredient.GetComponent<Rigidbody>() : ingredient.GetComponentInParent<Rigidbody>();

            return body != null && (ingredient != null || grabbable != null);
        }

        private void Release()
        {
            if (grabbedBody == null)
                return;

            grabbedBody.useGravity = previousUseGravity;
            grabbedBody.isKinematic = previousIsKinematic;

            if (grabbedGrabbable != null)
                grabbedGrabbable.NotifyPcReleased();

            grabbedBody = null;
            grabbedGrabbable = null;
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnGUI()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (crosshairTexture == null)
                crosshairTexture = Texture2D.whiteTexture;

            const float outerSize = 8f;
            const float innerSize = 4f;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(centerX - outerSize * 0.5f, centerY - outerSize * 0.5f, outerSize, outerSize), crosshairTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(centerX - innerSize * 0.5f, centerY - innerSize * 0.5f, innerSize, innerSize), crosshairTexture);
            GUI.color = Color.white;
        }

        private bool TryPressAimedControl()
        {
            if (!TryRaycastFromAim(uiInteractionDistance, out RaycastHit hit))
                return false;

            WorktableQuantitySlider slider = hit.collider.GetComponentInParent<WorktableQuantitySlider>();
            if (slider != null)
            {
                slider.SetQuantityFromPoint(hit.point);
                return true;
            }

            WorktableParticleButton button = hit.collider.GetComponentInParent<WorktableParticleButton>();
            if (button != null)
            {
                button.Press();
                return true;
            }

            return false;
        }

        private bool TryPressAimedInteract()
        {
            if (!TryRaycastFromAim(uiInteractionDistance, out RaycastHit hit))
            {
                Debug.Log("[PcMouseGrabber] E interact raycast hit nothing.");
                return false;
            }

            Debug.Log($"[PcMouseGrabber] E interact raycast hit '{hit.collider.name}' (root: '{hit.collider.transform.root.name}').");

            MemoryMiniGameCell memoryCell = hit.collider.GetComponentInParent<MemoryMiniGameCell>();
            if (memoryCell != null)
            {
                memoryCell.Select();
                return true;
            }

            BucketAssembler bucket = hit.collider.GetComponentInParent<BucketAssembler>();
            if (bucket != null)
            {
                bucket.TryMixCurrentContents();
                return true;
            }

            LightSwitchController lightSwitch = hit.collider.GetComponentInParent<LightSwitchController>();
            if (lightSwitch != null)
            {
                lightSwitch.TryTurnOn();
                return true;
            }

            PhoneObjectiveController phone = hit.collider.GetComponentInParent<PhoneObjectiveController>();
            if (phone != null)
            {
                phone.TryAnswer();
                return true;
            }

            DoorObjectiveController door = hit.collider.GetComponentInParent<DoorObjectiveController>();
            if (door != null)
            {
                door.TryOpen();
                return true;
            }

            GlovesObjectiveController gloves = hit.collider.GetComponentInParent<GlovesObjectiveController>();
            if (gloves != null)
            {
                gloves.TryEquip();
                return true;
            }

            Debug.Log("[PcMouseGrabber] E interact hit has no BucketAssembler, LightSwitchController, PhoneObjectiveController, DoorObjectiveController, or GlovesObjectiveController in parents.");
            return false;
        }

        private void TryScrollAimedSlider(float scroll)
        {
            if (Mathf.Abs(scroll) <= 0.01f)
                return;

            if (!TryRaycastFromAim(uiInteractionDistance, out RaycastHit hit))
                return;

            WorktableQuantitySlider slider = hit.collider.GetComponentInParent<WorktableQuantitySlider>();
            if (slider != null)
                slider.Scroll(scroll);
        }

        private bool TryRaycastFromAim(float distance, out RaycastHit hit)
        {
            hit = default;

            if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked)
                return false;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            return Physics.Raycast(ray, out hit, distance, ~0, QueryTriggerInteraction.Collide);
        }

        private bool MouseWasPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private bool MouseWasReleased()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(0);
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

        private bool InteractKeyWasPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
    }
}
