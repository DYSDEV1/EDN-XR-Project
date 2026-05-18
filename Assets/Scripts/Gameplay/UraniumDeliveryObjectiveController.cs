using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
            ReturnToSleep,
            Finished
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
        [SerializeField] private string returnToSleepMessage = "Repartir dormir";
        [SerializeField] private string finishedMessage = "Session termin\u00e9e";
        [SerializeField] private string finalTitle = "Merci d'avoir jou\u00e9";
        [SerializeField] private string finalProducerLine = "Producteur : Etienne Baillieux";
        [SerializeField] private string finalInternLine = "Stagiaire : Vianney Lehu";
        [SerializeField] private string finalTimePrefix = "Temps final : ";
        [SerializeField] private string replayPromptLine = "Appuyez sur R ou A pour rejouer";
        [SerializeField] private string worktableName = "WorkTable";
        [SerializeField] private string doorName = "Door";
        [SerializeField] private string couchName = "Couch";
        [SerializeField] private string doorBellClipName = "sonette";
        [SerializeField] private float deliveryDistance = 1.25f;
        [SerializeField] private float haloScale = 1.35f;
        [SerializeField] private float haloPulseSpeed = 4.2f;
        [SerializeField] private Color haloColor = new Color(1f, 0.88f, 0.25f);
        [SerializeField] private Vector3 vrEndCanvasLocalPosition = new Vector3(0f, 0f, 1.05f);
        [SerializeField] private Vector2 vrEndCanvasSize = new Vector2(2.6f, 1.7f);

        private readonly List<HaloInfo> halos = new List<HaloInfo>();

        private Step currentStep = Step.WaitingForDoor;
        private Transform worktable;
        private Transform door;
        private Transform couch;
        private AudioSource doorBellSource;
        private AudioClip doorBellClip;
        private Camera mainCamera;
        private Texture2D haloTexture;
        private Canvas endCanvas;
        private string finalTimeLine = "Temps final : 00:00";
        private float replayInputEnabledTime;
        private bool firstWorktableSpawnSeen;
        private bool endMovementLocked;
        private bool replaying;
        private bool leftReplayButtonWasPressed;
        private bool rightReplayButtonWasPressed;

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

            StopDoorBell();
            ClearHalos();
            DestroyEndCanvas();

            if (endMovementLocked)
            {
                PlayerMovementLock.Unlock("end screen destroyed");
                endMovementLocked = false;
            }
        }

        private void Update()
        {
            PulseHalos();

            if (currentStep == Step.Deliver)
                TryCompleteDeliveryNearDoor();

            if (currentStep == Step.Finished)
                TryReplayFromEndScreen();
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
            if (currentStep == Step.Deliver || currentStep == Step.ReturnToSleep || currentStep == Step.Finished)
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
            if (currentStep == Step.ReturnToSleep || currentStep == Step.Finished)
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
            StopDoorBell();
            ClearHalos();

            if (cardbox != null)
                cardbox.ConsumeForDelivery();

            Debug.Log($"[UraniumDeliveryObjective] Delivery completed with '{(cardbox != null ? cardbox.name : "unknown")}'.");
            StartReturnToSleepObjective();
        }

        private void StartReturnToSleepObjective()
        {
            currentStep = Step.ReturnToSleep;
            SetObjective(returnToSleepMessage);
            ResolveReferences();
            ClearHalos();

            if (couch != null)
            {
                AddHalo(couch, "Return To Sleep Halo");
                EnsureSleepInteractable(couch);
            }
            else
            {
                Debug.LogWarning($"[UraniumDeliveryObjective] Cannot start sleep objective: no object named '{couchName}' found.");
            }

            Debug.Log("[UraniumDeliveryObjective] Final objective active: return to sleep.");
        }

        public void TryReturnToSleep()
        {
            if (currentStep != Step.ReturnToSleep)
                return;

            currentStep = Step.Finished;
            ClearHalos();
            SetObjective(finishedMessage);
            finalTimeLine = finalTimePrefix + LevelTimerController.FormatTime(LevelTimerController.NotifyLevelFinished());
            replayInputEnabledTime = Time.time + 0.55f;
            leftReplayButtonWasPressed = IsXRReplayButtonDown(UnityEngine.XR.XRNode.LeftHand);
            rightReplayButtonWasPressed = IsXRReplayButtonDown(UnityEngine.XR.XRNode.RightHand);

            if (!endMovementLocked)
            {
                PlayerMovementLock.Lock("end screen");
                endMovementLocked = true;
            }

            BuildEndScreen();
            Debug.Log("[UraniumDeliveryObjective] End screen displayed.");
        }

        private void TryReplayFromEndScreen()
        {
            if (replaying || Time.time < replayInputEnabledTime)
                return;

            if (!ReplayPressed())
                return;

            replaying = true;
            SetObjective("Redemarrage...");
            SessionResetButton.ResetSessionNow("end screen replay");
        }

        private bool ReplayPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null
                && (Keyboard.current.rKey.wasPressedThisFrame
                    || Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.R)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.Space))
            {
                return true;
            }
