using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class DoorObjectiveController : MonoBehaviour
    {
        public static DoorObjectiveController Instance { get; private set; }

        private const string DefaultDoorObjectName = "Door (1)";
        private const string FallbackDoorObjectName = "Door";

        [Header("Objective")]
        [SerializeField] private string objectiveMessage = "Ouvrir la porte";
        [SerializeField] private string completedMessage = "Creer de l'uranium";

        [Header("Opening")]
        [SerializeField] private float openAngle = -90f;
        [SerializeField] private float openDuration = 0.65f;
        [SerializeField] private float openDoorWorldX = -4.808f;
        [SerializeField] private bool hingeOnNegativeLocalX = true;
        [SerializeField] private float interactDistance = 4f;

        [Header("Audio")]
        [SerializeField] private string doorClipName = "door";
        [SerializeField] private float doorSoundMaxDuration = 2.5f;
        [SerializeField] private float doorSoundVolume = 0.9f;

        [Header("Highlight")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.88f, 0.28f);
        [SerializeField] private float highlightPulseSpeed = 4f;
        [SerializeField] private float haloScale = 1.25f;

        private readonly List<RendererState> rendererStates = new List<RendererState>();

        private Transform doorTransform;
        private Transform hingeTransform;
        private Quaternion closedHingeLocalRotation;
        private Quaternion openHingeLocalRotation;
        private bool objectiveActive;
        private bool isOpen;
        private bool isOpening;
        private GameObject highlightObject;
        private Renderer highlightRenderer;
        private Material highlightMaterial;
        private Camera mainCamera;
        private AudioSource audioSource;
        private AudioClip doorClip;
        private bool leftPrimaryWasPressed;
        private bool rightPrimaryWasPressed;

        public static DoorObjectiveController EnsureInScene()
        {
            if (Instance != null)
                return Instance;

            GameObject door = FindDoorObject(DefaultDoorObjectName) ?? FindDoorObject(FallbackDoorObjectName);

            if (door == null)
            {
                Debug.LogWarning("[DoorObjective] Bootstrap failed: no object named 'Door (1)' or 'Door' found in the loaded scene.");
                return null;
            }

            DoorObjectiveController controller = door.GetComponent<DoorObjectiveController>();

            if (controller == null)
            {
                controller = door.AddComponent<DoorObjectiveController>();
                Debug.Log($"[DoorObjective] Added controller to '{door.name}' at pos={door.transform.position}, rot={door.transform.eulerAngles}.");
            }
            else
            {
                Debug.Log($"[DoorObjective] Controller already exists on '{door.name}'.");
            }

            return controller;
        }

        public static bool TryActivateDoorObjective()
        {
            DoorObjectiveController controller = EnsureInScene();

            if (controller == null)
            {
                Debug.LogWarning("[DoorObjective] Cannot activate objective because no door controller exists.");
                return false;
            }

            controller.ActivateObjective();
            return true;
        }

        private static GameObject FindDoorObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            GameObject found = GameObject.Find(objectName);

            if (found != null)
                return found;

            GameObject[] objects = Object.FindObjectsOfType<GameObject>();

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null
                    && string.Equals(objects[i].name, objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return objects[i];
                }
            }

            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            doorTransform = transform;
            mainCamera = Camera.main;
            MakeDoorDynamic();
            SaveRendererState();
            EnsureStableDoorBody();
            EnsureCollider();
            EnsureXRInteraction();
            EnsureAudio();
            PrepareHinge();
            Debug.Log($"[DoorObjective] Awake ready on '{name}'. doorPos={doorTransform.position}, hinge='{(hingeTransform != null ? hingeTransform.name : "none")}', openAngle={openAngle}, targetX={openDoorWorldX}, clip={(doorClip != null ? doorClip.name : "missing")}.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (highlightMaterial != null)
                Destroy(highlightMaterial);
        }

        private void Update()
        {
            if (!objectiveActive || isOpen)
                return;

            UpdateHighlightPulse();
        }

        private void LateUpdate()
        {
            if (!objectiveActive || isOpen || highlightObject == null)
                return;

            UpdateHighlightPlacement();

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
            {
                Vector3 toMarker = highlightObject.transform.position - mainCamera.transform.position;

                if (toMarker.sqrMagnitude > 0.001f)
                    highlightObject.transform.rotation = Quaternion.LookRotation(toMarker.normalized, Vector3.up);
            }
        }

        public void ActivateObjective()
        {
            if (isOpen)
            {
                Debug.Log("[DoorObjective] ActivateObjective called, but door is already open. Setting completed objective.");
                SetCompletedObjective();
                return;
            }

            objectiveActive = true;
            Debug.Log($"[DoorObjective] Objective activated. Door='{name}', pos={doorTransform.position}, rot={doorTransform.eulerAngles}, playerCamera={(Camera.main != null ? Camera.main.name : "missing")}.");

            if (ObjectiveHud.Instance != null)
                ObjectiveHud.Instance.SetMessage(objectiveMessage);
            else
                Debug.LogWarning("[DoorObjective] ObjectiveHud.Instance is null while activating door objective.");

            EnableHighlight();
        }

        public void TryOpen()
        {
            Debug.Log($"[DoorObjective] TryOpen called. objectiveActive={objectiveActive}, isOpen={isOpen}, isOpening={isOpening}, doorPos={doorTransform.position}, doorRot={doorTransform.eulerAngles}.");

            if (!objectiveActive)
            {
                Debug.LogWarning("[DoorObjective] TryOpen ignored because the door objective is not active yet. Grab/read the clipboard first.");
                return;
            }

            if (isOpen || isOpening)
            {
                Debug.Log("[DoorObjective] TryOpen ignored because the door is already open or opening.");
                return;
            }

            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            isOpening = true;
            DisableHighlight();
            PrepareHinge();
            PlayDoorSound();

            float elapsed = 0f;
            Quaternion startRotation = hingeTransform.localRotation;
            float startDoorWorldX = doorTransform.position.x;
            Debug.Log($"[DoorObjective] Opening started. hingePos={hingeTransform.position}, startDoorX={startDoorWorldX}, targetDoorX={openDoorWorldX}, startHingeRot={startRotation.eulerAngles}, targetHingeRot={openHingeLocalRotation.eulerAngles}.");

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                t = Mathf.SmoothStep(0f, 1f, t);
                hingeTransform.localRotation = Quaternion.Slerp(startRotation, openHingeLocalRotation, t);
                AlignDoorWorldX(Mathf.Lerp(startDoorWorldX, openDoorWorldX, t));
                yield return null;
            }

            hingeTransform.localRotation = openHingeLocalRotation;
            AlignDoorWorldX(openDoorWorldX);
            isOpen = true;
            isOpening = false;
            objectiveActive = false;
            Debug.Log($"[DoorObjective] Opening completed. doorPos={doorTransform.position}, doorRot={doorTransform.eulerAngles}, hingePos={hingeTransform.position}, hingeRot={hingeTransform.eulerAngles}.");
            SetCompletedObjective();
            UraniumDeliveryObjectiveController.NotifyDoorOpened();
        }

        private void SetCompletedObjective()
        {
            if (ObjectiveHud.Instance != null)
                ObjectiveHud.Instance.SetMessage(completedMessage);
        }

        private void EnsureXRInteraction()
        {
            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
            {
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
                Debug.Log("[DoorObjective] Added XRSimpleInteractable to door.");
            }

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void MakeDoorDynamic()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            int staticCount = 0;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null)
                    continue;

                if (children[i].gameObject.isStatic)
                    staticCount++;

                children[i].gameObject.isStatic = false;
            }

            Debug.Log($"[DoorObjective] Made door hierarchy dynamic. staticObjectsChanged={staticCount}, renderers={GetComponentsInChildren<Renderer>(true).Length}, colliders={GetComponentsInChildren<Collider>(true).Length}, path='{GetTransformPath(transform)}'.");
        }

        private void EnsureStableDoorBody()
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
            {
                Debug.Log("[DoorObjective] No Rigidbody on door. Transform animation will be used directly.");
                return;
            }

            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.Log("[DoorObjective] Door Rigidbody set to kinematic/no gravity for animation.");
        }

        private void EnsureAudio()
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = doorSoundVolume;
            doorClip = Resources.Load<AudioClip>(doorClipName);

            if (doorClip == null)
                Debug.LogWarning($"[DoorObjective] Door sound not found. Expected Assets/Resources/{doorClipName}.mp3 imported as Resources.Load<AudioClip>(\"{doorClipName}\").");
            else
                Debug.Log($"[DoorObjective] Loaded door sound '{doorClip.name}', length={doorClip.length:F2}s. It will stop after {doorSoundMaxDuration:F1}s.");
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            Debug.Log($"[DoorObjective] XR select entered by '{(args.interactorObject != null ? args.interactorObject.transform.name : "unknown")}'.");
            TryOpen();
        }

        private void OnMouseDown()
        {
            Debug.Log("[DoorObjective] OnMouseDown received.");
            TryOpen();
        }

        private void PrepareHinge()
        {
            if (hingeTransform != null)
                return;

            Bounds localBounds = GetLocalBounds();
            float hingeX = hingeOnNegativeLocalX ? localBounds.min.x : localBounds.max.x;
            Vector3 hingeLocalPoint = new Vector3(hingeX, localBounds.center.y, localBounds.center.z);
            Vector3 hingeWorldPoint = doorTransform.TransformPoint(hingeLocalPoint);
            Transform originalParent = doorTransform.parent;

            GameObject hingeObject = new GameObject($"{doorTransform.name} Hinge");
            hingeTransform = hingeObject.transform;
            hingeTransform.SetParent(originalParent, false);
            hingeTransform.position = hingeWorldPoint;
            hingeTransform.rotation = doorTransform.rotation;
            hingeTransform.localScale = Vector3.one;

            doorTransform.SetParent(hingeTransform, true);
            closedHingeLocalRotation = hingeTransform.localRotation;
            openHingeLocalRotation = closedHingeLocalRotation * Quaternion.Euler(0f, openAngle, 0f);
            Debug.Log($"[DoorObjective] Hinge prepared. localBounds={localBounds}, hingeWorldPoint={hingeWorldPoint}, originalParent={(originalParent != null ? originalParent.name : "none")}, closedRot={closedHingeLocalRotation.eulerAngles}, openRot={openHingeLocalRotation.eulerAngles}.");
        }

        private Bounds GetLocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length > 0)
            {
                Bounds bounds = ToLocalBounds(renderers[0].bounds);

                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(ToLocalBounds(renderers[i].bounds));

                return bounds;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            if (colliders.Length > 0)
            {
                Bounds bounds = ToLocalBounds(colliders[0].bounds);

                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(ToLocalBounds(colliders[i].bounds));

                return bounds;
            }

            return new Bounds(Vector3.zero, new Vector3(0.9f, 2f, 0.08f));
        }

        private Bounds ToLocalBounds(Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds localBounds = new Bounds(doorTransform.InverseTransformPoint(min), Vector3.zero);

            localBounds.Encapsulate(doorTransform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            localBounds.Encapsulate(doorTransform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            localBounds.Encapsulate(doorTransform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            localBounds.Encapsulate(doorTransform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            localBounds.Encapsulate(doorTransform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            localBounds.Encapsulate(doorTransform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            localBounds.Encapsulate(doorTransform.InverseTransformPoint(max));
            return localBounds;
        }

        private Bounds GetWorldBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;

                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                return bounds;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;

                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);

                return bounds;
            }

            return new Bounds(transform.position, new Vector3(0.9f, 2f, 0.08f));
        }

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>() != null)
            {
                Debug.Log($"[DoorObjective] Door already has collider(s): {GetComponentsInChildren<Collider>().Length}.");
                return;
            }

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Bounds localBounds = GetLocalBounds();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
            Debug.Log($"[DoorObjective] Added BoxCollider. center={collider.center}, size={collider.size}.");
        }

        private void SaveRendererState()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Material[] materials = renderers[i].materials;

                for (int j = 0; j < materials.Length; j++)
                {
                    if (materials[j] != null)
                        rendererStates.Add(new RendererState(materials[j]));
                }
            }
        }

        private void EnableHighlight()
        {
            if (highlightObject != null)
                return;

            Bounds bounds = GetWorldBounds();
            Debug.Log($"[DoorObjective] Enabling highlight. worldBounds center={bounds.center}, size={bounds.size}.");
            highlightObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightObject.name = "Door Objective Highlight";
            highlightObject.transform.position = bounds.center;
            highlightObject.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.5f) * haloScale;

            Collider markerCollider = highlightObject.GetComponent<Collider>();
            if (markerCollider != null)
                Destroy(markerCollider);

            highlightRenderer = highlightObject.GetComponent<Renderer>();
            Shader shader = Shader.Find("Unlit/Transparent");
            highlightMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            highlightMaterial.mainTexture = BuildHaloTexture();
            highlightMaterial.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.65f);
            highlightMaterial.renderQueue = 3000;

            if (highlightRenderer != null)
                highlightRenderer.material = highlightMaterial;

            ApplyHighlightEmission(1f);
        }

        private void DisableHighlight()
        {
            for (int i = 0; i < rendererStates.Count; i++)
                rendererStates[i].Restore();

            if (highlightObject != null)
                Destroy(highlightObject);

            highlightObject = null;
            highlightRenderer = null;
            Debug.Log("[DoorObjective] Highlight disabled.");
        }

        private void UpdateHighlightPlacement()
        {
            Bounds bounds = GetWorldBounds();
            highlightObject.transform.position = bounds.center;
            highlightObject.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.5f) * haloScale;
        }

        private void UpdateHighlightPulse()
        {
            if (highlightRenderer == null)
                return;

            float pulse = 0.65f + Mathf.Sin(Time.time * highlightPulseSpeed) * 0.35f;
            Color markerColor = Color.Lerp(highlightColor * 0.75f, highlightColor * 1.7f, pulse);
            markerColor.a = Mathf.Lerp(0.35f, 0.78f, pulse);
            highlightRenderer.material.color = markerColor;
            ApplyHighlightEmission(pulse);
        }

        private void ApplyHighlightEmission(float strength)
        {
            Color emission = highlightColor * Mathf.Lerp(0.7f, 1.8f, strength);

            for (int i = 0; i < rendererStates.Count; i++)
                rendererStates[i].ApplyEmission(emission);
        }

        private Texture2D BuildHaloTexture()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.62f) / 0.16f);
                    float glow = Mathf.Clamp01(1f - distance);
                    float alpha = Mathf.Max(ring * 0.85f, glow * 0.22f);
                    alpha *= Mathf.SmoothStep(1f, 0f, distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private bool CanInteractFromPlayer()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning("[DoorObjective] CanInteractFromPlayer: no main camera, allowing interaction.");
                return true;
            }

            Bounds bounds = GetWorldBounds();
            float distance = Vector3.Distance(mainCamera.transform.position, bounds.center);

            if (distance <= interactDistance)
            {
                Debug.Log($"[DoorObjective] Interaction allowed by distance. distance={distance:F2}, max={interactDistance:F2}.");
                return true;
            }

            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
            {
                Debug.LogWarning($"[DoorObjective] Interaction denied: camera is too far ({distance:F2}) and raycast hit nothing within {interactDistance:F2}m.");
                return false;
            }

            bool hitThisDoor = hit.collider != null && hit.collider.GetComponentInParent<DoorObjectiveController>() == this;
            Debug.Log($"[DoorObjective] Interaction raycast hit '{(hit.collider != null ? hit.collider.name : "none")}', hitThisDoor={hitThisDoor}.");
            return hitThisDoor;
        }

        private bool InteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetKeyDown(KeyCode.JoystickButton14)
                || Input.GetKeyDown(KeyCode.JoystickButton15))
            {
                return true;
            }
