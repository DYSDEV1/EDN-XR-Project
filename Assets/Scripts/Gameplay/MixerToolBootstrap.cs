using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public static class MixerToolBootstrap
    {
        private const string MixerNamePrefix = "PaintCan";
        private static readonly Vector3 PhysicalColliderInset = new Vector3(0.025f, 0f, 0.025f);

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

        private static void PreparePaintCan(GameObject mixer)
        {
            SetDynamicRecursively(mixer);
            DisableProblemMeshColliders(mixer);
            EnsurePhysicalCollider(mixer);
            EnsureDynamicMixerBody(mixer);
            EnsureTriggerZone(mixer);
            CleanTriggerZones(mixer);

            XRGrabInteractable grabInteractable = mixer.GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
                grabInteractable = mixer.AddComponent<XRGrabInteractable>();

            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            ConfigureGrabColliders(mixer, grabInteractable);

            if (mixer.GetComponent<PcGrabbableObject>() == null)
                mixer.AddComponent<PcGrabbableObject>();

            if (mixer.GetComponent<PaintCanRuntimeDebug>() == null)
                mixer.AddComponent<PaintCanRuntimeDebug>();
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

        private static void EnsureDynamicMixerBody(GameObject mixer)
        {
            Rigidbody rb = mixer.GetComponent<Rigidbody>();

            if (rb == null)
                rb = mixer.AddComponent<Rigidbody>();

            rb.mass = 0.45f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private static void EnsurePhysicalCollider(GameObject mixer)
        {
            BoxCollider boxCollider = mixer.GetComponent<BoxCollider>();

            if (boxCollider == null)
                boxCollider = mixer.AddComponent<BoxCollider>();

            boxCollider.isTrigger = false;
            Renderer renderer = mixer.GetComponentInChildren<Renderer>();

            if (renderer != null)
            {
                Bounds localBounds = ToLocalBounds(mixer.transform, renderer.bounds);
                boxCollider.center = localBounds.center;
                boxCollider.size = ShrinkSize(localBounds.size, PhysicalColliderInset);
            }
            else
            {
                boxCollider.size = ShrinkSize(new Vector3(0.25f, 0.35f, 0.25f), PhysicalColliderInset);
            }
        }

        private static Vector3 ShrinkSize(Vector3 size, Vector3 inset)
        {
            return new Vector3(
                Mathf.Max(0.01f, size.x - Mathf.Max(0f, inset.x) * 2f),
                Mathf.Max(0.01f, size.y - Mathf.Max(0f, inset.y) * 2f),
                Mathf.Max(0.01f, size.z - Mathf.Max(0f, inset.z) * 2f));
        }

        private static void DisableProblemMeshColliders(GameObject mixer)
        {
            MeshCollider[] meshColliders = mixer.GetComponentsInChildren<MeshCollider>(true);
            int disabledCount = 0;

            for (int i = 0; i < meshColliders.Length; i++)
            {
                if (meshColliders[i] == null || meshColliders[i].isTrigger)
                    continue;

                meshColliders[i].enabled = false;
                disabledCount++;
            }

            if (disabledCount > 0)
                Debug.Log($"[MixerToolBootstrap] Disabled {disabledCount} MeshCollider(s) on {mixer.name}; using BoxCollider for physics/grab.");
        }

        private static void ConfigureGrabColliders(GameObject mixer, XRGrabInteractable grabInteractable)
        {
            grabInteractable.colliders.Clear();

            Collider[] colliders = mixer.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i].isTrigger)
                    continue;

                if (colliders[i].GetComponentInParent<BucketAssembler>() != null)
                    continue;

                if (colliders[i] is MeshCollider)
                    continue;

                grabInteractable.colliders.Add(colliders[i]);
            }

            Debug.Log($"[MixerToolBootstrap] {mixer.name} grab colliders: {grabInteractable.colliders.Count}");
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
            Vector3 min = root.InverseTransformPoint(worldBounds.min);
            Vector3 max = root.InverseTransformPoint(worldBounds.max);
            return new Bounds((min + max) * 0.5f, Abs(max - min));
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }

    public class PaintCanRuntimeDebug : MonoBehaviour
    {
        private float nextLogTime;
        private Vector3 lastPosition;

        private void Start()
        {
            lastPosition = transform.position;
            LogState("start");
        }

        private void Update()
        {
            if (Time.time < nextLogTime)
                return;

            nextLogTime = Time.time + 1f;
            LogState("tick");
            lastPosition = transform.position;
        }

        private void LogState(string phase)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            string firstRendererInfo = "none";

            if (renderers.Length > 0 && renderers[0] != null)
            {
                Transform rendererTransform = renderers[0].transform;
                firstRendererInfo =
                    $"'{renderers[0].name}' pos={rendererTransform.position} local={rendererTransform.localPosition} " +
                    $"parent='{(rendererTransform.parent != null ? rendererTransform.parent.name : "none")}' static={rendererTransform.gameObject.isStatic}";
            }

            Debug.Log(
                $"[PaintCanRuntimeDebug] {phase} '{name}' " +
                $"rootPos={transform.position} movedSinceLast={(transform.position - lastPosition).magnitude:F3} " +
                $"rb={(rb != null ? $"pos={rb.position}, kinematic={rb.isKinematic}, gravity={rb.useGravity}" : "none")} " +
                $"grab={(grab != null ? $"yes colliders={grab.colliders.Count}" : "none")} " +
                $"renderers={renderers.Length} firstRenderer={firstRendererInfo} " +
                $"colliders={colliders.Length}");
        }
    }
}
