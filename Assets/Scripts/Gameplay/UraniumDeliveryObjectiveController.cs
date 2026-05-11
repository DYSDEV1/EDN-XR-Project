using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace EDNXR.Gameplay
{
    public class UraniumDeliveryObjectiveController : MonoBehaviour
    {
        private enum Step
        {
            WaitingForDoor,
            CreateUranium,
            PrepareDelivery,
            Deliver,
            Completed
        }

        private struct HaloInfo
        {
            public GameObject Object;
            public Vector3 BaseScale;
            public Renderer Renderer;

            public HaloInfo(GameObject haloObject, Vector3 baseScale, Renderer renderer)
            {
                Object = haloObject;
                BaseScale = baseScale;
                Renderer = renderer;
            }
        }

        public static UraniumDeliveryObjectiveController Instance { get; private set; }

        [SerializeField] private string createUraniumMessage = "Creer de l'uranium";
        [SerializeField] private string prepareDeliveryMessage = "Preparer l'uranium pour la livraison";
        [SerializeField] private string deliverMessage = "Donner la livraison";
        [SerializeField] private string completedMessage = "Livraison terminee";
        [SerializeField] private string worktableName = "WorkTable";
        [SerializeField] private string doorName = "Door";
        [SerializeField] private string doorBellClipName = "sonette";
        [SerializeField] private float deliveryDistance = 1.25f;
        [SerializeField] private float haloScale = 1.35f;
        [SerializeField] private float haloPulseSpeed = 4.2f;
        [SerializeField] private Color haloColor = new Color(1f, 0.88f, 0.25f);

        private readonly List<HaloInfo> halos = new List<HaloInfo>();

        private Step currentStep = Step.WaitingForDoor;
        private Transform worktable;
        private Transform door;
        private AudioSource doorBellSource;
        private AudioClip doorBellClip;
        private Camera mainCamera;
        private Texture2D haloTexture;
        private bool firstWorktableSpawnSeen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            mainCamera = Camera.main;
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            ClearHalos();
        }

        private void Update()
        {
            PulseHalos();

            if (currentStep == Step.Deliver)
                TryCompleteDeliveryNearDoor();
        }

        public static void NotifyDoorOpened()
        {
            EnsureInstance()?.StartCreateUraniumObjective();
        }

        public static void NotifyWorktablePacketSpawned()
        {
            EnsureInstance()?.OnWorktablePacketSpawned();
        }

        public static void NotifyUraniumCreated()
        {
            EnsureInstance()?.StartPrepareDeliveryObjective();
        }

        public static void NotifyUraniumStored(CardboxBaseController cardbox)
        {
            EnsureInstance()?.StartDeliverObjective(cardbox);
        }

        private static UraniumDeliveryObjectiveController EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            UraniumDeliveryObjectiveController existing = FindObjectOfType<UraniumDeliveryObjectiveController>();
            if (existing != null)
                return existing;

            GameObject controller = new GameObject("Uranium Delivery Objective Controller");
            return controller.AddComponent<UraniumDeliveryObjectiveController>();
        }

        private void StartCreateUraniumObjective()
        {
            if (currentStep != Step.WaitingForDoor && currentStep != Step.CreateUranium)
                return;

            currentStep = Step.CreateUranium;
            SetObjective(createUraniumMessage);
            ClearHalos();
            ResolveReferences();

            if (worktable != null)
                AddHalo(worktable, "Worktable Uranium Objective Halo");

            Debug.Log("[UraniumDeliveryObjective] Objective active: create uranium.");
        }

        private void OnWorktablePacketSpawned()
        {
            if (currentStep != Step.CreateUranium || firstWorktableSpawnSeen)
                return;

            firstWorktableSpawnSeen = true;
            ClearHalos();
            Debug.Log("[UraniumDeliveryObjective] First worktable packet spawned; worktable halo removed.");
        }

        private void StartPrepareDeliveryObjective()
        {
            if (currentStep == Step.Deliver || currentStep == Step.Completed)
                return;

            currentStep = Step.PrepareDelivery;
            SetObjective(prepareDeliveryMessage);
            ClearHalos();

            CardboxBaseController[] cardboxes = FindObjectsOfType<CardboxBaseController>(true);
            for (int i = 0; i < cardboxes.Length; i++)
            {
                if (cardboxes[i] != null && cardboxes[i].gameObject.activeInHierarchy && !cardboxes[i].HasUranium)
                    AddHalo(cardboxes[i].transform, "Cardbox Delivery Prep Halo");
            }

            Debug.Log("[UraniumDeliveryObjective] Uranium created; prepare delivery objective active.");
        }

        private void StartDeliverObjective(CardboxBaseController cardbox)
        {
            if (currentStep == Step.Completed)
                return;

            currentStep = Step.Deliver;
            SetObjective(deliverMessage);
            ClearHalos();
            ResolveReferences();

            if (door != null)
            {
                AddHalo(door, "Door Delivery Halo");
                StartDoorBell();
            }

            Debug.Log($"[UraniumDeliveryObjective] Uranium packed in '{(cardbox != null ? cardbox.name : "unknown")}'. Deliver objective active.");
        }

        private void TryCompleteDeliveryNearDoor()
        {
            if (door == null)
                ResolveReferences();

            if (door == null)
                return;

            Bounds doorBounds = GetWorldBounds(door);
            CardboxBaseController[] cardboxes = FindObjectsOfType<CardboxBaseController>(true);
            for (int i = 0; i < cardboxes.Length; i++)
            {
                CardboxBaseController cardbox = cardboxes[i];

                if (cardbox == null || !cardbox.gameObject.activeInHierarchy || !cardbox.HasUranium)
                    continue;

                float distance = Vector3.Distance(cardbox.transform.position, doorBounds.ClosestPoint(cardbox.transform.position));

                if (distance > deliveryDistance)
                    continue;

                CompleteDelivery(cardbox);
                return;
            }
        }

        private void CompleteDelivery(CardboxBaseController cardbox)
        {
            currentStep = Step.Completed;
            StopDoorBell();
            ClearHalos();
            SetObjective(completedMessage);

            if (cardbox != null)
                cardbox.ConsumeForDelivery();

            Debug.Log($"[UraniumDeliveryObjective] Delivery completed with '{(cardbox != null ? cardbox.name : "unknown")}'.");
        }

        private void StartDoorBell()
        {
            if (door == null)
                return;

            if (doorBellSource == null)
            {
                Transform existingAudio = door.Find("Delivery Doorbell Audio");

                if (existingAudio == null)
                {
                    GameObject audioObject = new GameObject("Delivery Doorbell Audio");
                    audioObject.transform.SetParent(door, false);
                    existingAudio = audioObject.transform;
                }

                doorBellSource = existingAudio.GetComponent<AudioSource>();

                if (doorBellSource == null)
                    doorBellSource = existingAudio.gameObject.AddComponent<AudioSource>();
            }

            if (doorBellClip == null)
                doorBellClip = Resources.Load<AudioClip>(doorBellClipName);

            if (doorBellClip == null)
            {
                Debug.LogWarning($"[UraniumDeliveryObjective] Doorbell sound not found. Expected Assets/Resources/{doorBellClipName}.mp3");
                return;
            }

            doorBellSource.clip = doorBellClip;
            doorBellSource.loop = true;
            doorBellSource.playOnAwake = false;
            doorBellSource.spatialBlend = 1f;
            doorBellSource.volume = 0.9f;

            if (!doorBellSource.isPlaying)
                doorBellSource.Play();
        }

        private void StopDoorBell()
        {
            if (doorBellSource != null)
            {
                doorBellSource.Stop();
                doorBellSource.loop = false;
            }
        }

        private void SetObjective(string message)
        {
            if (ObjectiveHud.Instance != null)
                ObjectiveHud.Instance.SetMessage(message);
        }

        private void ResolveReferences()
        {
            if (worktable == null)
            {
                GameObject foundWorktable = GameObject.Find(worktableName);
                if (foundWorktable != null)
                    worktable = foundWorktable.transform;
            }

            if (door == null)
            {
                GameObject foundDoor = GameObject.Find(doorName);
                if (foundDoor != null)
                {
                    door = foundDoor.transform;
                    Debug.Log($"[UraniumDeliveryObjective] Delivery door resolved by exact name: '{door.name}'.");
                }
            }
        }

        private void AddHalo(Transform target, string haloName)
        {
            if (target == null)
                return;

            Bounds bounds = GetWorldBounds(target);
            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = haloName;
            halo.transform.position = bounds.center;
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.4f) * haloScale;
            halo.transform.localScale = Vector3.one * diameter;

            Collider collider = halo.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = halo.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                material.mainTexture = GetHaloTexture();
                material.color = new Color(haloColor.r, haloColor.g, haloColor.b, 0.65f);
                material.renderQueue = 3000;
                renderer.material = material;
            }

            FaceCamera(halo.transform);
            halos.Add(new HaloInfo(halo, halo.transform.localScale, renderer));
        }

        private void ClearHalos()
        {
            for (int i = 0; i < halos.Count; i++)
            {
                if (halos[i].Object != null)
                    Destroy(halos[i].Object);
            }

            halos.Clear();
        }

        private void PulseHalos()
        {
            float pulse = 0.65f + Mathf.Sin(Time.time * haloPulseSpeed) * 0.35f;

            for (int i = halos.Count - 1; i >= 0; i--)
            {
                if (halos[i].Object == null)
                {
                    halos.RemoveAt(i);
                    continue;
                }

                halos[i].Object.transform.localScale = halos[i].BaseScale * Mathf.Lerp(0.94f, 1.08f, pulse);
                FaceCamera(halos[i].Object.transform);

                if (halos[i].Renderer != null)
                {
                    Color markerColor = Color.Lerp(haloColor * 0.75f, haloColor * 1.7f, pulse);
                    markerColor.a = Mathf.Lerp(0.35f, 0.78f, pulse);
                    halos[i].Renderer.material.color = markerColor;
                }
            }
        }

        private void FaceCamera(Transform halo)
        {
            if (halo == null)
                return;

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            Vector3 toHalo = halo.position - mainCamera.transform.position;

            if (toHalo.sqrMagnitude > 0.001f)
                halo.rotation = Quaternion.LookRotation(toHalo.normalized, Vector3.up);
        }

        private Texture2D GetHaloTexture()
        {
            if (haloTexture != null)
                return haloTexture;

            const int size = 128;
            haloTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            haloTexture.wrapMode = TextureWrapMode.Clamp;
            haloTexture.filterMode = FilterMode.Bilinear;
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
                    haloTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            haloTexture.Apply();
            return haloTexture;
        }

        private Bounds GetWorldBounds(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(target.position, Vector3.one * 0.5f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!CanUseRendererForBounds(renderers[i]))
                    continue;

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
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

            while (current != null)
            {
                if (current.name.IndexOf("Halo", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || current.name.IndexOf("Highlight", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                current = current.parent;
            }

            return true;
        }
    }
}
