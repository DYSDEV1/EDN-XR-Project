using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public static class MixerToolBootstrap
    {
        private const string MixerNamePrefix = "PaintCan";
        private static readonly Vector3 PhysicalColliderInset = new Vector3(0.045f, 0.015f, 0.045f);
        private static readonly Vector3 MaxPhysicalColliderSize = new Vector3(0.42f, 0.58f, 0.42f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMixerTool()
        {
            Transform[] sceneObjects = Object.FindObjectsOfType<Transform>(true);
            int preparedCount = 0;

            for (int i = 0; i < sceneObjects.Length; i++)
            {
                if (sceneObjects[i] == null || !IsPaintCanName(sceneObjects[i].name))
                    continue;

                PreparePaintCan(sceneObjects[i].gameObject);
                preparedCount++;
            }

            Debug.Log($"[MixerToolBootstrap] Prepared {preparedCount} paintcan(s).");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureMixerTool();
        }

        private static void PreparePaintCan(GameObject mixer)
        {
            SetDynamicRecursively(mixer);
            ConfigureMeshCollidersForGrab(mixer);
            EnsurePhysicalCollider(mixer);
            EnsureDynamicMixerBody(mixer);
            EnsureTriggerZone(mixer);
            CleanTriggerZones(mixer);
            RemoveNestedRigidbodies(mixer);

            XRGrabInteractable grabInteractable = mixer.GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
                grabInteractable = mixer.AddComponent<XRGrabInteractable>();

            grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grabInteractable.velocityDamping = 0.75f;
            grabInteractable.velocityScale = 0.85f;
            grabInteractable.angularVelocityDamping = 0.75f;
            grabInteractable.angularVelocityScale = 0.75f;
            grabInteractable.snapToColliderVolume = false;
            grabInteractable.throwOnDetach = false;
            grabInteractable.forceGravityOnDetach = true;
            ConfigureGrabColliders(mixer, grabInteractable);
            EnsureRecipeInteractor(mixer, grabInteractable);
            EnsureStabilityGuard(mixer);
            IgnorePlayerCollision(mixer);

            if (mixer.GetComponent<PcGrabbableObject>() == null)
                mixer.AddComponent<PcGrabbableObject>();

        }

        private static bool IsPaintCanName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            return objectName.Replace(" ", "").StartsWith(MixerNamePrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void SetDynamicRecursively(GameObject mixer)
        {
            Transform[] children = mixer.GetComponentsInChildren<Transform>(true);
            int changedCount = 0;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null)
                    continue;

                if (children[i].gameObject.isStatic)
                    changedCount++;

                children[i].gameObject.isStatic = false;
            }

            if (changedCount > 0)
                Debug.Log($"[MixerToolBootstrap] Set {changedCount} object(s) under {mixer.name} to non-static so the model can follow physics.");
        }

        private static void EnsureTriggerZone(GameObject mixer)
        {
            if (mixer.GetComponentInChildren<BucketAssembler>(true) != null)
                return;

            GameObject triggerZone = new GameObject("TriggerZone");
            triggerZone.transform.SetParent(mixer.transform, false);
            triggerZone.transform.localPosition = new Vector3(0f, 0.01f, 0.01f);
            triggerZone.transform.localRotation = Quaternion.identity;
            triggerZone.transform.localScale = Vector3.one;

            BoxCollider trigger = triggerZone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.55f, 0.55f, 0.55f);
            trigger.center = Vector3.zero;

            triggerZone.AddComponent<BucketAssembler>();

            Debug.Log($"[MixerToolBootstrap] Added TriggerZone to {mixer.name}");
        }

        private static void CleanTriggerZones(GameObject mixer)
        {
            BucketAssembler[] triggerZones = mixer.GetComponentsInChildren<BucketAssembler>(true);

            for (int i = 0; i < triggerZones.Length; i++)
            {
                if (triggerZones[i] == null || triggerZones[i].gameObject == mixer)
                    continue;

                XRGrabInteractable xrGrab = triggerZones[i].GetComponent<XRGrabInteractable>();
                if (xrGrab != null)
                    Object.Destroy(xrGrab);

                PcGrabbableObject pcGrab = triggerZones[i].GetComponent<PcGrabbableObject>();
                if (pcGrab != null)
                    Object.Destroy(pcGrab);

                Rigidbody rb = triggerZones[i].GetComponent<Rigidbody>();
                if (rb != null)
                    Object.Destroy(rb);

                Collider[] colliders = triggerZones[i].GetComponents<Collider>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c] != null)
                        colliders[c].isTrigger = true;
                }
            }
        }

        private static void EnsureRecipeInteractor(GameObject mixer, XRGrabInteractable grabInteractable)
        {
            PaintCanRecipeInteractable recipeInteractable = mixer.GetComponent<PaintCanRecipeInteractable>();

            if (recipeInteractable == null)
                recipeInteractable = mixer.AddComponent<PaintCanRecipeInteractable>();

            recipeInteractable.Configure(mixer.GetComponentInChildren<BucketAssembler>(true), grabInteractable);
        }

        private static void EnsureDynamicMixerBody(GameObject mixer)
        {
            Rigidbody rb = mixer.GetComponent<Rigidbody>();

            if (rb == null)
                rb = mixer.AddComponent<Rigidbody>();

            rb.mass = 0.45f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxDepenetrationVelocity = 0.8f;
            rb.maxAngularVelocity = 6f;
            rb.drag = 0.35f;
            rb.angularDrag = 1.5f;
        }

        private static void EnsurePhysicalCollider(GameObject mixer)
        {
            BoxCollider boxCollider = mixer.GetComponent<BoxCollider>();

            if (boxCollider == null)
                boxCollider = mixer.AddComponent<BoxCollider>();

            boxCollider.isTrigger = false;
            Bounds localBounds = GetRendererLocalBounds(mixer.transform);

            if (localBounds.size.sqrMagnitude > 0.0001f)
            {
                boxCollider.center = localBounds.center;
                boxCollider.size = ClampSize(ShrinkSize(localBounds.size, PhysicalColliderInset), MaxPhysicalColliderSize);
            }
            else
            {
                boxCollider.size = ClampSize(ShrinkSize(new Vector3(0.25f, 0.35f, 0.25f), PhysicalColliderInset), MaxPhysicalColliderSize);
            }
        }

        private static Vector3 ShrinkSize(Vector3 size, Vector3 inset)
        {
            return new Vector3(
                Mathf.Max(0.01f, size.x - Mathf.Max(0f, inset.x) * 2f),
                Mathf.Max(0.01f, size.y - Mathf.Max(0f, inset.y) * 2f),
                Mathf.Max(0.01f, size.z - Mathf.Max(0f, inset.z) * 2f));
        }

        private static Vector3 ClampSize(Vector3 size, Vector3 maxSize)
        {
            return new Vector3(
                Mathf.Clamp(size.x, 0.04f, maxSize.x),
                Mathf.Clamp(size.y, 0.08f, maxSize.y),
                Mathf.Clamp(size.z, 0.04f, maxSize.z));
        }

        private static void RemoveNestedRigidbodies(GameObject mixer)
        {
            Rigidbody[] bodies = mixer.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null || bodies[i].gameObject == mixer)
                    continue;

                Object.Destroy(bodies[i]);
            }
        }

        private static void EnsureStabilityGuard(GameObject mixer)
        {
            if (mixer.GetComponent<PaintCanStabilityGuard>() == null)
                mixer.AddComponent<PaintCanStabilityGuard>();
        }

        private static void ConfigureMeshCollidersForGrab(GameObject mixer)
        {
            MeshCollider[] meshColliders = mixer.GetComponentsInChildren<MeshCollider>(true);
            int configuredCount = 0;

            for (int i = 0; i < meshColliders.Length; i++)
            {
                if (meshColliders[i] == null)
                    continue;

                if (meshColliders[i].GetComponentInParent<BucketAssembler>() != null)
                    continue;

                meshColliders[i].convex = true;
                meshColliders[i].isTrigger = true;
                meshColliders[i].enabled = true;
                configuredCount++;
            }

            if (configuredCount == 0)
            {
                MeshFilter meshFilter = mixer.GetComponentInChildren<MeshFilter>(true);

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = true;
                    meshCollider.isTrigger = true;
                    meshCollider.enabled = true;
                    configuredCount = 1;
                }
            }

            if (configuredCount > 0)
                Debug.Log($"[MixerToolBootstrap] Configured {configuredCount} MeshCollider(s) on {mixer.name} for XR grab shape.");
        }

        private static void ConfigureGrabColliders(GameObject mixer, XRGrabInteractable grabInteractable)
        {
            grabInteractable.colliders.Clear();

            Collider[] colliders = mixer.GetComponentsInChildren<Collider>(true);
            int meshGrabColliders = 0;
            int physicalGrabColliders = 0;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].enabled)
                    continue;

                if (colliders[i].GetComponentInParent<BucketAssembler>() != null)
                    continue;

                if (colliders[i] is MeshCollider)
                {
                    grabInteractable.colliders.Add(colliders[i]);
                    meshGrabColliders++;
                    continue;
                }

                if (colliders[i].isTrigger)
                    continue;

                grabInteractable.colliders.Add(colliders[i]);
                physicalGrabColliders++;
            }

            Debug.Log($"[MixerToolBootstrap] {mixer.name} grab colliders: {grabInteractable.colliders.Count} (mesh={meshGrabColliders}, physical={physicalGrabColliders})");
        }

        private static void IgnorePlayerCollision(GameObject mixer)
        {
            Collider[] mixerColliders = mixer.GetComponentsInChildren<Collider>(true);
            CharacterController[] characterControllers = Object.FindObjectsOfType<CharacterController>(true);
            int ignoredPairs = 0;

            for (int i = 0; i < mixerColliders.Length; i++)
            {
                Collider mixerCollider = mixerColliders[i];

                if (mixerCollider == null)
                    continue;

                for (int c = 0; c < characterControllers.Length; c++)
                {
                    CharacterController characterController = characterControllers[c];

                    if (characterController == null)
                        continue;

                    Physics.IgnoreCollision(mixerCollider, characterController, true);
                    ignoredPairs++;
                }
            }

            if (ignoredPairs > 0)
                Debug.Log($"[MixerToolBootstrap] Ignored {ignoredPairs} paintcan/player collision pair(s) for {mixer.name}.");
        }

        private static bool IsPlayerCharacterController(CharacterController characterController)
        {
            Transform current = characterController.transform;

            while (current != null)
            {
                if (current.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || current.name.IndexOf("XR Rig", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Bounds GetRendererLocalBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Bounds rendererBounds = ToLocalBounds(root, renderers[i].bounds);

                if (!hasBounds)
                {
                    combinedBounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(rendererBounds.min);
                    combinedBounds.Encapsulate(rendererBounds.max);
                }
            }

            return hasBounds ? combinedBounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        private static bool HasPhysicalCollider(GameObject mixer)
        {
            Collider[] colliders = mixer.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                    return true;
            }

            return false;
        }

        private static Bounds ToLocalBounds(Transform root, Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds localBounds = new Bounds(root.InverseTransformPoint(worldBounds.center), Vector3.zero);

            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, min.y, min.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            localBounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, max.y, max.z)));
            return localBounds;
        }

    }

    public class PaintCanRuntimeDebug : MonoBehaviour
    {
    }

    public class PaintCanStabilityGuard : MonoBehaviour
    {
        private const float LowestSafeY = -0.35f;
        private const float MaxLinearSpeed = 4.5f;
        private const float MaxAngularSpeed = 8f;
        private Rigidbody rb;
        private XRGrabInteractable grabInteractable;
        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private bool pcGrabbed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<XRGrabInteractable>();
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
            ConfigureBody();
        }

        private void LateUpdate()
        {
            ConfigureBody();

            if (!IsFinite(transform.position) || transform.position.y < LowestSafeY)
            {
                ResetToLastSafePose();
                CameraRenderGuard.EnsureCameraIsRenderingNow();
                return;
            }

            if (transform.position.y > 0.05f)
            {
                lastSafePosition = transform.position;
                lastSafeRotation = transform.rotation;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ClampVelocity();
        }

        private void OnCollisionStay(Collision collision)
        {
            ClampVelocity();
        }

        private void ConfigureBody()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (rb == null)
                return;

            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxDepenetrationVelocity = 0.8f;
            rb.maxAngularVelocity = 6f;

            if (IsHeld())
            {
                ClampVelocity();
                return;
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            ClampVelocity();
        }

        private void ClampVelocity()
        {
            if (rb == null)
                return;

            if (rb.velocity.sqrMagnitude > MaxLinearSpeed * MaxLinearSpeed)
                rb.velocity = rb.velocity.normalized * MaxLinearSpeed;

            if (rb.angularVelocity.sqrMagnitude > MaxAngularSpeed * MaxAngularSpeed)
                rb.angularVelocity = rb.angularVelocity.normalized * MaxAngularSpeed;
        }

        private void ResetToLastSafePose()
        {
            transform.SetPositionAndRotation(lastSafePosition, lastSafeRotation);
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Debug.LogWarning($"[PaintCanStabilityGuard] Reset '{name}' to its last safe pose after an unsafe physics position.");
        }

        private void OnPcGrabbed()
        {
            pcGrabbed = true;
        }

        private void OnPcReleased()
        {
            pcGrabbed = false;
        }

        private bool IsHeld()
        {
            return pcGrabbed || (grabInteractable != null && grabInteractable.isSelected);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
