using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class WorktableParticleSpawner : MonoBehaviour
    {
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
        [SerializeField] private Vector3 panelOffset = new Vector3(0f, 0.72f, 0f);
        [SerializeField] private Vector3 iconStartOffset = new Vector3(-0.32f, 0f, -0.18f);
        [SerializeField] private Vector3 iconSpacing = new Vector3(0.16f, 0f, 0f);
        [SerializeField] private Vector3 gaugeOffset = new Vector3(-0.32f, 0f, 0.06f);
        [SerializeField] private Vector3 screwBoxSpawnOffset = new Vector3(0f, 0.18f, 0f);
        [SerializeField] private int gaugeSegments = 10;

        [Header("Quantity")]
        [SerializeField] private int minQuantity = 1;
        [SerializeField] private int maxQuantity = 10;
        [SerializeField] private int selectedQuantity = 1;

        [Header("Packet")]
        [SerializeField] private float packetRadius = 0.09f;

        private ParticleOption[] particleOptions;
        private IngredientType selectedParticle = IngredientType.QuarkDown;
        private Transform panelRoot;
        private TMP_Text titleText;
        private TMP_Text quantityText;
        private Renderer[] gaugeRenderers;

        private void Awake()
        {
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

        public void SelectParticle(IngredientType type)
        {
            selectedParticle = type;
            RefreshDisplay();
        }

        public void ChangeQuantity(int delta)
        {
            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, minQuantity, maxQuantity);
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

            packet.AddComponent<XRGrabInteractable>();
            CreateText("PacketCountLabel", packet.transform, new Vector3(0f, packetRadius + 0.035f, 0f), 0.12f, "x" + selectedQuantity);
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
            GameObject root = new GameObject("Worktable Particle Spawner");
            panelRoot = root.transform;
            panelRoot.SetParent(parent);
            panelRoot.position = parent.position + panelOffset;
            panelRoot.rotation = Quaternion.identity;

            titleText = CreateText("SelectedText", panelRoot, new Vector3(0f, 0.02f, -0.36f), 0.13f);
            quantityText = CreateText("QuantityText", panelRoot, new Vector3(0f, 0.02f, 0.24f), 0.11f);

            for (int i = 0; i < particleOptions.Length; i++)
            {
                Vector3 localPosition = iconStartOffset + iconSpacing * i;
                GameObject icon = CreateButtonPrimitive(particleOptions[i].label, particleOptions[i].color, localPosition, new Vector3(0.1f, 0.025f, 0.1f));
                icon.transform.SetParent(panelRoot, false);
                icon.AddComponent<WorktableParticleButton>().ConfigureParticle(this, particleOptions[i].type);
                CreateText(particleOptions[i].label + "Label", icon.transform, new Vector3(0f, 0.04f, 0.075f), 0.055f, particleOptions[i].label);
            }

            gaugeRenderers = new Renderer[gaugeSegments];

            for (int i = 0; i < gaugeSegments; i++)
            {
                Vector3 localPosition = gaugeOffset + new Vector3(0.065f * i, 0f, 0f);
                GameObject segment = CreateButtonPrimitive("GaugeSegment " + (i + 1), Color.gray, localPosition, new Vector3(0.05f, 0.018f, 0.035f));
                segment.transform.SetParent(panelRoot, false);
                gaugeRenderers[i] = segment.GetComponent<Renderer>();
            }

            GameObject minus = CreateButtonPrimitive("Quantity Minus", Color.red, gaugeOffset + new Vector3(-0.09f, 0f, 0f), new Vector3(0.055f, 0.025f, 0.055f));
            minus.transform.SetParent(panelRoot, false);
            minus.AddComponent<WorktableParticleButton>().ConfigureDecrease(this);
            CreateText("MinusLabel", minus.transform, new Vector3(0f, 0.04f, 0f), 0.09f, "-");

            GameObject plus = CreateButtonPrimitive("Quantity Plus", Color.green, gaugeOffset + new Vector3(0.065f * gaugeSegments + 0.03f, 0f, 0f), new Vector3(0.055f, 0.025f, 0.055f));
            plus.transform.SetParent(panelRoot, false);
            plus.AddComponent<WorktableParticleButton>().ConfigureIncrease(this);
            CreateText("PlusLabel", plus.transform, new Vector3(0f, 0.04f, 0f), 0.09f, "+");

            GameObject spawn = CreateButtonPrimitive("Spawn Packet", new Color(1f, 0.85f, 0.2f), new Vector3(0.22f, 0f, 0.36f), new Vector3(0.24f, 0.035f, 0.09f));
            spawn.transform.SetParent(panelRoot, false);
            spawn.AddComponent<WorktableParticleButton>().ConfigureSpawn(this);
            CreateText("SpawnLabel", spawn.transform, new Vector3(0f, 0.045f, 0f), 0.055f, "SPAWN");
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

        private TMP_Text CreateText(string objectName, Transform parent, Vector3 localPosition, float fontSize, string text = "")
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
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

            if (gaugeRenderers == null)
                return;

            int activeSegments = Mathf.RoundToInt(Mathf.InverseLerp(minQuantity, maxQuantity, selectedQuantity) * (gaugeRenderers.Length - 1)) + 1;

            for (int i = 0; i < gaugeRenderers.Length; i++)
            {
                if (gaugeRenderers[i] == null)
                    continue;

                gaugeRenderers[i].material.color = i < activeSegments ? option.color : Color.gray;
            }
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
