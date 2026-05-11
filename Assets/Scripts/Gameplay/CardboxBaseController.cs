using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class CardboxBaseController : MonoBehaviour
    {
        [SerializeField] private Vector3 storageTriggerPadding = new Vector3(0.12f, 0.18f, 0.12f);
        [SerializeField] private Vector3 grabColliderPadding = new Vector3(0.22f, 0.18f, 0.22f);
        [SerializeField] private Vector3 physicsColliderInset = new Vector3(0.035f, 0f, 0.035f);
        [SerializeField] private float labelVerticalOffset = 0.35f;
        [SerializeField] private float scanInterval = 0.12f;
        [SerializeField] private bool moveToVisibleDebugSpot = false;

        private BoxCollider physicsCollider;
        private BoxCollider grabCollider;
        private BoxCollider storageTrigger;
        private TMP_Text label;
        private int uraniumCount;
        private float nextScanTime;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float nextDebugTime;
        private int debugFramesRemaining = 12;

        public bool HasUranium => uraniumCount > 0;

        private void Awake()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            MakeDynamic();
            RemoveRuntimeVisibleBoxIfPresent();
            RemoveRuntimeDynamicVisualModelIfPresent();
            MoveToVisibleDebugSpotIfNeeded();
            EnsureCollider();
            EnsureGrabCollider();
            DisableMeshCollidersForPhysics();
            EnsureRigidbody();
            EnsureStorageTrigger();
            EnsureGrab();
            LogState("Awake ready");
        }

        private void MoveToVisibleDebugSpotIfNeeded()
        {
            if (!moveToVisibleDebugSpot)
                return;

            Transform anchor = FindVisibleAnchor();

            if (anchor == null)
                return;

            int index = ExtractIndexFromName(name);
            Vector3 offset = new Vector3(-0.55f + index * 0.42f, 0.35f, 0.45f);
            transform.position = anchor.position + offset;
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            startPosition = transform.position;
            startRotation = transform.rotation;
            Debug.Log($"[CardboxBase] Moved {name} to visible debug spot at {transform.position} near {anchor.name}");
        }

        private void RemoveRuntimeVisibleBoxIfPresent()
        {
            Transform existing = transform.Find("Runtime Visible Cardbox");

            if (existing == null)
                return;

            Destroy(existing.gameObject);
            Debug.Log($"[CardboxBase] Removed runtime debug cube from {name}; using real CardboxBase model.");
        }

        private void RemoveRuntimeDynamicVisualModelIfPresent()
        {
            Transform existing = transform.Find("Runtime Dynamic Cardbox Model");

            if (existing == null)
                return;

            RestoreOriginalRenderers(existing);
            existing.gameObject.SetActive(false);
            Destroy(existing.gameObject);
            Debug.Log($"[CardboxBase] Removed runtime dynamic visual copy from {name}; restored original model renderer.");
        }

        private void RestoreOriginalRenderers(Transform runtimeVisualRoot)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                    continue;

                if (runtimeVisualRoot != null && renderer.transform.IsChildOf(runtimeVisualRoot))
                    continue;

                if (renderer.GetComponentInParent<TMP_Text>() != null)
                    continue;

                renderer.enabled = true;
            }
        }

        private void Update()
        {
            RecoverIfFallen();
            DebugPositionDuringStartup();

            if (Time.time < nextScanTime)
                return;

            nextScanTime = Time.time + scanInterval;
            ScanForUranium();
        }

        private void LateUpdate()
        {
            if (label == null || Camera.main == null)
                return;

            label.transform.position = transform.position + Vector3.up * labelVerticalOffset;
            Vector3 directionToCamera = label.transform.position - Camera.main.transform.position;

            if (directionToCamera.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
        }

        private void OnDestroy()
        {
            LogState("OnDestroy");

            if (label != null)
                Destroy(label.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryConsumeUranium(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryConsumeUranium(other);
        }

        private void ScanForUranium()
        {
            if (storageTrigger == null)
                return;

            Bounds bounds = storageTrigger.bounds;
            Collider[] colliders = Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                storageTrigger.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < colliders.Length; i++)
                TryConsumeUranium(colliders[i]);
        }

        private void TryConsumeUranium(Collider other)
        {
            if (other == null || uraniumCount > 0)
                return;

            IngredientBall ingredient = ResolveIngredient(other);

            if (ingredient == null || ingredient.IsConsumed || ingredient.Type != IngredientType.Uranium)
                return;

            uraniumCount = 1;
            ingredient.Consume();
            UpdateLabel();
            UraniumDeliveryObjectiveController.NotifyUraniumStored(this);
            Debug.Log($"[CardboxBase] {name} consumed uranium. total={uraniumCount}");
        }

        public void ConsumeForDelivery()
        {
            uraniumCount = 0;
            UpdateLabel();
            gameObject.SetActive(false);
            Debug.Log($"[CardboxBase] {name} consumed by delivery objective.");
        }

        private IngredientBall ResolveIngredient(Collider other)
        {
            IngredientBall ingredient = other.GetComponentInParent<IngredientBall>();

            if (ingredient != null)
                return ingredient;

            if (other.attachedRigidbody != null)
                ingredient = other.attachedRigidbody.GetComponentInParent<IngredientBall>();

            return ingredient;
        }

        private void UpdateLabel()
        {
            if (uraniumCount <= 0)
            {
                if (label != null)
                    Destroy(label.gameObject);

                label = null;
                return;
            }

            if (label == null)
                label = CreateLabel();

            label.text = $"Uranium x{uraniumCount}";
        }

        private TMP_Text CreateLabel()
        {
            GameObject labelObject = new GameObject("Cardbox Uranium Label");
            labelObject.transform.position = transform.position + Vector3.up * labelVerticalOffset;
            TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
            text.fontSize = 0.18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = new Vector2(2f, 0.35f);
            return text;
        }

        private void EnsureGrab()
        {
            XRGrabInteractable grab = GetComponent<XRGrabInteractable>();

            if (grab == null)
                grab = gameObject.AddComponent<XRGrabInteractable>();

            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.selectEntered.RemoveListener(OnXrGrabbed);
            grab.selectExited.RemoveListener(OnXrReleased);
            grab.selectEntered.AddListener(OnXrGrabbed);
            grab.selectExited.AddListener(OnXrReleased);
            ConfigureGrabColliders(grab);

            if (GetComponent<PcGrabbableObject>() == null)
                gameObject.AddComponent<PcGrabbableObject>();
        }

        private void ConfigureGrabColliders(XRGrabInteractable grab)
        {
            if (grab == null)
                return;

            grab.colliders.Clear();

            if (grabCollider != null && grabCollider.enabled)
                grab.colliders.Add(grabCollider);

            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].enabled || colliders[i].isTrigger || colliders[i] is MeshCollider)
                    continue;

                if (colliders[i] == grabCollider)
                    continue;

                grab.colliders.Add(colliders[i]);
            }

            Debug.Log($"[CardboxBase] {name} grab configured. xrGrabColliders={grab.colliders.Count}, grabColliderBounds={(grabCollider != null ? grabCollider.bounds.ToString() : "none")}");
        }

        private void EnsureRigidbody()
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.mass = 0.65f;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void OnXrGrabbed(SelectEnterEventArgs args)
        {
            ApplyHeldPhysics(true);
            Debug.Log($"[CardboxBase] XR grabbed {name}");
        }

        private void OnXrReleased(SelectExitEventArgs args)
        {
            ApplyReleasedPhysics();
            Debug.Log($"[CardboxBase] XR released {name}; gravity enabled.");
        }

        private void OnPcGrabbed()
        {
            ApplyHeldPhysics(true);
            Debug.Log($"[CardboxBase] PC grabbed {name}");
        }

        private void OnPcReleased()
        {
            ApplyReleasedPhysics();
            Debug.Log($"[CardboxBase] PC released {name}; gravity enabled.");
        }

        private void ApplyHeldPhysics(bool kinematic)
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
                return;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = kinematic;
            rb.WakeUp();
        }

        private void ApplyReleasedPhysics()
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
                return;

            rb.useGravity = true;
            rb.isKinematic = false;
            rb.WakeUp();
        }

        private void RecoverIfFallen()
        {
            if (transform.position.y > startPosition.y - 2f)
                return;

            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            transform.position = startPosition;
            transform.rotation = startRotation;
            Debug.LogWarning($"[CardboxBase] {name} fell below the scene and was restored to its start position.");
        }

        private void DebugPositionDuringStartup()
        {
            if (debugFramesRemaining <= 0 || Time.time < nextDebugTime)
                return;

            nextDebugTime = Time.time + 0.5f;
            debugFramesRemaining--;
            LogState($"Startup debug {12 - debugFramesRemaining}/12");
        }

        private void LogState(string reason)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            XRGrabInteractable grab = GetComponent<XRGrabInteractable>();

            int enabledColliders = 0;
            int triggerColliders = 0;
            int enabledRenderers = 0;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                if (colliders[i].enabled)
                    enabledColliders++;

                if (colliders[i].isTrigger)
                    triggerColliders++;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                    enabledRenderers++;
            }

            Debug.Log(
                $"[CardboxBase] {reason} | name={name}, activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy}, " +
                $"pos={transform.position}, rot={transform.eulerAngles}, parent={(transform.parent != null ? transform.parent.name : "none")}, " +
                $"rb={(rb != null ? $"yes kin={rb.isKinematic} grav={rb.useGravity} vel={rb.velocity}" : "none")}, " +
                $"colliders={enabledColliders}/{colliders.Length} enabled, triggers={triggerColliders}, " +
                $"renderers={enabledRenderers}/{renderers.Length} enabled, " +
                $"grab={(grab != null ? $"yes colliders={grab.colliders.Count}" : "none")}");
        }

        private Transform FindVisibleAnchor()
        {
            Transform paintCan = FindBestAnchorByPrefix("PaintCan");

            if (paintCan != null)
                return paintCan;

            GameObject screwBox = GameObject.Find("ScrewBox");

            if (screwBox != null)
                return screwBox.transform;

            GameObject workTable = GameObject.Find("WorkTable");

            if (workTable != null)
                return workTable.transform;

            return null;
        }

        private Transform FindBestAnchorByPrefix(string prefix)
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            Camera camera = Camera.main;
            Transform best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null || string.IsNullOrWhiteSpace(transforms[i].name))
                    continue;

                if (!transforms[i].name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!transforms[i].gameObject.activeInHierarchy)
                    continue;

                float score = camera != null
                    ? Vector3.SqrMagnitude(transforms[i].position - camera.transform.position)
                    : i;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = transforms[i];
                }
            }

            return best;
        }

        private int ExtractIndexFromName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return 0;

            int open = objectName.IndexOf('(');
            int close = objectName.IndexOf(')');

            if (open < 0 || close <= open)
                return 0;

            string numberText = objectName.Substring(open + 1, close - open - 1);
            return int.TryParse(numberText, out int parsedIndex) ? Mathf.Clamp(parsedIndex, 0, 8) : 0;
        }

        private void EnsureCollider()
        {
            Collider[] colliders = GetComponents<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] is BoxCollider boxCollider && !boxCollider.isTrigger)
                {
                    physicsCollider = boxCollider;
                    break;
                }
            }

            if (physicsCollider == null)
                physicsCollider = gameObject.AddComponent<BoxCollider>();

            Bounds bounds = GetRendererLocalBounds();
            Vector3 size = ShrinkSize(Abs(bounds.size), physicsColliderInset);
            physicsCollider.center = bounds.center;
            physicsCollider.size = new Vector3(
                Mathf.Max(size.x, 0.18f),
                Mathf.Max(size.y, 0.12f),
                Mathf.Max(size.z, 0.18f));
            physicsCollider.isTrigger = false;
            physicsCollider.enabled = true;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i] == physicsCollider)
                    continue;

                if (!colliders[i].isTrigger && colliders[i].transform == transform)
                    colliders[i].enabled = false;
            }

            Debug.Log($"[CardboxBase] {name} physics collider center={physicsCollider.center}, size={physicsCollider.size}");
        }

        private void EnsureGrabCollider()
        {
            Transform existing = transform.Find("Cardbox Grab Collider");

            if (existing == null)
            {
                GameObject colliderObject = new GameObject("Cardbox Grab Collider");
                colliderObject.transform.SetParent(transform, false);
                existing = colliderObject.transform;
            }

            existing.localPosition = Vector3.zero;
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
            existing.gameObject.layer = gameObject.layer;

            grabCollider = existing.GetComponent<BoxCollider>();

            if (grabCollider == null)
                grabCollider = existing.gameObject.AddComponent<BoxCollider>();

            Bounds bounds = GetRendererLocalBounds();
            Vector3 size = Abs(bounds.size) + grabColliderPadding;
            grabCollider.center = bounds.center;
            grabCollider.size = new Vector3(
                Mathf.Max(size.x, 0.55f),
                Mathf.Max(size.y, 0.42f),
                Mathf.Max(size.z, 0.42f));
            grabCollider.isTrigger = true;
            grabCollider.enabled = true;
        }

        private void DisableMeshCollidersForPhysics()
        {
            MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);

            for (int i = 0; i < meshColliders.Length; i++)
            {
                if (meshColliders[i] == null || meshColliders[i].isTrigger)
                    continue;

                meshColliders[i].enabled = false;
            }
        }

        private void EnsureStorageTrigger()
        {
            Transform existing = transform.Find("Cardbox Storage Trigger");

            if (existing == null)
            {
                Transform oldExisting = transform.Find("Uranium Storage Trigger");

                if (oldExisting != null)
                {
                    oldExisting.name = "Cardbox Storage Trigger";
                    existing = oldExisting;
                }
            }

            if (existing != null)
                storageTrigger = existing.GetComponent<BoxCollider>();

            if (storageTrigger == null)
            {
                GameObject triggerObject = new GameObject("Cardbox Storage Trigger");
                triggerObject.transform.SetParent(transform, false);
                storageTrigger = triggerObject.AddComponent<BoxCollider>();
            }

            Bounds bounds = GetRendererLocalBounds();
            storageTrigger.center = bounds.center + Vector3.up * Mathf.Max(0.08f, bounds.size.y * 0.18f);
            storageTrigger.size = Abs(bounds.size) + storageTriggerPadding;
            storageTrigger.isTrigger = true;
        }

        private Bounds GetRendererLocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!CanUseRendererForBounds(renderers[i]))
                    continue;

                Bounds localBounds = ToLocalBounds(renderers[i].bounds);

                if (!hasBounds)
                {
                    bounds = localBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(localBounds);
                }
            }

            return bounds;
        }

        private bool CanUseRendererForBounds(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled)
                return false;

            if (renderer.GetComponentInParent<TMP_Text>() != null)
                return false;

            Transform current = renderer.transform;

            while (current != null && current != transform)
            {
                if (current.name == "Runtime Dynamic Cardbox Model" || current.name == "Runtime Visible Cardbox")
                    return false;

                current = current.parent;
            }

            return true;
        }

        private Bounds ToLocalBounds(Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds bounds = new Bounds(transform.InverseTransformPoint(min), Vector3.zero);
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(max));
            return bounds;
        }

        private void MakeDynamic()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                    children[i].gameObject.isStatic = false;
            }
        }

        private Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private Vector3 ShrinkSize(Vector3 size, Vector3 inset)
        {
            return new Vector3(
                Mathf.Max(0.01f, size.x - Mathf.Max(0f, inset.x) * 2f),
                Mathf.Max(0.01f, size.y - Mathf.Max(0f, inset.y) * 2f),
                Mathf.Max(0.01f, size.z - Mathf.Max(0f, inset.z) * 2f));
        }
    }
}
