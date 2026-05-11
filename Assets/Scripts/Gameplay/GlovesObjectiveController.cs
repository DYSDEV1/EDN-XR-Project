using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class GlovesObjectiveController : MonoBehaviour
    {
        public static GlovesObjectiveController Instance { get; private set; }

        private const string GlovesObjectName = "LatexGloves";

        [Header("Objective")]
        [SerializeField] private string objectiveMessage = "Prendre les gants";
        [SerializeField] private string completedMessage = "Regarder le clipboard de la recette uranium";

        [Header("Equip")]
        [SerializeField] private float interactDistance = 4f;
        [SerializeField] private float equipDuration = 0.75f;
        [SerializeField] private Vector3 cameraEquipOffset = new Vector3(0f, -0.35f, 0.25f);

        [Header("Audio")]
        [SerializeField] private string glovesClipName = "gloves";
        [SerializeField] private float glovesSoundMaxDuration = 4f;
        [SerializeField] private float glovesSoundVolume = 0.9f;

        [Header("Highlight")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.88f, 0.28f);
        [SerializeField] private float highlightPulseSpeed = 4f;
        [SerializeField] private float haloScale = 1.4f;

        private Transform glovesTransform;
        private Camera mainCamera;
        private GameObject highlightObject;
        private Renderer highlightRenderer;
        private Material highlightMaterial;
        private AudioSource audioSource;
        private AudioClip glovesClip;
        private bool objectiveActive;
        private bool isEquipped;
        private bool isEquipping;
        private bool leftPrimaryWasPressed;
        private bool rightPrimaryWasPressed;

        public static GlovesObjectiveController EnsureInScene()
        {
            if (Instance != null)
                return Instance;

            GameObject gloves = FindGlovesObject();

            if (gloves == null)
            {
                Debug.LogWarning("[GlovesObjective] Bootstrap failed: no object named 'LatexGloves' found in the loaded scene.");
                return null;
            }

            GlovesObjectiveController controller = gloves.GetComponent<GlovesObjectiveController>();

            if (controller == null)
            {
                controller = gloves.AddComponent<GlovesObjectiveController>();
                Debug.Log($"[GlovesObjective] Added controller to '{gloves.name}' at pos={gloves.transform.position}, path='{GetTransformPath(gloves.transform)}'.");
            }

            return controller;
        }

        public static bool TryActivateGlovesObjective()
        {
            GlovesObjectiveController controller = EnsureInScene();

            if (controller == null)
                return false;

            controller.ActivateObjective();
            return true;
        }

        private static GameObject FindGlovesObject()
        {
            GameObject found = GameObject.Find(GlovesObjectName);

            if (found != null)
                return found;

            GameObject[] objects = Object.FindObjectsOfType<GameObject>(true);

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null
                    && objects[i].name.IndexOf("LatexGloves", System.StringComparison.OrdinalIgnoreCase) >= 0)
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
            glovesTransform = transform;
            mainCamera = Camera.main;
            MakeDynamic();
            EnsureCollider();
            EnsureRigidbody();
            EnsureXRInteraction();
            EnsureAudio();
            Debug.Log($"[GlovesObjective] Awake ready on '{name}'. pos={glovesTransform.position}, path='{GetTransformPath(transform)}', clip={(glovesClip != null ? glovesClip.name : "missing")}.");
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
            if (!objectiveActive || isEquipped)
                return;

            UpdateHighlightPulse();
        }

        private void LateUpdate()
        {
            if (!objectiveActive || isEquipped || highlightObject == null)
                return;

            Bounds bounds = GetWorldBounds();
            highlightObject.transform.position = bounds.center;
            highlightObject.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.3f) * haloScale;

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
            if (isEquipped)
            {
                SetCompletedObjective();
                return;
            }

            objectiveActive = true;

            if (ObjectiveHud.Instance != null)
                ObjectiveHud.Instance.SetMessage(objectiveMessage);

            EnableHighlight();
            Debug.Log($"[GlovesObjective] Objective activated. pos={glovesTransform.position}.");
        }

        public void TryEquip()
        {
            Debug.Log($"[GlovesObjective] TryEquip called. objectiveActive={objectiveActive}, isEquipped={isEquipped}, isEquipping={isEquipping}.");

            if (!objectiveActive || isEquipped || isEquipping)
                return;

            StartCoroutine(EquipRoutine());
        }

        private IEnumerator EquipRoutine()
        {
            isEquipping = true;
            objectiveActive = false;
            DisableHighlight();
            SetCollidersEnabled(false);
            PlayerMovementLock.Lock("equip gloves");
            PlayGlovesSound();

            Vector3 startPosition = glovesTransform.position;
            Quaternion startRotation = glovesTransform.rotation;
            Vector3 startScale = glovesTransform.localScale;
            float elapsed = 0f;

            while (elapsed < equipDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / equipDuration));
                Transform target = GetEquipTarget();
                Vector3 targetPosition = target != null
                    ? target.TransformPoint(cameraEquipOffset)
                    : startPosition + Vector3.up * 0.4f;

                glovesTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
                glovesTransform.rotation = Quaternion.Slerp(startRotation, target != null ? target.rotation : startRotation, t);
                glovesTransform.localScale = Vector3.Lerp(startScale, startScale * 0.08f, t);
                yield return null;
            }

            isEquipped = true;
            isEquipping = false;
            SetCompletedObjective();
            Debug.Log("[GlovesObjective] Gloves equipped and consumed.");
            PlayerMovementLock.Unlock("equip gloves");
            Destroy(gameObject);
        }

        private Transform GetEquipTarget()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            return mainCamera != null ? mainCamera.transform : null;
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
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            Debug.Log($"[GlovesObjective] XR select entered by '{(args.interactorObject != null ? args.interactorObject.transform.name : "unknown")}'.");
            TryEquip();
        }

        private void OnMouseDown()
        {
            TryEquip();
        }

        private void MakeDynamic()
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

            Debug.Log($"[GlovesObjective] Made gloves hierarchy dynamic. staticObjectsChanged={staticCount}.");
        }

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>() != null)
                return;

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Bounds localBounds = GetLocalBounds();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }

        private void EnsureRigidbody()
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void EnsureAudio()
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = glovesSoundVolume;
            glovesClip = Resources.Load<AudioClip>(glovesClipName);

            if (glovesClip == null)
                Debug.LogWarning($"[GlovesObjective] Gloves sound not found. Expected Assets/Resources/{glovesClipName}.mp3.");
        }

        private void PlayGlovesSound()
        {
            if (glovesClip == null)
                return;

            GameObject soundObject = new GameObject("Gloves Equip Audio");
            soundObject.transform.position = transform.position;
            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.clip = glovesClip;
            source.volume = glovesSoundVolume;
            source.spatialBlend = 1f;
            source.loop = false;
            source.Play();
            Destroy(soundObject, glovesSoundMaxDuration);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = enabled;
            }
        }

        private Bounds GetLocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.25f);

            Bounds localBounds = ToLocalBounds(renderers[0].bounds);

            for (int i = 1; i < renderers.Length; i++)
                localBounds.Encapsulate(ToLocalBounds(renderers[i].bounds));

            return localBounds;
        }

        private Bounds ToLocalBounds(Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds localBounds = new Bounds(transform.InverseTransformPoint(min), Vector3.zero);

            localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            localBounds.Encapsulate(transform.InverseTransformPoint(max));
            return localBounds;
        }

        private Bounds GetWorldBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
                return new Bounds(transform.position, Vector3.one * 0.25f);

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private void EnableHighlight()
        {
            if (highlightObject != null)
                return;

            Bounds bounds = GetWorldBounds();
            highlightObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightObject.name = "Gloves Objective Highlight";
            highlightObject.transform.position = bounds.center;
            highlightObject.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.3f) * haloScale;

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
        }

        private void DisableHighlight()
        {
            if (highlightObject != null)
                Destroy(highlightObject);

            highlightObject = null;
            highlightRenderer = null;
        }

        private void UpdateHighlightPulse()
        {
            if (highlightRenderer == null)
                return;

            float pulse = 0.65f + Mathf.Sin(Time.time * highlightPulseSpeed) * 0.35f;
            Color markerColor = Color.Lerp(highlightColor * 0.75f, highlightColor * 1.7f, pulse);
            markerColor.a = Mathf.Lerp(0.35f, 0.78f, pulse);
            highlightRenderer.material.color = markerColor;
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
                return true;

            Bounds bounds = GetWorldBounds();
            float distance = Vector3.Distance(mainCamera.transform.position, bounds.center);

            if (distance <= interactDistance)
                return true;

            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
                return false;

            return hit.collider != null && hit.collider.GetComponentInParent<GlovesObjectiveController>() == this;
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

        private static string GetTransformPath(Transform target)
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
    }
}