#endif

            return XRReplayButtonPressed(UnityEngine.XR.XRNode.LeftHand, ref leftReplayButtonWasPressed)
                || XRReplayButtonPressed(UnityEngine.XR.XRNode.RightHand, ref rightReplayButtonWasPressed);
        }

        private bool XRReplayButtonPressed(UnityEngine.XR.XRNode node, ref bool wasPressed)
        {
            bool pressed = IsXRReplayButtonDown(node);
            bool justPressed = pressed && !wasPressed;
            wasPressed = pressed;
            return justPressed;
        }

        private bool IsXRReplayButtonDown(UnityEngine.XR.XRNode node)
        {
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);

            if (!device.isValid)
                return false;

            bool pressed;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out pressed) && pressed)
                return true;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out pressed) && pressed)
                return true;

            return false;
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

            if (couch == null)
            {
                GameObject foundCouch = GameObject.Find(couchName);
                if (foundCouch != null)
                    couch = foundCouch.transform;
            }
        }

        private void EnsureSleepInteractable(Transform target)
        {
            ReturnToSleepObjectiveInteractable interactable = target.GetComponent<ReturnToSleepObjectiveInteractable>();

            if (interactable == null)
                interactable = target.gameObject.AddComponent<ReturnToSleepObjectiveInteractable>();

            interactable.Configure(this);
        }

        private void BuildEndScreen()
        {
            DestroyEndCanvas();

            GameObject canvasObject = new GameObject("End Screen Canvas");
            canvasObject.transform.SetParent(transform, false);
            endCanvas = canvasObject.AddComponent<Canvas>();
            endCanvas.sortingOrder = 3000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            ConfigureEndCanvas(endCanvas);

            GameObject backgroundObject = new GameObject("End Screen Background");
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            Image background = backgroundObject.AddComponent<Image>();
            background.color = Color.black;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            TMP_Text title = CreateEndText("End Title", canvasObject.transform);
            title.text = finalTitle;
            title.fontSize = 76f;
            title.fontStyle = FontStyles.Bold;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.48f);
            titleRect.anchorMax = new Vector2(0.92f, 0.68f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TMP_Text time = CreateEndText("End Final Time", canvasObject.transform);
            time.text = finalTimeLine;
            time.fontSize = 40f;
            time.fontStyle = FontStyles.Bold;
            RectTransform timeRect = time.rectTransform;
            timeRect.anchorMin = new Vector2(0.08f, 0.38f);
            timeRect.anchorMax = new Vector2(0.92f, 0.46f);
            timeRect.offsetMin = Vector2.zero;
            timeRect.offsetMax = Vector2.zero;

            TMP_Text credits = CreateEndText("End Credits", canvasObject.transform);
            credits.text = finalProducerLine + "\n" + finalInternLine;
            credits.fontSize = 42f;
            credits.fontStyle = FontStyles.Normal;
            RectTransform creditsRect = credits.rectTransform;
            creditsRect.anchorMin = new Vector2(0.08f, 0.16f);
            creditsRect.anchorMax = new Vector2(0.92f, 0.34f);
            creditsRect.offsetMin = Vector2.zero;
            creditsRect.offsetMax = Vector2.zero;

            TMP_Text replay = CreateEndText("End Replay Prompt", canvasObject.transform);
            replay.text = replayPromptLine;
            replay.fontSize = 32f;
            replay.fontStyle = FontStyles.Normal;
            RectTransform replayRect = replay.rectTransform;
            replayRect.anchorMin = new Vector2(0.08f, 0.045f);
            replayRect.anchorMax = new Vector2(0.92f, 0.12f);
            replayRect.offsetMin = Vector2.zero;
            replayRect.offsetMax = Vector2.zero;
        }

        private void ConfigureEndCanvas(Canvas canvas)
        {
            if (!XRSettings.isDeviceActive || Camera.main == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            Camera camera = Camera.main;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            rectTransform.SetParent(camera.transform, false);
            rectTransform.localPosition = vrEndCanvasLocalPosition;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one * 0.001f;
            rectTransform.sizeDelta = vrEndCanvasSize * 1000f;
        }

        private TMP_Text CreateEndText(string objectName, Transform parent)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            return text;
        }

        private void DestroyEndCanvas()
        {
            if (endCanvas == null)
                return;

            Destroy(endCanvas.gameObject);
            endCanvas = null;
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
