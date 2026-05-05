using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class BucketAssembler : MonoBehaviour
    {
        [System.Serializable]
        private struct RecipeEntry
        {
            public IngredientType ingredientType;
            public int requiredCount;

            public RecipeEntry(IngredientType ingredientType, int requiredCount)
            {
                this.ingredientType = ingredientType;
                this.requiredCount = requiredCount;
            }
        }

        private class CraftRecipe
        {
            public readonly string displayName;
            public readonly string formula;
            public readonly IngredientType outputType;
            public readonly Color outputColor;
            public readonly float outputRadius;
            public readonly RecipeEntry[] entries;

            public CraftRecipe(string displayName, string formula, IngredientType outputType, Color outputColor, float outputRadius, params RecipeEntry[] entries)
            {
                this.displayName = displayName;
                this.formula = formula;
                this.outputType = outputType;
                this.outputColor = outputColor;
                this.outputRadius = outputRadius;
                this.entries = entries;
            }
        }

        [Header("References")]
        [SerializeField] private ParticleRecipe targetRecipe;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Spawn Points")]
        [SerializeField] private Transform outputSpawnPoint;
        [SerializeField] private Transform defaultElectronSpawnOrigin;

        [Header("Workbench Spawn")]
        [SerializeField] private Transform workbenchSpawnA;
        [SerializeField] private Transform workbenchSpawnB;
        [SerializeField] private string workbenchAName = "WorkBench (3)";
        [SerializeField] private string workbenchBName = "WorkBench (4)";
        [SerializeField] private Vector3 workbenchSpawnOffset = new Vector3(0f, 0.55f, 0f);
        [SerializeField] private Vector3 workbenchColumnSpacing = new Vector3(0.14f, 0f, 0f);
        [SerializeField] private Vector3 workbenchRowSpacing = new Vector3(0f, 0f, 0.14f);
        [SerializeField] private int workbenchColumns = 6;

        [Header("Legacy Visual")]
        [SerializeField] private GameObject protonVisual;
        [SerializeField] private Transform protonSpawnPoint;

        [Header("Default Starting Particles")]
        [SerializeField] private bool spawnDefaultQuarksOnStart = true;
        [SerializeField] private int defaultQuarkUpCount = 6;
        [SerializeField] private int defaultQuarkDownCount = 6;
        [SerializeField] private float quarkRadius = 0.055f;
        [SerializeField] private Transform defaultQuarkSpawnOrigin;
        [SerializeField] private Vector3 defaultQuarkStartOffset = new Vector3(-0.42f, 0.18f, -0.12f);
        [SerializeField] private Vector3 defaultQuarkSpacing = new Vector3(0.12f, 0f, 0f);
        [SerializeField] private bool spawnDefaultElectronsOnStart = true;
        [SerializeField] private int defaultElectronCount = 2;
        [SerializeField] private float electronRadius = 0.045f;
        [SerializeField] private Vector3 defaultElectronStartOffset = new Vector3(-0.22f, 0.18f, 0.18f);
        [SerializeField] private Vector3 defaultElectronSpacing = new Vector3(0.14f, 0f, 0f);

        [Header("Settings")]
        [SerializeField] private bool consumeIngredientOnEnter = true;
        [SerializeField] private float successDelay = 0.2f;
        [SerializeField] private bool clearSpawnedOutputsOnReset = false;

        [Header("Timing Mini Game - saved for later")]
        [SerializeField] private bool requireTimingMiniGame = false;
        [SerializeField] private int timingSuccessesRequired = 3;
        [SerializeField] private float cursorSpeed = 1.4f;
        [SerializeField, Range(0f, 1f)] private float successZoneCenter = 0.5f;
        [SerializeField, Range(0.05f, 0.8f)] private float successZoneSize = 0.22f;
        [SerializeField] private KeyCode timingKey = KeyCode.Space;
        [SerializeField] private bool resetProgressOnMiss = true;

        [Header("Events")]
        public UnityEvent onRecipeCompleted;
        public UnityEvent onWrongRecipe;

        private readonly Dictionary<IngredientType, int> currentCounts = new();
        private readonly List<GameObject> spawnedOutputs = new();
        private readonly HashSet<GameObject> protectedRecipeOutputs = new();
        private CraftRecipe[] builtInRecipes;
        private CraftRecipe pendingRecipe;
        private bool timingMiniGameActive = false;
        private int timingSuccessCount = 0;
        private float cursorPosition = 0f;
        private int cursorDirection = 1;
        private bool xrTimingButtonWasHeld = false;
        private int workbenchSpawnIndex = 0;

        private const int TimingBarSegments = 21;

        private void Awake()
        {
            builtInRecipes = new[]
            {
                new CraftRecipe(
                    "Proton",
                    "uud",
                    IngredientType.Proton,
                    Color.white,
                    0.09f,
                    new RecipeEntry(IngredientType.QuarkUp, 2),
                    new RecipeEntry(IngredientType.QuarkDown, 1)),

                new CraftRecipe(
                    "Neutron",
                    "udd",
                    IngredientType.Neutron,
                    new Color(0.72f, 0.72f, 0.72f),
                    0.09f,
                    new RecipeEntry(IngredientType.QuarkUp, 1),
                    new RecipeEntry(IngredientType.QuarkDown, 2)),

                new CraftRecipe(
                    "Atome d'helium",
                    "2p + 2n + 2e",
                    IngredientType.Atom,
                    new Color(0.58f, 0.25f, 1f),
                    0.14f,
                    new RecipeEntry(IngredientType.Proton, 2),
                    new RecipeEntry(IngredientType.Neutron, 2),
                    new RecipeEntry(IngredientType.Electron, 2)),
            };
        }

        private void Start()
        {
            ResolveWorkbenchSpawnPoints();

            if (outputSpawnPoint == null)
                outputSpawnPoint = workbenchSpawnA != null ? workbenchSpawnA : protonSpawnPoint;

            if (protonVisual != null)
                protonVisual.SetActive(false);

            if (spawnDefaultQuarksOnStart)
                SpawnDefaultQuarks();

            if (spawnDefaultElectronsOnStart)
                SpawnDefaultElectrons();

            UpdateFeedback();
        }

        private void Update()
        {
            if (!timingMiniGameActive)
                return;

            UpdateTimingCursor();

            if (TimingButtonWasPressed())
            {
                PressTimingButton();
                return;
            }

            UpdateTimingFeedback(null);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (timingMiniGameActive) return;

            IngredientBall ingredient = GetValidIngredient(other);
            if (ingredient == null) return;
            if (ingredient.IsConsumed) return;
            if (protectedRecipeOutputs.Contains(ingredient.gameObject)) return;

            AddIngredient(ingredient);
        }

        private void AddIngredient(IngredientBall ingredient)
        {
            IngredientType type = ingredient.Type;

            if (!currentCounts.ContainsKey(type))
                currentCounts[type] = 0;

            currentCounts[type]++;

            if (consumeIngredientOnEnter)
                ingredient.Consume();

            CraftRecipe matchingRecipe = FindMatchingRecipe();

            if (matchingRecipe != null)
            {
                CompleteCraft(matchingRecipe);
                return;
            }

            if (!CanStillMatchAnyRecipe())
            {
                SetText("Aucune recette avec cette combinaison. Appuie sur Reset.");
                onWrongRecipe?.Invoke();
                return;
            }

            UpdateFeedback();
        }

        private void CompleteCraft(CraftRecipe recipe)
        {
            if (requireTimingMiniGame)
            {
                StartTimingMiniGame(recipe);
                return;
            }

            SpawnRecipeOutput(recipe);
            currentCounts.Clear();
            SetText($"Recette reussie : {recipe.displayName} ({recipe.formula}) cree sur la workbench.");
            Invoke(nameof(CompleteRecipe), successDelay);
        }

        private void SpawnRecipeOutput(CraftRecipe recipe)
        {
            Vector3 spawnPosition = GetWorkbenchSpawnPosition();
            GameObject output = CreateParticleBall(recipe.displayName, recipe.outputType, recipe.outputColor, recipe.outputRadius, spawnPosition);
            spawnedOutputs.Add(output);
            protectedRecipeOutputs.Add(output);
            StartCoroutine(UnprotectRecipeOutput(output));
        }

        private IEnumerator UnprotectRecipeOutput(GameObject output)
        {
            yield return new WaitForSeconds(0.5f);

            if (output != null)
                protectedRecipeOutputs.Remove(output);
        }

        private GameObject CreateParticleBall(string particleName, IngredientType type, Color color, float radius, Vector3 position)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = particleName;
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * (radius * 2f);

            Renderer renderer = ball.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.material = material;
            }

            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = type == IngredientType.Electron ? 0.15f : 0.45f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            IngredientBall ingredient = ball.AddComponent<IngredientBall>();
            ingredient.Configure(type, particleName);

            if (ball.GetComponent<XRGrabInteractable>() == null)
                ball.AddComponent<XRGrabInteractable>();

            return ball;
        }

        private Vector3 GetOutputSpawnPosition()
        {
            Transform spawn = outputSpawnPoint != null ? outputSpawnPoint : transform;
            return spawn.position + Vector3.up * 0.04f;
        }

        private void ResolveWorkbenchSpawnPoints()
        {
            if (workbenchSpawnA == null)
                workbenchSpawnA = FindTransformByName(workbenchAName);

            if (workbenchSpawnB == null)
                workbenchSpawnB = FindTransformByName(workbenchBName);
        }

        private Transform FindTransformByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            GameObject found = GameObject.Find(objectName);
            return found != null ? found.transform : null;
        }

        private Vector3 GetWorkbenchSpawnPosition()
        {
            Transform bench = GetWorkbenchForSpawn(workbenchSpawnIndex);

            if (bench == null)
                return GetOutputSpawnPosition();

            int localIndex = workbenchSpawnIndex / GetActiveWorkbenchCount();
            int column = workbenchColumns <= 0 ? 0 : localIndex % workbenchColumns;
            int row = workbenchColumns <= 0 ? 0 : localIndex / workbenchColumns;
            Vector3 position = bench.position
                + workbenchSpawnOffset
                + workbenchColumnSpacing * column
                + workbenchRowSpacing * row;

            workbenchSpawnIndex++;
            return position;
        }

        private Transform GetWorkbenchForSpawn(int index)
        {
            bool hasA = workbenchSpawnA != null;
            bool hasB = workbenchSpawnB != null;

            if (hasA && hasB)
                return index % 2 == 0 ? workbenchSpawnA : workbenchSpawnB;

            if (hasA)
                return workbenchSpawnA;

            if (hasB)
                return workbenchSpawnB;

            return null;
        }

        private int GetActiveWorkbenchCount()
        {
            return workbenchSpawnA != null && workbenchSpawnB != null ? 2 : 1;
        }

        private void SpawnDefaultElectrons()
        {
            Transform origin = defaultElectronSpawnOrigin != null ? defaultElectronSpawnOrigin : null;

            for (int i = 0; i < defaultElectronCount; i++)
            {
                Vector3 position = origin != null
                    ? origin.position + defaultElectronStartOffset + defaultElectronSpacing * i
                    : GetWorkbenchSpawnPosition();
                GameObject electron = CreateParticleBall($"Electron ({i + 1})", IngredientType.Electron, Color.green, electronRadius, position);
                spawnedOutputs.Add(electron);
            }
        }

        private void SpawnDefaultQuarks()
        {
            Transform origin = defaultQuarkSpawnOrigin != null ? defaultQuarkSpawnOrigin : null;

            for (int i = 0; i < defaultQuarkUpCount; i++)
            {
                Vector3 position = origin != null
                    ? origin.position + defaultQuarkStartOffset + defaultQuarkSpacing * i
                    : GetWorkbenchSpawnPosition();
                GameObject quark = CreateParticleBall($"QuarkUp ({i + 1})", IngredientType.QuarkUp, Color.red, quarkRadius, position);
                spawnedOutputs.Add(quark);
            }

            for (int i = 0; i < defaultQuarkDownCount; i++)
            {
                Vector3 rowOffset = defaultQuarkStartOffset + new Vector3(0f, 0f, 0.14f);
                Vector3 position = origin != null
                    ? origin.position + rowOffset + defaultQuarkSpacing * i
                    : GetWorkbenchSpawnPosition();
                GameObject quark = CreateParticleBall($"QuarkDown ({i + 1})", IngredientType.QuarkDown, Color.blue, quarkRadius, position);
                spawnedOutputs.Add(quark);
            }
        }

        private void CompleteRecipe()
        {
            onRecipeCompleted?.Invoke();
        }

        private CraftRecipe FindMatchingRecipe()
        {
            for (int i = 0; i < builtInRecipes.Length; i++)
            {
                if (RecipeMatches(builtInRecipes[i]))
                    return builtInRecipes[i];
            }

            return null;
        }

        private bool RecipeMatches(CraftRecipe recipe)
        {
            for (int i = 0; i < recipe.entries.Length; i++)
            {
                currentCounts.TryGetValue(recipe.entries[i].ingredientType, out int currentValue);

                if (currentValue != recipe.entries[i].requiredCount)
                    return false;
            }

            foreach (var pair in currentCounts)
            {
                if (pair.Value <= 0)
                    continue;

                if (!RecipeContainsIngredient(recipe, pair.Key))
                    return false;
            }

            return true;
        }

        private bool CanStillMatchAnyRecipe()
        {
            for (int i = 0; i < builtInRecipes.Length; i++)
            {
                if (CanStillMatchRecipe(builtInRecipes[i]))
                    return true;
            }

            return false;
        }

        private bool CanStillMatchRecipe(CraftRecipe recipe)
        {
            foreach (var pair in currentCounts)
            {
                if (pair.Value <= 0)
                    continue;

                if (!TryGetRequiredCount(recipe, pair.Key, out int requiredCount))
                    return false;

                if (pair.Value > requiredCount)
                    return false;
            }

            return true;
        }

        private bool RecipeContainsIngredient(CraftRecipe recipe, IngredientType type)
        {
            return TryGetRequiredCount(recipe, type, out _);
        }

        private bool TryGetRequiredCount(CraftRecipe recipe, IngredientType type, out int requiredCount)
        {
            for (int i = 0; i < recipe.entries.Length; i++)
            {
                if (recipe.entries[i].ingredientType == type)
                {
                    requiredCount = recipe.entries[i].requiredCount;
                    return true;
                }
            }

            requiredCount = 0;
            return false;
        }

        private void StartTimingMiniGame(CraftRecipe recipe)
        {
            pendingRecipe = recipe;
            timingMiniGameActive = true;
            timingSuccessCount = 0;
            cursorPosition = 0f;
            cursorDirection = 1;
            xrTimingButtonWasHeld = false;

            UpdateTimingFeedback("Recette valide ! Mini-jeu garde pour plus tard : appuie quand le curseur est dans la zone verte.");
        }

        private bool TimingButtonWasPressed()
        {
            bool xrButtonIsHeld = IsXRTimingButtonHeld();
            bool xrButtonWasPressed = xrButtonIsHeld && !xrTimingButtonWasHeld;
            xrTimingButtonWasHeld = xrButtonIsHeld;

            if (xrButtonWasPressed)
                return true;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null
                && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(timingKey)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetKeyDown(KeyCode.JoystickButton14)
                || Input.GetKeyDown(KeyCode.JoystickButton15))
            {
                return true;
            }
