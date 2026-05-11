using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class WorktableParticleSpawner : MonoBehaviour
    {
        public static WorktableParticleSpawner Instance { get; private set; }

        private static System.Collections.Generic.HashSet<IngredientType> unlockedIngredients = new System.Collections.Generic.HashSet<IngredientType> 
        { 
            IngredientType.QuarkUp, 
            IngredientType.QuarkDown,
            IngredientType.Electron
        };

        [System.Serializable]
        private struct ParticleOption
        {
            public IngredientType type;
            public string label;
            public Color color;

            public ParticleOption(IngredientType type, string label, Color color)
            {
                this.type = type;
                this.label = label;
                this.color = color;
            }
        }

        [Header("Scene References")]
        [SerializeField] private Transform worktable;
        [SerializeField] private Transform screwBox;
        [SerializeField] private string worktableName = "WorkTable";
        [SerializeField] private string screwBoxName = "ScrewBox";

        [Header("Layout")]
        [SerializeField] private Vector3 panelOffset = new Vector3(-0.39f, 0.3f, -0.39f);
        [SerializeField] private Vector3 panelRotation = new Vector3(90f, 270f, 180f);
        [SerializeField] private Vector3 iconStartOffset = new Vector3(0.15f, 0f, 0.15f);
        [SerializeField] private Vector3 iconSpacing = new Vector3(0.16f, 0f, -0.16f);
        [SerializeField] private Vector3 quantityBarOffset = new Vector3(-0.05f, 0f, 0f);
        [SerializeField] private float quantityBarLength = 0.45f;
        [SerializeField] private Vector3 screwBoxSpawnOffset = new Vector3(0f, 0.18f, 0f);

        [Header("Quantity")]
        [SerializeField] private int minQuantity = 1;
        [SerializeField] private int maxQuantity = 200;
        [SerializeField] private int selectedQuantity = 1;

        [Header("Packet")]
        [SerializeField] private float packetRadius = 0.09f;

        private ParticleOption[] particleOptions;
        private IngredientType selectedParticle = IngredientType.QuarkDown;
        private Transform panelRoot;
        private TMP_Text titleText;
        private TMP_Text quantityText;
        private Transform quantityHandle;
        private Renderer quantityTrackRenderer;
        private Renderer quantityHandleRenderer;
        private float nextPanelHealthCheckTime;

        private System.Collections.Generic.List<WorktableParticleButton> particleButtons = new System.Collections.Generic.List<WorktableParticleButton>();

        private void Awake()
        {
            Instance = this;

            particleOptions = new[]
            {
                new ParticleOption(IngredientType.QuarkDown, "Down", Color.blue),
                new ParticleOption(IngredientType.QuarkUp, "Up", Color.red),
                new ParticleOption(IngredientType.Electron, "Electron", Color.green),
                new ParticleOption(IngredientType.Proton, "Proton", Color.white),
                new ParticleOption(IngredientType.Neutron, "Neutron", new Color(0.72f, 0.72f, 0.72f)),
                new ParticleOption(IngredientType.Atom, "Helium", new Color(0.58f, 0.25f, 1f)),
            };
        }

        private void Start()
        {
            ResolveSceneReferences();
            BuildWorktablePanel();
            RefreshDisplay();
        }

        private void Update()
        {
            if (Time.time < nextPanelHealthCheckTime)
                return;

            nextPanelHealthCheckTime = Time.time + 0.5f;
            EnsurePanelStillAlive();
        }

        public void SelectParticle(IngredientType type)
        {
            selectedParticle = type;
            RefreshDisplay();
        }

        public void UnlockParticle(IngredientType type)
        {
            if (type == IngredientType.Uranium)
                return;

            EnsurePanelStillAlive();

            if (unlockedIngredients.Add(type))
            {
                foreach (var btn in particleButtons)
                {
                    if (btn.ParticleType == type)
                    {
                        btn.SetUnlocked(true);
                    }
                }
            }

            Debug.Log($"[WorktableParticleSpawner] Unlocked {type}. Buttons alive={CountAliveParticleButtons()}/{particleOptions.Length}, panel={(panelRoot != null ? panelRoot.name : "null")}");
        }

        public static bool IsParticleUnlocked(IngredientType type)
        {
            if (type == IngredientType.Uranium)
                return false;

            return unlockedIngredients.Contains(type);
        }

        public static void ResetUnlocks()
        {
            unlockedIngredients.Clear();
            unlockedIngredients.Add(IngredientType.QuarkUp);
            unlockedIngredients.Add(IngredientType.QuarkDown);
            unlockedIngredients.Add(IngredientType.Electron);

            if (Instance != null)
            {
                foreach (var btn in Instance.particleButtons)
                {
                    btn.SetUnlocked(unlockedIngredients.Contains(btn.ParticleType));
                }
            }
        }

        public void ChangeQuantity(int delta)
        {
            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, minQuantity, maxQuantity);
            RefreshDisplay();
        }

        public void SetQuantityFromNormalized(float normalizedValue)
        {
            int quantityRange = Mathf.Max(0, maxQuantity - minQuantity);
            int newQuantity = minQuantity + Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * quantityRange);
            SetQuantity(newQuantity);
        }

        public void SetQuantityFromWorldPoint(Vector3 worldPoint)
        {
            if (panelRoot == null)
                return;

            Vector3 localPoint = panelRoot.InverseTransformPoint(worldPoint);
            float minZ = quantityBarOffset.z - quantityBarLength * 0.5f;
            float maxZ = quantityBarOffset.z + quantityBarLength * 0.5f;
            SetQuantityFromNormalized(Mathf.InverseLerp(minZ, maxZ, localPoint.z));
        }

        public float GetQuantityNormalized()
        {
            if (maxQuantity <= minQuantity)
                return 0f;

            return Mathf.InverseLerp(minQuantity, maxQuantity, selectedQuantity);
        }

        private void SetQuantity(int quantity)
        {
            selectedQuantity = Mathf.Clamp(quantity, minQuantity, maxQuantity);
            RefreshDisplay();
        }

        public void SpawnSelectedPacket()
        {
            ResolveSceneReferences();

            Vector3 spawnPosition = screwBox != null
                ? screwBox.position + screwBoxSpawnOffset
                : transform.position + Vector3.up * 0.5f;

            ParticleOption option = GetOption(selectedParticle);
            GameObject packet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            packet.transform.position = spawnPosition;
            packet.transform.localScale = Vector3.one * (packetRadius * 2f);

            Renderer renderer = packet.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = option.color;
                renderer.material = material;
            }

            Rigidbody rb = packet.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ParticlePacket particlePacket = packet.AddComponent<ParticlePacket>();
            particlePacket.Configure(selectedParticle, selectedQuantity);

            IngredientBall ingredient = packet.AddComponent<IngredientBall>();
            ingredient.Configure(selectedParticle, option.label);

            XRGrabInteractable grabInteractable = packet.AddComponent<XRGrabInteractable>();
            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            CreatePacketQuantityLabel(packet.transform, option.label, selectedQuantity);
            UraniumDeliveryObjectiveController.NotifyWorktablePacketSpawned();
        }

        public class BillboardLabel : MonoBehaviour
        {
            private Camera mainCamera;

            private void Start()
            {
                mainCamera = Camera.main;
            }

            private void LateUpdate()
            {
                if (mainCamera != null)
                {
                    transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
                }
            }
        }

        private void CreatePacketQuantityLabel(Transform parent, string label, int quantity)
        {
            GameObject container = new GameObject("Packet Quantity Label");
            container.transform.position = parent.position + Vector3.up * (packetRadius * 2.6f);
            container.transform.localScale = Vector3.one;
            container.AddComponent<PacketQuantityLabel>().Configure(parent, packetRadius * 2.6f);

            string combinedText = $"{label} x{quantity}";
            TMP_Text textComp = CreateText("QuantityLabel", container.transform, Vector3.zero, 0.18f, combinedText, Color.white);
            textComp.fontStyle = FontStyles.Bold;
            textComp.enableWordWrapping = false;
            textComp.rectTransform.sizeDelta = new Vector2(2f, 0.35f);
            textComp.transform.localRotation = Quaternion.identity;
        }

        private void ResolveSceneReferences()
        {
            if (worktable == null)
                worktable = FindTransformByName(worktableName);

            if (screwBox == null)
                screwBox = FindTransformByName(screwBoxName);
        }

        private Transform FindTransformByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            GameObject found = GameObject.Find(objectName);
            return found != null ? found.transform : null;
        }

        private void BuildWorktablePanel()
        {
            Transform parent = worktable != null ? worktable : transform;

            if (panelRoot != null)
                Destroy(panelRoot.gameObject);

            particleButtons.Clear();

            GameObject root = new GameObject("Worktable Particle Spawner");
            panelRoot = root.transform;
            panelRoot.SetParent(parent);
            panelRoot.position = parent.position + panelOffset;
            panelRoot.rotation = Quaternion.Euler(panelRotation);

            titleText = CreateText("SelectedText", panelRoot, new Vector3(0f, 0.02f, -0.36f), 0.13f);
            quantityText = CreateText("QuantityText", panelRoot, new Vector3(0f, 0.02f, 0.24f), 0.11f);

            int columns = 2;
            for (int i = 0; i < particleOptions.Length; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Vector3 localPosition = iconStartOffset + new Vector3(iconSpacing.x * col, 0f, iconSpacing.z * row);
                
                GameObject icon = CreateButtonPrimitive(particleOptions[i].label, particleOptions[i].color, localPosition, new Vector3(0.1f, 0.025f, 0.1f));
                icon.transform.SetParent(panelRoot, false);
                
                WorktableParticleButton buttonScript = icon.AddComponent<WorktableParticleButton>();
                bool isInitiallyUnlocked = unlockedIngredients.Contains(particleOptions[i].type);
                buttonScript.ConfigureParticle(this, particleOptions[i].type, particleOptions[i].color, isInitiallyUnlocked);
                particleButtons.Add(buttonScript);
                
                CreateText(particleOptions[i].label + "Label", icon.transform, new Vector3(0f, 0.04f, 0.075f), 0.055f, particleOptions[i].label);
            }

            BuildQuantityBar();

            GameObject spawn = CreateButtonPrimitive("Spawn Packet", new Color(1f, 0.85f, 0.2f), new Vector3(-0.25f, 0f, 0.15f), new Vector3(0.18f, 0.035f, 0.12f));
            spawn.transform.SetParent(panelRoot, false);
            spawn.AddComponent<WorktableParticleButton>().ConfigureSpawn(this);
            CreateText("SpawnLabel", spawn.transform, new Vector3(0f, 0.045f, 0f), 0.055f, "SPAWN");

            Debug.Log($"[WorktableParticleSpawner] Built panel under {(parent != null ? parent.name : "null")}. Particle buttons={particleButtons.Count}");
        }

        private void EnsurePanelStillAlive()
        {
            if (particleOptions == null || particleOptions.Length == 0)
                return;

            bool needsRebuild = panelRoot == null || !panelRoot.gameObject.activeInHierarchy;

            if (!needsRebuild)
            {
                if (particleButtons.Count != particleOptions.Length)
                {
                    needsRebuild = true;
                }
                else
                {
                    for (int i = 0; i < particleButtons.Count; i++)
                    {
                        if (particleButtons[i] == null || !particleButtons[i].gameObject.activeInHierarchy)
                        {
                            needsRebuild = true;
                            break;
                        }
                    }
                }
            }

            if (!needsRebuild)
                return;

            Debug.LogWarning($"[WorktableParticleSpawner] Panel/buttons missing. Rebuilding. panel={(panelRoot != null ? panelRoot.name : "null")}, aliveButtons={CountAliveParticleButtons()}/{particleOptions.Length}");
            ResolveSceneReferences();
            BuildWorktablePanel();
            RefreshDisplay();
        }

        private int CountAliveParticleButtons()
        {
            int count = 0;

            for (int i = 0; i < particleButtons.Count; i++)
            {
                if (particleButtons[i] != null && particleButtons[i].gameObject.activeInHierarchy)
                    count++;
            }

            return count;
        }

        private void BuildQuantityBar()
        {
            GameObject track = CreateButtonPrimitive(
                "Quantity Scroll Bar",
                new Color(0.18f, 0.18f, 0.18f),
                quantityBarOffset,
                new Vector3(0.045f, 0.025f, quantityBarLength));
            track.transform.SetParent(panelRoot, false);
            track.AddComponent<WorktableQuantitySlider>().Configure(this);
            quantityTrackRenderer = track.GetComponent<Renderer>();

            GameObject handle = CreateButtonPrimitive(
                "Quantity Scroll Handle",
                Color.white,
                quantityBarOffset,
                new Vector3(0.065f, 0.045f, 0.06f));
            handle.transform.SetParent(panelRoot, false);
            handle.AddComponent<WorktableQuantitySlider>().Configure(this);
            quantityHandle = handle.transform;
            quantityHandleRenderer = handle.GetComponent<Renderer>();
        }

        private GameObject CreateButtonPrimitive(string objectName, Color color, Vector3 localPosition, Vector3 localScale)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = objectName;
            button.transform.localPosition = localPosition;
            button.transform.localScale = localScale;

            Renderer renderer = button.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.material = material;
            }

            return button;
        }

        private TMP_Text CreateText(string objectName, Transform parent, Vector3 localPosition, float fontSize, string text = "", Color? textColor = null)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor ?? Color.black;
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
            return tmp;
        }

        private void RefreshDisplay()
        {
            ParticleOption option = GetOption(selectedParticle);

            if (titleText != null)
                titleText.text = "Particle: " + option.label;

            if (quantityText != null)
                quantityText.text = "Quantity: " + selectedQuantity;

            if (quantityHandle != null)
            {
                float normalized = GetQuantityNormalized();
                float minZ = quantityBarOffset.z - quantityBarLength * 0.5f;
                float maxZ = quantityBarOffset.z + quantityBarLength * 0.5f;
                
                Vector3 handlePos = quantityHandle.localPosition;
                handlePos.z = Mathf.Lerp(minZ, maxZ, normalized);
                quantityHandle.localPosition = handlePos;
            }

            if (quantityTrackRenderer != null)
                quantityTrackRenderer.material.color = new Color(0.18f, 0.18f, 0.18f);

            if (quantityHandleRenderer != null)
                quantityHandleRenderer.material.color = option.color;
        }

        private ParticleOption GetOption(IngredientType type)
        {
            for (int i = 0; i < particleOptions.Length; i++)
            {
                if (particleOptions[i].type == type)
                    return particleOptions[i];
            }

            return particleOptions[0];
        }
    }
}