#endif

            return XRPrimaryPressedThisFrame(UnityEngine.XR.XRNode.LeftHand, ref leftPrimaryWasPressed)
                || XRPrimaryPressedThisFrame(UnityEngine.XR.XRNode.RightHand, ref rightPrimaryWasPressed);
        }

        private void PlayDoorSound()
        {
            if (audioSource == null)
                EnsureAudio();

            if (audioSource == null || doorClip == null)
                return;

            audioSource.clip = doorClip;
            audioSource.volume = doorSoundVolume;
            audioSource.Play();
            Debug.Log($"[DoorObjective] Door sound started and will stop after {doorSoundMaxDuration:F1}s.");
            StartCoroutine(StopDoorSoundAfterDelay());
        }

        private IEnumerator StopDoorSoundAfterDelay()
        {
            yield return new WaitForSeconds(doorSoundMaxDuration);

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                Debug.Log("[DoorObjective] Door sound stopped after delay.");
            }
        }

        private bool XRPrimaryPressedThisFrame(UnityEngine.XR.XRNode node, ref bool wasPressed)
        {
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);

            if (!device.isValid)
            {
                wasPressed = false;
                return false;
            }

            bool pressed;

            if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out pressed))
                pressed = false;

            bool pressedThisFrame = pressed && !wasPressed;
            wasPressed = pressed;
            return pressedThisFrame;
        }

        private string GetTransformPath(Transform target)
        {
            if (target == null)
                return "null";

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private void AlignDoorWorldX(float targetX)
        {
            if (doorTransform == null || hingeTransform == null)
                return;

            Vector3 hingePosition = hingeTransform.position;
            hingePosition.x += targetX - doorTransform.position.x;
            hingeTransform.position = hingePosition;
        }

        private Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private struct RendererState
        {
            private readonly Material material;
            private readonly Color emissionColor;
            private readonly bool hadEmissionKeyword;

            public RendererState(Material material)
            {
                this.material = material;
                emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                hadEmissionKeyword = material.IsKeywordEnabled("_EMISSION");
            }

            public void ApplyEmission(Color color)
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    return;

                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }

            public void Restore()
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    return;

                material.SetColor("_EmissionColor", emissionColor);

                if (!hadEmissionKeyword)
                    material.DisableKeyword("_EMISSION");
            }
        }
    }
}