#endif

            return false;
        }

        private bool IsXRTimingButtonHeld()
        {
            return IsXRControllerButtonHeld(UnityEngine.XR.XRNode.LeftHand)
                || IsXRControllerButtonHeld(UnityEngine.XR.XRNode.RightHand);
        }

        private bool IsXRControllerButtonHeld(UnityEngine.XR.XRNode node)
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

        public void PressTimingButton()
        {
            if (!timingMiniGameActive)
                return;

            if (IsCursorInSuccessZone())
            {
                timingSuccessCount++;

                if (timingSuccessCount >= timingSuccessesRequired)
                {
                    timingMiniGameActive = false;

                    if (pendingRecipe != null)
                    {
                        SpawnRecipeOutput(pendingRecipe);
                        SetText($"Mini-jeu reussi ! {pendingRecipe.displayName} cree sur la workbench.");
                    }

                    pendingRecipe = null;
                    currentCounts.Clear();
                    Invoke(nameof(CompleteRecipe), successDelay);
                    return;
                }

                cursorPosition = 0f;
                cursorDirection = 1;
                UpdateTimingFeedback("Reussi !");
                return;
            }

            if (resetProgressOnMiss)
                timingSuccessCount = 0;

            UpdateTimingFeedback("Rate ! Recommence le timing.");
        }

        private void UpdateTimingCursor()
        {
            cursorPosition += Time.deltaTime * cursorSpeed * cursorDirection;

            if (cursorPosition >= 1f)
            {
                cursorPosition = 1f;
                cursorDirection = -1;
            }
            else if (cursorPosition <= 0f)
            {
                cursorPosition = 0f;
                cursorDirection = 1;
            }
        }

        private bool IsCursorInSuccessZone()
        {
            float halfZone = successZoneSize * 0.5f;
            return cursorPosition >= successZoneCenter - halfZone
                && cursorPosition <= successZoneCenter + halfZone;
        }

        private void UpdateTimingFeedback(string prefix)
        {
            string message = string.IsNullOrEmpty(prefix) ? string.Empty : prefix + "\n";

            SetText(
                message +
                $"Timing {timingSuccessCount}/{timingSuccessesRequired}\n" +
                BuildTimingBar() +
                "\nAppuie au bon moment."
            );
        }

        private string BuildTimingBar()
        {
            int cursorIndex = Mathf.RoundToInt(cursorPosition * (TimingBarSegments - 1));
            float halfZone = successZoneSize * 0.5f;
            int zoneStart = Mathf.RoundToInt(Mathf.Clamp01(successZoneCenter - halfZone) * (TimingBarSegments - 1));
            int zoneEnd = Mathf.RoundToInt(Mathf.Clamp01(successZoneCenter + halfZone) * (TimingBarSegments - 1));

            System.Text.StringBuilder bar = new System.Text.StringBuilder(TimingBarSegments + 2);
            bar.Append('[');

            for (int i = 0; i < TimingBarSegments; i++)
            {
                if (i == cursorIndex)
                    bar.Append("<color=#FFD400>|</color>");
                else if (i >= zoneStart && i <= zoneEnd)
                    bar.Append("<color=#00FF66>O</color>");
                else
                    bar.Append("<color=#FF4040>-</color>");
            }

            bar.Append(']');
            return bar.ToString();
        }

        private IngredientBall GetValidIngredient(Collider other)
        {
            IngredientBall[] ingredients = other.GetComponents<IngredientBall>();

            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null && ingredients[i].Type != IngredientType.None)
                    return ingredients[i];
            }

            return null;
        }

        public void ResetBucket()
        {
            currentCounts.Clear();
            pendingRecipe = null;
            timingMiniGameActive = false;
            timingSuccessCount = 0;
            cursorPosition = 0f;
            cursorDirection = 1;
            xrTimingButtonWasHeld = false;

            if (clearSpawnedOutputsOnReset)
                ClearSpawnedOutputs();

            UpdateFeedback();
        }

        private void ClearSpawnedOutputs()
        {
            for (int i = spawnedOutputs.Count - 1; i >= 0; i--)
            {
                if (spawnedOutputs[i] != null)
                    Destroy(spawnedOutputs[i]);
            }

            spawnedOutputs.Clear();
        }

        private void UpdateFeedback()
        {
            if (targetRecipe != null)
            {
                SetText($"Objectif : {targetRecipe.GetRecipeDescription()}\n{BuildCurrentContentsText()}");
                return;
            }

            SetText(
                "Recettes :\n" +
                "Proton = 2 Up + 1 Down (uud)\n" +
                "Neutron = 1 Up + 2 Down (udd)\n" +
                "Helium = 2 Protons + 2 Neutrons + 2 Electrons\n" +
                BuildCurrentContentsText()
            );
        }

        private string BuildCurrentContentsText()
        {
            return "Dans le recipient : " +
                $"Up={GetCount(IngredientType.QuarkUp)}, " +
                $"Down={GetCount(IngredientType.QuarkDown)}, " +
                $"p={GetCount(IngredientType.Proton)}, " +
                $"n={GetCount(IngredientType.Neutron)}, " +
                $"e={GetCount(IngredientType.Electron)}";
        }

        private int GetCount(IngredientType type)
        {
            currentCounts.TryGetValue(type, out int count);
            return count;
        }

        private void SetText(string message)
        {
            if (feedbackText != null)
                feedbackText.text = message;
        }
    }
}
