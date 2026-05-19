using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.SceneManagement;
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
        [SerializeField] private bool spawnDefaultQuarksOnStart = false;
        [SerializeField] private int defaultQuarkUpCount = 6;
        [SerializeField] private int defaultQuarkDownCount = 6;
        [SerializeField] private float quarkRadius = 0.055f;
        [SerializeField] private Transform defaultQuarkSpawnOrigin;
        [SerializeField] private Vector3 defaultQuarkStartOffset = new Vector3(-0.42f, 0.18f, -0.12f);
        [SerializeField] private Vector3 defaultQuarkSpacing = new Vector3(0.12f, 0f, 0f);
        [SerializeField] private bool spawnDefaultElectronsOnStart = false;
        [SerializeField] private int defaultElectronCount = 2;
        [SerializeField] private float electronRadius = 0.045f;
        [SerializeField] private Vector3 defaultElectronStartOffset = new Vector3(-0.22f, 0.18f, 0.18f);
        [SerializeField] private Vector3 defaultElectronSpacing = new Vector3(0.14f, 0f, 0f);

        [Header("Settings")]
        [SerializeField] private bool consumeIngredientOnEnter = true;
        [SerializeField] private bool requireMixerToolPress = true;
        [SerializeField] private string mixerToolName = "PaintCan";
        [SerializeField] private float mixerPressCooldown = 0.45f;
        [SerializeField] private float successDelay = 0.2f;
        [SerializeField] private bool clearSpawnedOutputsOnReset = false;

        [Header("Recipe Mini Game")]
        [SerializeField] private float shakeMiniGameDuration = 5f;
        [SerializeField] private int requiredShakeDirectionChanges = 4;
        [SerializeField] private float requiredShakeTravel = 0.45f;
        [SerializeField] private float shakeDirectionThreshold = 0.035f;
        [SerializeField] private string mixingClipName = "melange";
        [SerializeField] private float mixingClipVolume = 0.9f;
        [SerializeField] private bool balancedFollowUpMiniGames = true;
        [SerializeField, Range(0f, 1f)] private float coolingMiniGameChance = 1f;
        [SerializeField] private float coolingMiniGameDuration = 5f;
        [SerializeField, Range(0f, 1f)] private float timingMiniGameChance = 1f;
        [SerializeField] private float timingMiniGameDuration = 4f;
        [SerializeField] private int timingMiniGameSteps = 3;
        [SerializeField] private float timingCursorSpeed = 1.4f;
        [SerializeField] private float timingCursorSpeedMultiplier = 0.7f;
        [SerializeField, Range(0.05f, 0.8f)] private float timingGreenZoneSize = 0.22f;
        [SerializeField] private string timingSuccessClipName = "success";
        [SerializeField] private float timingSuccessClipDuration = 1.4f;
        [SerializeField] private float timingSuccessClipVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float memoryMiniGameChance = 1f;
        [SerializeField] private float memoryRevealDuration = 2f;
        [SerializeField] private float memorySolveDuration = 8f;
        [SerializeField] private float memoryNumberFontSize = 11f;
        [SerializeField] private float memoryNumberScale = 0.24f;

        [Header("Wrong Recipe Explosion")]
        [SerializeField] private float explosionEffectHeight = 0.45f;
        [SerializeField] private float explosionEffectDuration = 0.75f;
        [SerializeField] private float explosionEffectRadius = 0.6f;
        [SerializeField] private float explosionVolume = 0.75f;

        [Header("Bucket Contents Label")]
        [SerializeField] private Vector3 contentsLabelOffset = new Vector3(0f, 0.30f, 0f);
        [SerializeField] private float contentsLabelFontSize = 0.32f;
        [SerializeField] private float absorbRadius = 1.25f;
        [SerializeField] private float absorbScanInterval = 0.12f;

        [Header("Events")]
        public UnityEvent onRecipeCompleted;
        public UnityEvent onWrongRecipe;

        private readonly Dictionary<IngredientType, int> currentCounts = new();
        private readonly List<GameObject> spawnedOutputs = new();
        private readonly HashSet<GameObject> protectedRecipeOutputs = new();
        private static readonly HashSet<IngredientType> completedRecipeOutputs = new();
        private static readonly List<FollowUpMiniGame> balancedFollowUpMiniGameBag = new List<FollowUpMiniGame>();
        private CraftRecipe[] builtInRecipes;
        private int workbenchSpawnIndex = 0;
        private AudioClip generatedExplosionClip;
        private float nextMixerPressTime = 0f;
        private TMP_Text contentsLabel;
        private float nextAbsorbScanTime = 0f;
        private float startupGraceEndTime = 0f;
        private bool isDead = false;
        private Text deathTextUI;
        private Transform originalCameraParent;
        private Vector3 originalCameraLocalPos;
        private Quaternion originalCameraLocalRot;
        private bool deathMovedCamera;
        private bool deathMovementLocked;
        private Transform deathVrOrigin;
        private XROrigin deathVrRig;
        private CharacterController deathVrCharacterController;
        private bool deathVrCharacterControllerWasEnabled;
        private bool deathPreparedVrRig;
        private float deathInputEnabledTime;
        private bool isPaintCanTriggerZone = false;
        private bool isRecipeMiniGameActive = false;
        private Coroutine recipeMiniGameRoutine;
        private AudioSource mixingAudioSource;
        private AudioClip mixingClip;
        private AudioClip timingSuccessClip;
        private bool leftTimingButtonWasPressed;
        private bool rightTimingButtonWasPressed;
        private bool memoryMiniGameInputActive;
        private int memoryMiniGameExpectedStep;
        private int memoryMiniGameSelectedCell = -1;
        private float nextUraniumRejectFeedbackTime;

        private enum ShakeAxis
        {
            Horizontal,
            Vertical
        }

        private enum FollowUpMiniGame
        {
            None,
            Cooling,
            Timing,
            Memory
        }

        public static void ResetCompletedRecipes()
        {
            completedRecipeOutputs.Clear();
            balancedFollowUpMiniGameBag.Clear();
        }

        private void Awake()
        {
            Debug.Log($"[BucketAssembler] Awake on '{name}' (parent: {(transform.parent != null ? transform.parent.name : "none")})");
            startupGraceEndTime = Time.time + 3f;
            spawnDefaultQuarksOnStart = false;
            spawnDefaultElectronsOnStart = false;
            contentsLabelOffset = new Vector3(0f, 0.3f, 0f);
            contentsLabelFontSize = 0.42f;
            // Keep absorbRadius small to avoid consuming quarks already on the table
            absorbRadius = Mathf.Clamp(absorbRadius, 0.1f, 0.4f);

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

                new CraftRecipe(
                    "Uranium",
                    "92p + 146n + 92e",
                    IngredientType.Uranium,
                    new Color(0.35f, 1f, 0.25f),
                    0.22f,
                    new RecipeEntry(IngredientType.Proton, 92),
                    new RecipeEntry(IngredientType.Neutron, 146),
                    new RecipeEntry(IngredientType.Electron, 92)),
            };

            mixingClip = Resources.Load<AudioClip>(mixingClipName);

            if (mixingClip == null)
                Debug.LogWarning($"[BucketAssembler] Mixing sound not found. Expected Assets/Resources/{mixingClipName}.mp3");

            timingSuccessClip = Resources.Load<AudioClip>(timingSuccessClipName);

            if (timingSuccessClip == null)
                Debug.LogWarning($"[BucketAssembler] Timing success sound not found. Expected Assets/Resources/{timingSuccessClipName}.mp3");
        }

        private void Start()
        {
            // Allow manual mixing by clicking
            requireMixerToolPress = true;

            DestroyPrePlacedIngredients();
            EnsureTriggerSetup();
            EnsureWaterTriggers();
            ResolveWorkbenchSpawnPoints();
            BuildContentsLabel();
            Debug.Log($"[BucketAssembler] Start on '{name}'. Absorb radius: {absorbRadius}, position: {transform.position}");

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

        private void OnDestroy()
        {
            StopMixingSound();
            DestroyContentsLabel();

            if (deathMovementLocked)
            {
                PlayerMovementLock.Unlock("recipe death destroyed");
                deathMovementLocked = false;
            }

            RestoreVrDeathState();
        }

        /// <summary>
        /// Removes all pre-placed IngredientBall objects from the scene at startup.
        /// Only spawned quarks from the worktable should exist.
        /// </summary>
        private void DestroyPrePlacedIngredients()
        {
            IngredientBall[] existing = FindObjectsOfType<IngredientBall>(true);
            int removed = 0;

            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null)
                    continue;

                Debug.Log($"[BucketAssembler] Removing pre-placed ingredient: '{existing[i].name}'");
                Destroy(existing[i].gameObject);
                removed++;
            }

            if (removed > 0)
                Debug.Log($"[BucketAssembler] Removed {removed} pre-placed ingredient(s)");
        }

        private void EnsureTriggerSetup()
        {
            isPaintCanTriggerZone = transform.parent != null
                && IsPaintCanName(transform.parent.name);

            Collider[] colliders = GetComponents<Collider>();
            bool hasTrigger = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].isTrigger)
                {
                    hasTrigger = true;
                }
            }

            if (!hasTrigger)
            {
                BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                Debug.Log($"[BucketAssembler] Added trigger BoxCollider to {name}");
            }

            if (isPaintCanTriggerZone)
            {
                Rigidbody triggerBody = GetComponent<Rigidbody>();
                if (triggerBody != null)
                {
                    Destroy(triggerBody);
                    Debug.Log($"[BucketAssembler] Removed Rigidbody from paintcan TriggerZone '{name}'");
                }
            }
            else
            {
                // Ensure a Rigidbody exists on THIS object (required for OnTriggerEnter)
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                    Debug.Log($"[BucketAssembler] Added kinematic Rigidbody to {name}");
                }
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Also ensure parent has a Rigidbody (for compound collider setups)
            if (transform.parent != null && !isPaintCanTriggerZone)
            {
                Rigidbody parentRb = transform.parent.GetComponent<Rigidbody>();
                if (parentRb == null)
                {
                    parentRb = transform.parent.gameObject.AddComponent<Rigidbody>();
                    Debug.Log($"[BucketAssembler] Added kinematic Rigidbody to parent {transform.parent.name}");
                }

                // Force it to be kinematic to prevent falling through the floor
                parentRb.isKinematic = true;
                parentRb.useGravity = false;
            }

            Debug.Log($"[BucketAssembler] Trigger setup complete. name='{name}', parent='{(transform.parent != null ? transform.parent.name : "none")}', isPaintCanTriggerZone={isPaintCanTriggerZone}, position={transform.position}, localPosition={transform.localPosition}, colliders={GetComponents<Collider>().Length}, hasRigidbody={GetComponent<Rigidbody>() != null}");
        }

        private void EnsureWaterTriggers()
        {
            Collider[] colliders = FindObjectsOfType<Collider>(true);
            int changed = 0;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !IsWaterObject(colliders[i].transform))
                    continue;

                if (!colliders[i].isTrigger)
                {
                    colliders[i].isTrigger = true;
                    changed++;
                }
            }

            if (changed > 0)
                Debug.Log($"[BucketAssembler] Converted {changed} water collider(s) to triggers so PaintCan can pass through.");
        }

        private bool IsWaterObject(Transform source)
        {
            Transform current = source;

            while (current != null)
            {
                string objectName = current.name;

                if (!string.IsNullOrWhiteSpace(objectName)
                    && objectName.StartsWith("Eau", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void LateUpdate()
        {
            if (contentsLabel == null || Camera.main == null)
                return;

            contentsLabel.transform.position = transform.position + contentsLabelOffset;
            Vector3 directionToCamera = contentsLabel.transform.position - Camera.main.transform.position;

            if (directionToCamera.sqrMagnitude > 0.0001f)
                contentsLabel.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
        }

        private void Update()
        {
            if (isDead)
            {
                bool pressed = false;

                if (Time.time < deathInputEnabledTime)
                    return;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    pressed = true;
                if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
                    pressed = true;
#else
                if (Input.anyKeyDown)
                    pressed = true;
#endif
                if (DeathRestartPressedInXR())
                    pressed = true;

                if (pressed)
                {
                    if (deathTextUI != null)
                        deathTextUI.text = "Chargement en cours...";
                    RestartScene();
                }
                return;
            }

            if (Time.time < startupGraceEndTime)
                return;

            ScanForIngredientsNearBucket();
        }

        private void ScanForIngredientsNearBucket()
        {
            if (Time.time < nextAbsorbScanTime)
                return;

            nextAbsorbScanTime = Time.time + absorbScanInterval;
            Bounds absorbBounds = GetAbsorbBounds();
            Collider[] colliders = Physics.OverlapBox(
                absorbBounds.center,
                absorbBounds.extents,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i].transform == transform)
                    continue;

                if (IsPlayerOrInteractorCollider(colliders[i]))
                    continue;

                if (IsMixerTool(colliders[i]))
                    continue;

                ProcessIngredientCollider(colliders[i]);
            }

            ScanIngredientComponentsByDistance(absorbBounds);
        }

        private void ScanIngredientComponentsByDistance(Bounds absorbBounds)
        {
            IngredientBall[] ingredients = FindObjectsOfType<IngredientBall>();

            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] == null || ingredients[i].IsConsumed)
                    continue;

                if (protectedRecipeOutputs.Contains(ingredients[i].gameObject))
                    continue;

                Vector3 position = ingredients[i].transform.position;

                if (!absorbBounds.Contains(position))
                    continue;

                ParticlePacket packet = ingredients[i].GetComponentInParent<ParticlePacket>();
                AddIngredient(ingredients[i], packet);
            }
        }

        private Bounds GetAbsorbBounds()
        {
            Collider[] ownColliders = GetComponentsInChildren<Collider>();

            if (ownColliders.Length == 0)
                return new Bounds(transform.position, Vector3.one * (absorbRadius * 2f));

            Bounds bounds = ownColliders[0].bounds;

            for (int i = 1; i < ownColliders.Length; i++)
                bounds.Encapsulate(ownColliders[i].bounds);

            bounds.Expand(absorbRadius);
            return bounds;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < startupGraceEndTime) return;
            if (ShouldIgnorePassiveEnvironment(other)) return;

            Debug.Log($"[BucketAssembler] OnTriggerEnter: {other.name} (root: {other.transform.root.name})");
            ProcessTriggerCollider(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < startupGraceEndTime) return;
            if (collision == null || ShouldIgnorePassiveEnvironment(collision.collider)) return;

            Debug.Log($"[BucketAssembler] OnCollisionEnter: {collision.collider.name} (root: {collision.transform.root.name})");
            ProcessIngredientCollider(collision.collider);
        }

        private void OnTriggerStay(Collider other)
        {
            if (Time.time < startupGraceEndTime) return;
            if (ShouldIgnorePassiveEnvironment(other)) return;

            ProcessIngredientCollider(other);
        }

        private bool ShouldIgnorePassiveEnvironment(Collider other)
        {
            if (other == null)
                return true;

            if (IsPlayerOrInteractorCollider(other))
                return true;

            if (other.attachedRigidbody != null)
                return false;

            if (IsMixerTool(other) || IsCardboxBaseCollider(other))
                return false;

            if (other.GetComponentInParent<IngredientBall>() != null
                || other.GetComponentInChildren<IngredientBall>() != null
                || other.GetComponentInParent<ParticlePacket>() != null
                || other.GetComponentInChildren<ParticlePacket>() != null)
            {
                return false;
            }

            return InferIngredientType(other.transform) == IngredientType.None;
        }

        private void ProcessTriggerCollider(Collider other)
        {
            if (IsCardboxBaseCollider(other))
                return;

            if (IsMixerTool(other))
            {
                if (!requireMixerToolPress)
                    TryMixCurrentContents();
                else if (currentCounts.Count > 0)
                    SetText("Ingredients ajoutes. Appuie sur la PaintCan pour melanger.\n" + BuildCurrentContentsText());

                return;
            }

            ProcessIngredientCollider(other);
        }

        private void ProcessIngredientCollider(Collider other)
        {
            if (IsPlayerOrInteractorCollider(other))
                return;

            if (IsCardboxBaseCollider(other))
                return;

            IngredientBall ingredient = GetValidIngredient(other);
            if (ingredient == null)
                return;
            if (ingredient.IsConsumed)
            {
                Debug.Log($"[BucketAssembler] Ingredient already consumed: {ingredient.DisplayName}");
                return;
            }
            if (protectedRecipeOutputs.Contains(ingredient.gameObject))
            {
                Debug.Log($"[BucketAssembler] Ingredient is protected output: {ingredient.DisplayName}");
                return;
            }

            ParticlePacket packet = GetParticlePacket(other);
            IngredientType type = packet != null ? packet.Type : ingredient.Type;

            if (!CanAcceptIngredientType(type))
                return;

            Debug.Log($"[BucketAssembler] Consuming ingredient: {ingredient.DisplayName} (type={type})");
            AddIngredient(ingredient, packet);
        }

        private void AddIngredient(IngredientBall ingredient, ParticlePacket packet)
        {
            IngredientType type = packet != null ? packet.Type : ingredient.Type;
            int amount = packet != null ? packet.Count : 1;

            if (!CanAcceptIngredientType(type))
                return;

            if (!currentCounts.ContainsKey(type))
                currentCounts[type] = 0;

            currentCounts[type] += amount;
            Debug.Log($"Bucket absorbed {amount} x {type}. {BuildCurrentContentsText()}");

            if (consumeIngredientOnEnter)
                ingredient.Consume();

            UpdateContentsLabel();

            if (requireMixerToolPress)
            {
                SetText("Ingredients ajoutes. Appuie sur le recipient avec une PaintCan pour melanger.\n" + BuildCurrentContentsText());
                return;
            }

            TryMixCurrentContents();
        }

        public void TryMixCurrentContents()
        {
            if (isRecipeMiniGameActive)
            {
                SetText("Mini-jeu en cours : secoue la PaintCan horizontalement.");
                return;
            }

            nextMixerPressTime = Time.time + mixerPressCooldown;

            if (currentCounts.Count == 0)
            {
                SetText("Le recipient est vide. Ajoute des particules avant de melanger.");
                return;
            }

            CraftRecipe matchingRecipe = FindMatchingRecipe();

            if (matchingRecipe != null)
            {
                if (IsRecipeAlreadyKnown(matchingRecipe))
                {
                    SetText($"Recette deja faite : {matchingRecipe.displayName}. Tu ne peux pas refaire la meme recette deux fois.\n" + BuildCurrentContentsText());
                    Debug.Log($"[BucketAssembler] Duplicate recipe ignored before mini-game: {matchingRecipe.displayName} ({matchingRecipe.outputType}).");
                    return;
                }

                StartRecipeMiniGame(matchingRecipe);
                return;
            }

            if (!CanStillMatchAnyRecipe())
            {
                FailRecipeWithExplosion("Mauvaise recette ! Explosion.");
                return;
            }

            SetText("Recette incomplete. Ajoute les bonnes particules puis melange encore.\n" + BuildCurrentContentsText());
        }

        private void StartRecipeMiniGame(CraftRecipe recipe)
        {
            GameObject paintCan = ResolveOwningPaintCan();

            if (paintCan == null)
            {
                Debug.LogWarning("[BucketAssembler] Cannot start shake mini-game: owning PaintCan not found. Completing craft directly.");
                CompleteCraft(recipe);
                return;
            }

            if (recipeMiniGameRoutine != null)
                StopCoroutine(recipeMiniGameRoutine);

            recipeMiniGameRoutine = StartCoroutine(RecipeShakeMiniGameRoutine(recipe, paintCan.transform));
        }

        private IEnumerator RecipeShakeMiniGameRoutine(CraftRecipe recipe, Transform shakeTarget)
        {
            isRecipeMiniGameActive = true;
            ShakeAxis shakeAxis = Random.value < 0.5f ? ShakeAxis.Horizontal : ShakeAxis.Vertical;
            float endTime = Time.time + shakeMiniGameDuration;
            Vector3 previousPosition = shakeTarget.position;
            float previousDirection = 0f;
            float totalTravel = 0f;
            int shakeCount = 0;
            PlayMixingSound(shakeTarget.position);

            Debug.Log($"[BucketAssembler] Shake mini-game started for {recipe.displayName}. axis={shakeAxis}, duration={shakeMiniGameDuration}s target={shakeTarget.name}");

            while (Time.time < endTime && shakeTarget != null)
            {
                Vector3 currentPosition = shakeTarget.position;
                Vector3 delta = currentPosition - previousPosition;
                float axisDelta = shakeAxis == ShakeAxis.Horizontal
                    ? new Vector2(delta.x, delta.z).magnitude * Mathf.Sign(Mathf.Abs(delta.x) >= Mathf.Abs(delta.z) ? delta.x : delta.z)
                    : delta.y;

                float distance = Mathf.Abs(axisDelta);

                if (distance >= shakeDirectionThreshold)
                {
                    float direction = Mathf.Sign(axisDelta);
                    totalTravel += distance;

                    if (Mathf.Abs(previousDirection) > 0.01f && !Mathf.Approximately(previousDirection, direction))
                        shakeCount++;

                    previousDirection = direction;
                    previousPosition = currentPosition;
                }

                float remaining = Mathf.Max(0f, endTime - Time.time);
                SetMiniGameText(shakeAxis, remaining, shakeCount, totalTravel);

                if (shakeCount >= requiredShakeDirectionChanges && totalTravel >= requiredShakeTravel)
                {
                    StopMixingSound();
                    Debug.Log($"[BucketAssembler] Shake mini-game succeeded. axis={shakeAxis}, shakes={shakeCount}, travel={totalTravel:F2}");

                    FollowUpMiniGame followUpMiniGame = ChooseFollowUpMiniGame();
                    Debug.Log($"[BucketAssembler] Follow-up mini-game selected: {followUpMiniGame}");

                    if (followUpMiniGame == FollowUpMiniGame.Cooling)
                    {
                        bool cooled = false;
                        yield return CoolingMiniGameRoutine(shakeTarget, value => cooled = value);

                        if (!cooled)
                        {
                            isRecipeMiniGameActive = false;
                            recipeMiniGameRoutine = null;
                            FailRecipeWithExplosion("Refroidissement rate ! La recette explose.");
                            yield break;
                        }
                    }
                    else if (followUpMiniGame == FollowUpMiniGame.Timing)
                    {
                        bool timed = false;
                        yield return TimingMiniGameRoutine(shakeTarget, value => timed = value);

                        if (!timed)
                        {
                            isRecipeMiniGameActive = false;
                            recipeMiniGameRoutine = null;
                            FailRecipeWithExplosion("Timing rate ! La recette explose.");
                            yield break;
                        }
                    }
                    else if (followUpMiniGame == FollowUpMiniGame.Memory)
                    {
                        bool memorized = false;
                        yield return MemoryMiniGameRoutine(shakeTarget, value => memorized = value);

                        if (!memorized)
                        {
                            isRecipeMiniGameActive = false;
                            recipeMiniGameRoutine = null;
                            FailRecipeWithExplosion("Memoire ratee ! La recette explose.");
                            yield break;
                        }
                    }

                    isRecipeMiniGameActive = false;
                    recipeMiniGameRoutine = null;
                    CompleteCraft(recipe);
                    yield break;
                }

                yield return null;
            }

            isRecipeMiniGameActive = false;
            recipeMiniGameRoutine = null;
            StopMixingSound();
            Debug.Log($"[BucketAssembler] Shake mini-game failed. axis={shakeAxis}, shakes={shakeCount}, travel={totalTravel:F2}");
            FailRecipeWithExplosion("Mini-jeu rate ! La recette explose.");
        }

        private FollowUpMiniGame ChooseFollowUpMiniGame()
        {
            if (balancedFollowUpMiniGames)
                return DrawBalancedFollowUpMiniGame();

            float timingChance = Mathf.Clamp01(timingMiniGameChance);
            float coolingChance = Mathf.Clamp01(coolingMiniGameChance);
            float memoryChance = Mathf.Clamp01(memoryMiniGameChance);
            float totalChance = timingChance + coolingChance + memoryChance;

            if (totalChance <= 0.001f)
                return FollowUpMiniGame.None;

            float roll = Random.value * totalChance;

            if (roll < timingChance)
                return FollowUpMiniGame.Timing;

            if (roll < timingChance + coolingChance)
                return FollowUpMiniGame.Cooling;

            return FollowUpMiniGame.Memory;
        }

        private FollowUpMiniGame DrawBalancedFollowUpMiniGame()
        {
            if (balancedFollowUpMiniGameBag.Count == 0)
                RefillBalancedFollowUpMiniGameBag();

            int lastIndex = balancedFollowUpMiniGameBag.Count - 1;
            FollowUpMiniGame selected = balancedFollowUpMiniGameBag[lastIndex];
            balancedFollowUpMiniGameBag.RemoveAt(lastIndex);
            return selected;
        }

        private static void RefillBalancedFollowUpMiniGameBag()
        {
            balancedFollowUpMiniGameBag.Clear();
            balancedFollowUpMiniGameBag.Add(FollowUpMiniGame.Cooling);
            balancedFollowUpMiniGameBag.Add(FollowUpMiniGame.Timing);
            balancedFollowUpMiniGameBag.Add(FollowUpMiniGame.Memory);

            for (int i = balancedFollowUpMiniGameBag.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                FollowUpMiniGame temp = balancedFollowUpMiniGameBag[i];
                balancedFollowUpMiniGameBag[i] = balancedFollowUpMiniGameBag[swapIndex];
                balancedFollowUpMiniGameBag[swapIndex] = temp;
            }
        }

        private IEnumerator CoolingMiniGameRoutine(Transform paintCan, System.Action<bool> onCompleted)
        {
            float endTime = Time.time + coolingMiniGameDuration;
            Debug.Log($"[BucketAssembler] Cooling mini-game started. target={paintCan.name}, duration={coolingMiniGameDuration}s");

            while (Time.time < endTime && paintCan != null)
            {
                float remaining = Mathf.Max(0f, endTime - Time.time);
                SetCoolingMiniGameText(remaining);

                if (IsPaintCanInWater(paintCan))
                {
                    Debug.Log("[BucketAssembler] Cooling mini-game succeeded.");
                    onCompleted?.Invoke(true);
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[BucketAssembler] Cooling mini-game failed.");
            onCompleted?.Invoke(false);
        }

        private IEnumerator TimingMiniGameRoutine(Transform paintCan, System.Action<bool> onCompleted)
        {
            int totalSteps = Mathf.Max(1, timingMiniGameSteps);

            for (int step = 1; step <= totalSteps; step++)
            {
                bool stepSucceeded = false;
                yield return TimingMiniGameStepRoutine(paintCan, step, totalSteps, value => stepSucceeded = value);

                if (!stepSucceeded)
                {
                    onCompleted?.Invoke(false);
                    yield break;
                }

                PlayTimingSuccessSound(paintCan != null ? paintCan.position : transform.position);

                if (step < totalSteps)
                {
                    SetText($"Timing reussi {step}/{totalSteps} !");
                    yield return new WaitForSeconds(0.25f);
                }
            }

            onCompleted?.Invoke(true);
        }

        private IEnumerator TimingMiniGameStepRoutine(Transform paintCan, int step, int totalSteps, System.Action<bool> onCompleted)
        {
            float endTime = Time.time + timingMiniGameDuration;
            float cursor = 0f;
            int cursorDirection = 1;
            float greenZoneCenter = Random.Range(timingGreenZoneSize * 0.5f, 1f - timingGreenZoneSize * 0.5f);
            GameObject timingBar = BuildTimingBar(paintCan, greenZoneCenter, out Transform cursorTransform);

            Debug.Log($"[BucketAssembler] Timing mini-game step {step}/{totalSteps} started. duration={timingMiniGameDuration}s, greenCenter={greenZoneCenter:F2}, greenSize={timingGreenZoneSize:F2}");

            while (Time.time < endTime && paintCan != null)
            {
                cursor += cursorDirection * timingCursorSpeed * timingCursorSpeedMultiplier * Time.deltaTime;

                if (cursor >= 1f)
                {
                    cursor = 1f;
                    cursorDirection = -1;
                }
                else if (cursor <= 0f)
                {
                    cursor = 0f;
                    cursorDirection = 1;
                }

                UpdateTimingBar(timingBar, cursorTransform, paintCan, cursor);
                float remaining = Mathf.Max(0f, endTime - Time.time);
                SetTimingMiniGameText(remaining, step, totalSteps);

                if (MiniGameInteractPressedThisFrame())
                {
                    bool success = Mathf.Abs(cursor - greenZoneCenter) <= timingGreenZoneSize * 0.5f;
                    Destroy(timingBar);
                    Debug.Log($"[BucketAssembler] Timing mini-game step {step}/{totalSteps} input. cursor={cursor:F2}, success={success}");
                    onCompleted?.Invoke(success);
                    yield break;
                }

                yield return null;
            }

            Destroy(timingBar);
            Debug.LogWarning($"[BucketAssembler] Timing mini-game step {step}/{totalSteps} failed: timeout.");
            onCompleted?.Invoke(false);
        }

        private IEnumerator MemoryMiniGameRoutine(Transform paintCan, System.Action<bool> onCompleted)
        {
            int[] sequence = BuildMemorySequence();
            GameObject grid = BuildMemoryGrid(paintCan, sequence, true, out MemoryMiniGameCell[] cells, out TMP_Text[] labels);
            memoryMiniGameInputActive = false;
            memoryMiniGameExpectedStep = 0;
            memoryMiniGameSelectedCell = -1;

            Debug.Log($"[BucketAssembler] Memory mini-game reveal started. sequence={string.Join(",", sequence)} reveal={memoryRevealDuration}s");

            float revealEnd = Time.time + memoryRevealDuration;

            while (Time.time < revealEnd && paintCan != null)
            {
                UpdateMemoryGrid(grid, paintCan);
                SetMemoryMiniGameText(Mathf.Max(0f, revealEnd - Time.time), 0, sequence.Length, true);
                yield return null;
            }

            SetMemoryGridHidden(cells, labels);
            memoryMiniGameInputActive = true;
            float solveEnd = Time.time + memorySolveDuration;

            while (Time.time < solveEnd && paintCan != null)
            {
                UpdateMemoryGrid(grid, paintCan);
                SetMemoryMiniGameText(Mathf.Max(0f, solveEnd - Time.time), memoryMiniGameExpectedStep, sequence.Length, false);

                if (MiniGameInteractPressedThisFrame())
                    TrySelectMemoryCellFromAim();

                if (memoryMiniGameSelectedCell >= 0)
                {
                    int selectedCell = memoryMiniGameSelectedCell;
                    memoryMiniGameSelectedCell = -1;

                    if (selectedCell != sequence[memoryMiniGameExpectedStep])
                    {
                        Destroy(grid);
                        memoryMiniGameInputActive = false;
                        Debug.LogWarning($"[BucketAssembler] Memory mini-game failed. selected={selectedCell}, expected={sequence[memoryMiniGameExpectedStep]}, step={memoryMiniGameExpectedStep + 1}");
                        onCompleted?.Invoke(false);
                        yield break;
                    }

                    SetMemoryCellSolved(cells[selectedCell], labels[selectedCell], memoryMiniGameExpectedStep + 1);
                    memoryMiniGameExpectedStep++;

                    if (memoryMiniGameExpectedStep >= sequence.Length)
                    {
                        Destroy(grid);
                        memoryMiniGameInputActive = false;
                        Debug.Log("[BucketAssembler] Memory mini-game succeeded.");
                        onCompleted?.Invoke(true);
                        yield break;
                    }
                }

                yield return null;
            }

            Destroy(grid);
            memoryMiniGameInputActive = false;
            Debug.LogWarning("[BucketAssembler] Memory mini-game failed: timeout.");
            onCompleted?.Invoke(false);
        }

        public void SelectMemoryMiniGameCell(int cellIndex)
        {
            if (!memoryMiniGameInputActive)
                return;

            memoryMiniGameSelectedCell = cellIndex;
        }

        private bool TrySelectMemoryCellFromAim()
        {
            if (IsVrActive())
            {
                return TrySelectMemoryCellFromXRRayInteractor("Right")
                    || TrySelectMemoryCellFromXRNode(UnityEngine.XR.XRNode.RightHand, "RightHand XRNode");
            }

            if (TrySelectMemoryCellFromCamera())
                return true;

            return TrySelectMemoryCellFromXRRayInteractor("Right")
                || TrySelectMemoryCellFromXRNode(UnityEngine.XR.XRNode.RightHand, "RightHand XRNode")
                || TrySelectMemoryCellFromXRRayInteractor("Left")
                || TrySelectMemoryCellFromXRNode(UnityEngine.XR.XRNode.LeftHand, "LeftHand XRNode");
        }

        private bool TrySelectMemoryCellFromCamera()
        {
            Camera camera = Camera.main;

            if (camera == null)
                return false;

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            return TrySelectMemoryCellFromRay(ray, "Camera");
        }

        private bool TrySelectMemoryCellFromXRRayInteractor(string handName)
        {
            XRRayInteractor[] interactors = FindObjectsOfType<XRRayInteractor>(true);

            for (int i = 0; i < interactors.Length; i++)
            {
                XRRayInteractor interactor = interactors[i];

                if (interactor == null || !interactor.isActiveAndEnabled)
                    continue;

                if (!HasNameInParents(interactor.transform, handName))
                    continue;

                if (interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit)
                    && TrySelectMemoryCellFromCollider(hit.collider, $"{handName} XRRayInteractor hit"))
                {
                    return true;
                }

                Transform rayOrigin = interactor.rayOriginTransform != null
                    ? interactor.rayOriginTransform
                    : interactor.transform;

                if (rayOrigin != null
                    && TrySelectMemoryCellFromRay(new Ray(rayOrigin.position, rayOrigin.forward), $"{handName} XRRayInteractor ray"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TrySelectMemoryCellFromXRNode(UnityEngine.XR.XRNode node, string source)
        {
            UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);

            if (!device.isValid)
                return false;

            if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position))
                return false;

            if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation))
                return false;

            return TrySelectMemoryCellFromRay(new Ray(position, rotation * Vector3.forward), source);
        }

        private bool TrySelectMemoryCellFromRay(Ray ray, string source)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 8f, ~0, QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            MemoryMiniGameCell bestCell = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                MemoryMiniGameCell cell = hits[i].collider != null
                    ? hits[i].collider.GetComponentInParent<MemoryMiniGameCell>()
                    : null;

                if (cell == null || hits[i].distance >= bestDistance)
                    continue;

                bestCell = cell;
                bestDistance = hits[i].distance;
            }

            if (bestCell == null)
                return false;

            SelectMemoryMiniGameCell(bestCell.CellIndex);
            Debug.Log($"[BucketAssembler] Memory selected cell {bestCell.CellIndex} via {source}. expected={memoryMiniGameExpectedStep + 1}");
            return true;
        }

        private bool TrySelectMemoryCellFromCollider(Collider collider, string source)
        {
            MemoryMiniGameCell cell = collider != null
                ? collider.GetComponentInParent<MemoryMiniGameCell>()
                : null;

            if (cell == null)
                return false;

            SelectMemoryMiniGameCell(cell.CellIndex);
            Debug.Log($"[BucketAssembler] Memory selected cell {cell.CellIndex} via {source}. expected={memoryMiniGameExpectedStep + 1}");
            return true;
        }

        private int[] BuildMemorySequence()
        {
            List<int> available = new List<int>();

            for (int i = 0; i < 9; i++)
                available.Add(i);

            int[] sequence = new int[4];

            for (int i = 0; i < sequence.Length; i++)
            {
                int pick = Random.Range(0, available.Count);
                sequence[i] = available[pick];
                available.RemoveAt(pick);
            }

            return sequence;
        }

        private GameObject BuildMemoryGrid(Transform followTarget, int[] sequence, bool showSequence, out MemoryMiniGameCell[] cells, out TMP_Text[] labels)
        {
            GameObject root = new GameObject("Recipe Memory Mini Game");
            cells = new MemoryMiniGameCell[9];
            labels = new TMP_Text[9];

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;
                int col = i % 3;
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = $"Memory Cell {i}";
                cell.transform.SetParent(root.transform, false);
                cell.transform.localPosition = new Vector3((col - 1) * 0.16f, (1 - row) * 0.16f, 0f);
                cell.transform.localScale = new Vector3(0.13f, 0.13f, 0.012f);

                Renderer renderer = cell.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    material.color = Color.red;
                    renderer.material = material;
                }

                cells[i] = cell.AddComponent<MemoryMiniGameCell>();
                cells[i].Configure(this, i);

                GameObject labelObject = new GameObject($"Memory Cell Label {i}");
                labelObject.transform.SetParent(cell.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                labelObject.transform.localScale = Vector3.one * memoryNumberScale;
                TMP_Text label = labelObject.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.fontSize = memoryNumberFontSize;
                label.color = Color.white;
                label.text = "";
                label.rectTransform.sizeDelta = new Vector2(1.8f, 1.8f);
                labels[i] = label;
            }

            if (showSequence)
            {
                for (int i = 0; i < sequence.Length; i++)
                    SetMemoryCellRevealed(cells[sequence[i]], labels[sequence[i]], i + 1);
            }

            UpdateMemoryGrid(root, followTarget);
            return root;
        }

        private void UpdateMemoryGrid(GameObject root, Transform followTarget)
        {
            if (root == null || followTarget == null)
                return;

            root.transform.position = followTarget.position + Vector3.up * 0.55f;

            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 toGrid = root.transform.position - camera.transform.position;

                if (toGrid.sqrMagnitude > 0.001f)
                    root.transform.rotation = Quaternion.LookRotation(toGrid.normalized, Vector3.up);
            }
        }

        private void SetMemoryGridHidden(MemoryMiniGameCell[] cells, TMP_Text[] labels)
        {
            for (int i = 0; i < cells.Length; i++)
                SetMemoryCellColor(cells[i], labels[i], Color.red, "");
        }

        private void SetMemoryCellRevealed(MemoryMiniGameCell cell, TMP_Text label, int number)
        {
            SetMemoryCellColor(cell, label, Color.green, number.ToString());
        }

        private void SetMemoryCellSolved(MemoryMiniGameCell cell, TMP_Text label, int number)
        {
            SetMemoryCellColor(cell, label, new Color(0.1f, 0.65f, 1f), number.ToString());
        }

        private void SetMemoryCellColor(MemoryMiniGameCell cell, TMP_Text label, Color color, string text)
        {
            if (cell != null)
            {
                Renderer renderer = cell.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = color;
            }

            if (label != null)
                label.text = text;
        }

        private void SetMiniGameText(ShakeAxis shakeAxis, float remaining, int shakeCount, float travel)
        {
            string axisText = shakeAxis == ShakeAxis.Horizontal ? "horizontalement" : "verticalement";
            string titleText = shakeAxis == ShakeAxis.Horizontal ? "SECOUE HORIZONTAL" : "SECOUE VERTICAL";
            string message =
                $"Secoue {axisText} ! {remaining:F1}s\n" +
                $"Allers-retours : {shakeCount}/{requiredShakeDirectionChanges}";

            SetText(message);

            if (contentsLabel != null)
            {
                contentsLabel.text =
                    $"{titleText}\n" +
                    $"{remaining:F1}s\n" +
                    $"{shakeCount}/{requiredShakeDirectionChanges}";
            }
        }

        private void SetCoolingMiniGameText(float remaining)
        {
            SetText($"Refroidir !!! {remaining:F1}s\nMets la PaintCan dans l'eau.");

            if (contentsLabel != null)
            {
                contentsLabel.text =
                    "REFROIDIR !!!\n" +
                    "DANS L'EAU\n" +
                    $"{remaining:F1}s";
            }
        }

        private void SetTimingMiniGameText(float remaining, int step, int totalSteps)
        {
            SetText($"Timing {step}/{totalSteps} ! {remaining:F1}s\nAppuie sur A quand la barre blanche est dans le vert.");

            if (contentsLabel != null)
            {
                contentsLabel.text =
                    $"TIMING {step}/{totalSteps}\n" +
                    "A DANS LE VERT\n" +
                    $"{remaining:F1}s";
            }
        }

        private void SetMemoryMiniGameText(float remaining, int currentStep, int totalSteps, bool reveal)
        {
            if (reveal)
            {
                SetText($"Memoire ! Observe le schema. {remaining:F1}s");

                if (contentsLabel != null)
                    contentsLabel.text = $"MEMOIRE\nOBSERVE\n{remaining:F1}s";

                return;
            }

            SetText($"Memoire ! Reproduis le schema. {remaining:F1}s\nVise une case puis appuie sur A.");

            if (contentsLabel != null)
            {
                contentsLabel.text =
                    "MEMOIRE\n" +
                    $"{currentStep}/{totalSteps}\n" +
                    $"{remaining:F1}s";
            }
        }

        private GameObject BuildTimingBar(Transform followTarget, float greenZoneCenter, out Transform cursorTransform)
        {
            GameObject root = new GameObject("Recipe Timing Mini Game");
            cursorTransform = null;

            GameObject redBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            redBar.name = "Timing Red Bar";
            redBar.transform.SetParent(root.transform, false);
            redBar.transform.localPosition = Vector3.zero;
            redBar.transform.localScale = new Vector3(1.1f, 0.035f, 0.035f);
            SetRendererColor(redBar, Color.red);
            DestroyCollider(redBar);

            GameObject greenZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            greenZone.name = "Timing Green Zone";
            greenZone.transform.SetParent(root.transform, false);
            greenZone.transform.localPosition = new Vector3(Mathf.Lerp(-0.55f, 0.55f, greenZoneCenter), 0.002f, 0f);
            greenZone.transform.localScale = new Vector3(1.1f * timingGreenZoneSize, 0.04f, 0.04f);
            SetRendererColor(greenZone, Color.green);
            DestroyCollider(greenZone);

            GameObject cursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cursor.name = "Timing White Cursor";
            cursor.transform.SetParent(root.transform, false);
            cursor.transform.localPosition = new Vector3(-0.55f, 0.018f, 0f);
            cursor.transform.localScale = new Vector3(0.035f, 0.075f, 0.06f);
            SetRendererColor(cursor, Color.white);
            DestroyCollider(cursor);
            cursorTransform = cursor.transform;

            UpdateTimingBar(root, cursorTransform, followTarget, 0f);
            return root;
        }

        private void UpdateTimingBar(GameObject root, Transform cursorTransform, Transform followTarget, float cursor)
        {
            if (root == null || followTarget == null)
                return;

            root.transform.position = followTarget.position + Vector3.up * 0.55f;

            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 directionToCamera = root.transform.position - camera.transform.position;

                if (directionToCamera.sqrMagnitude > 0.0001f)
                    root.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
            }

            if (cursorTransform != null)
                cursorTransform.localPosition = new Vector3(Mathf.Lerp(-0.55f, 0.55f, cursor), 0.018f, 0f);
        }

        private void SetRendererColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();

            if (renderer == null)
                return;

            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.material = material;
        }

        private void DestroyCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);
        }

        private bool MiniGameInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;

            if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E)
                || Input.GetKeyDown(KeyCode.A)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetKeyDown(KeyCode.JoystickButton14)
                || Input.GetKeyDown(KeyCode.JoystickButton15))
            {
                return true;
            }
#endif

            return XRPrimaryPressedThisFrame(UnityEngine.XR.XRNode.RightHand, ref rightTimingButtonWasPressed);
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

        private bool IsPaintCanInWater(Transform paintCan)
        {
            Bounds paintCanBounds = GetTransformWorldBounds(paintCan);
            Collider[] waterColliders = FindWaterColliders();

            for (int i = 0; i < waterColliders.Length; i++)
            {
                Collider waterCollider = waterColliders[i];

                if (waterCollider == null || !waterCollider.enabled)
                    continue;

                if (waterCollider.bounds.Intersects(paintCanBounds)
                    || waterCollider.bounds.Contains(paintCan.position))
                {
                    return true;
                }
            }

            return false;
        }

        private Collider[] FindWaterColliders()
        {
            Collider[] allColliders = FindObjectsOfType<Collider>(true);
            List<Collider> waterColliders = new List<Collider>();

            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null && IsWaterObject(allColliders[i].transform))
                    waterColliders.Add(allColliders[i]);
            }

            return waterColliders.ToArray();
        }

        private Bounds GetTransformWorldBounds(Transform target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);

            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;

                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);

                return bounds;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;

                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                return bounds;
            }

            return new Bounds(target.position, Vector3.one * 0.2f);
        }

        private void PlayMixingSound(Vector3 position)
        {
            if (mixingClip == null)
                mixingClip = Resources.Load<AudioClip>(mixingClipName);

            if (mixingClip == null)
                return;

            if (mixingAudioSource == null)
            {
                GameObject audioObject = new GameObject("Recipe Mixing Audio");
                mixingAudioSource = audioObject.AddComponent<AudioSource>();
                mixingAudioSource.playOnAwake = false;
                mixingAudioSource.loop = true;
                mixingAudioSource.spatialBlend = 1f;
            }

            mixingAudioSource.transform.position = position;
            mixingAudioSource.clip = mixingClip;
            mixingAudioSource.volume = mixingClipVolume;
            mixingAudioSource.Play();
        }

        private void StopMixingSound()
        {
            if (mixingAudioSource == null)
                return;

            mixingAudioSource.Stop();
            Destroy(mixingAudioSource.gameObject);
            mixingAudioSource = null;
        }

        private void PlayTimingSuccessSound(Vector3 position)
        {
            if (timingSuccessClip == null)
                timingSuccessClip = Resources.Load<AudioClip>(timingSuccessClipName);

            if (timingSuccessClip == null)
                return;

            GameObject audioObject = new GameObject("Timing Success Audio");
            audioObject.transform.position = position;

            AudioSource audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = timingSuccessClipVolume;
            audioSource.clip = timingSuccessClip;
            audioSource.Play();

            StartCoroutine(StopAndDestroyAudioAfterDelay(audioSource, timingSuccessClipDuration));
        }

        private IEnumerator StopAndDestroyAudioAfterDelay(AudioSource audioSource, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

            if (audioSource == null)
                yield break;

            audioSource.Stop();
            Destroy(audioSource.gameObject);
        }

        private void FailRecipeWithExplosion(string message)
        {
            VrPaintCanDebugLog.Write($"failRecipeWithExplosion bucket='{name}' message='{message}' pos={VrPaintCanDebugLog.FormatVector(transform.position)} isVr={IsVrActive()}");
            SetText(message);
            SpawnWrongRecipeEffect();
            currentCounts.Clear();
            UpdateContentsLabel();
            onWrongRecipe?.Invoke();
            
            isDead = true;
            deathInputEnabledTime = Time.time + 0.5f;

            Camera mainCam = Camera.main;
            if (IsVrActive())
            {
                PrepareVrDeathState(mainCam);
            }
            else if (mainCam != null)
            {
                originalCameraParent = mainCam.transform.parent;
                originalCameraLocalPos = mainCam.transform.localPosition;
                originalCameraLocalRot = mainCam.transform.localRotation;
                deathMovedCamera = true;

                mainCam.transform.SetParent(null);
                mainCam.transform.position = transform.position + new Vector3(0f, 2.5f, -3f);
                mainCam.transform.LookAt(transform.position);

                // Disable player controllers if they exist
                PcPlayerController controller = FindObjectOfType<PcPlayerController>();
                if (controller != null) controller.enabled = false;

                PcMouseGrabber grabber = FindObjectOfType<PcMouseGrabber>();
                if (grabber != null) grabber.enabled = false;
            }

            CreateDeathScreenUI();
        }

        private void CreateDeathScreenUI()
        {
            GameObject canvasObj = new GameObject("DeathCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.sortingOrder = 2000;
            ConfigureDeathCanvas(canvas);
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject bgObj = new GameObject("RedBackground");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.8f, 0f, 0f, 0.45f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("DeathText");
            textObj.transform.SetParent(canvasObj.transform, false);
            deathTextUI = textObj.AddComponent<Text>();
            deathTextUI.text = IsVrActive()
                ? "VOUS ETES MORT\nAppuyez sur A ou sur la gachette pour reapparaitre"
                : "VOUS ETES MORT\nAppuyez sur une touche pour reapparaitre";
            deathTextUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            deathTextUI.fontSize = IsVrActive() ? 64 : 48;
            deathTextUI.alignment = TextAnchor.MiddleCenter;
            deathTextUI.color = Color.white;
            deathTextUI.gameObject.AddComponent<Outline>().effectColor = Color.black;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.1f);
            textRect.anchorMax = new Vector2(0.9f, 0.9f);
            textRect.sizeDelta = Vector2.zero;
        }

        private void ConfigureDeathCanvas(Canvas canvas)
        {
            Camera mainCam = Camera.main;

            if (!IsVrActive() || mainCam == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCam;

            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            rectTransform.SetParent(mainCam.transform, false);
            rectTransform.localPosition = new Vector3(0f, 0f, 1.05f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one * 0.001f;
            rectTransform.sizeDelta = new Vector2(2600f, 1700f);
        }

        private void RestartScene()
        {
            if (deathTextUI != null && deathTextUI.canvas != null)
            {
                Destroy(deathTextUI.canvas.gameObject);
            }

            Camera mainCam = Camera.main;
            if (mainCam != null && deathMovedCamera)
            {
                mainCam.transform.SetParent(originalCameraParent, false);
                mainCam.transform.localPosition = originalCameraLocalPos;
                mainCam.transform.localRotation = originalCameraLocalRot;
                deathMovedCamera = false;
            }

            RestoreVrDeathState();

            PcPlayerController controller = FindObjectOfType<PcPlayerController>(true);
            if (controller != null) controller.enabled = true;

            PcMouseGrabber grabber = FindObjectOfType<PcMouseGrabber>(true);
            if (grabber != null) grabber.enabled = true;

            IngredientBall[] allBalls = FindObjectsOfType<IngredientBall>(true);
            foreach (var ball in allBalls) Destroy(ball.gameObject);

            ParticlePacket[] packets = FindObjectsOfType<ParticlePacket>(true);
            foreach (var packet in packets) Destroy(packet.gameObject);

            GameObject wakeupHost = new GameObject("Wakeup Intro Controller");
            WakeupIntroController wakeupIntro = wakeupHost.AddComponent<WakeupIntroController>();
            bool wakeupStarted = wakeupIntro.BeginIntro();

            if (!wakeupStarted)
                Destroy(wakeupHost);

            if (deathMovementLocked)
            {
                PlayerMovementLock.Unlock("recipe death");
                deathMovementLocked = false;
            }

            isDead = false;
        }

        private void PrepareVrDeathState(Camera mainCam)
        {
            VrPaintCanDebugLog.Write($"prepareVrDeathState bucket='{name}' cam='{(mainCam != null ? mainCam.name : "null")}' bucketPos={VrPaintCanDebugLog.FormatVector(transform.position)}");

            if (!deathMovementLocked)
            {
                PlayerMovementLock.Lock("recipe death");
                deathMovementLocked = true;
            }

            Transform xrOrigin = FindVrOrigin(mainCam);

            if (xrOrigin == null)
            {
                VrPaintCanDebugLog.Write("prepareVrDeathState no XR origin found");
                return;
            }

            if (!deathPreparedVrRig)
            {
                deathVrOrigin = xrOrigin;
                deathVrRig = xrOrigin.GetComponent<XROrigin>();
                deathVrCharacterController = xrOrigin.GetComponent<CharacterController>();
                deathVrCharacterControllerWasEnabled = deathVrCharacterController != null && deathVrCharacterController.enabled;
                deathPreparedVrRig = true;
            }

            if (deathVrCharacterController != null)
            {
                VrCharacterControllerSafetyGuard.AllowTemporaryDisable("recipe death move", 0.5f);
                deathVrCharacterController.enabled = false;
            }

            Vector3 deathCameraPosition = transform.position + new Vector3(0f, 1.65f, -2.2f);

            if (deathVrRig != null)
            {
                deathVrRig.MoveCameraToWorldLocation(deathCameraPosition);
                FaceVrCameraTowardDeathTarget(mainCam);
            }
            else if (mainCam != null)
            {
                deathVrOrigin.position += deathCameraPosition - mainCam.transform.position;
                FaceTransformToward(deathVrOrigin, transform.position);
            }
            else
            {
                deathVrOrigin.position = deathCameraPosition;
            }

            StopPaintCanPhysics();

            if (deathVrCharacterController != null)
                deathVrCharacterController.enabled = true;

            Physics.SyncTransforms();
            VrPaintCanDebugLog.Write($"prepareVrDeathState applied xrOrigin='{xrOrigin.name}' originPos={VrPaintCanDebugLog.FormatVector(xrOrigin.position)} deathCameraTarget={VrPaintCanDebugLog.FormatVector(deathCameraPosition)} ccEnabledAfterMove={(deathVrCharacterController != null && deathVrCharacterController.enabled)}");
        }

        private void RestoreVrDeathState()
        {
            if (!deathPreparedVrRig)
                return;

            if (deathVrCharacterController != null)
                deathVrCharacterController.enabled = deathVrCharacterControllerWasEnabled || IsVrActive();

            VrPaintCanDebugLog.Write($"restoreVrDeathState origin='{(deathVrOrigin != null ? deathVrOrigin.name : "null")}' ccRestored={deathVrCharacterControllerWasEnabled}");
            deathVrOrigin = null;
            deathVrRig = null;
            deathVrCharacterController = null;
            deathPreparedVrRig = false;
            Physics.SyncTransforms();
        }

        private Transform FindVrOrigin(Camera mainCam)
        {
            if (mainCam != null)
            {
                XROrigin origin = mainCam.GetComponentInParent<XROrigin>();

                if (origin != null)
                    return origin.transform;

                Transform namedRoot = FindVrOriginParent(mainCam.transform);

                if (namedRoot != null)
                    return namedRoot;
            }

            XROrigin sceneOrigin = FindObjectOfType<XROrigin>(true);

            if (sceneOrigin != null)
                return sceneOrigin.transform;

            Transform fallback = FindTransformByName("XR Origin (XR Rig)");
            return fallback != null ? fallback : FindTransformByName("XR Origin");
        }

        private Transform FindVrOriginParent(Transform source)
        {
            Transform current = source;

            while (current != null)
            {
                if (current.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || current.name.IndexOf("XR Rig", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private void FaceVrCameraTowardDeathTarget(Camera mainCam)
        {
            if (deathVrRig == null)
                return;

            if (mainCam == null)
                mainCam = Camera.main;

            Vector3 direction = transform.position - (mainCam != null ? mainCam.transform.position : deathVrOrigin.position);
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
                deathVrRig.MatchOriginUpCameraForward(Vector3.up, direction.normalized);
        }

        private void FaceTransformToward(Transform source, Vector3 target)
        {
            if (source == null)
                return;

            Vector3 direction = target - source.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
                source.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void StopPaintCanPhysics()
        {
            Rigidbody[] bodies = FindObjectsOfType<Rigidbody>(true);

            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];

                if (body == null || !IsPaintCanName(body.name))
                    continue;

                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
                VrPaintCanDebugLog.Write($"stopPaintCanPhysics body='{body.name}' pos={VrPaintCanDebugLog.FormatVector(body.position)}");
            }
        }

        private bool DeathRestartPressedInXR()
        {
            if (!IsVrActive())
                return false;

            return XRButtonPressed(UnityEngine.XR.XRNode.LeftHand)
                || XRButtonPressed(UnityEngine.XR.XRNode.RightHand);
        }

        private bool XRButtonPressed(UnityEngine.XR.XRNode node)
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

        private bool IsVrActive()
        {
            return UnityEngine.XR.XRSettings.isDeviceActive;
        }

        private bool IsMixerTool(Collider other)
        {
            Transform current = other.transform;

            while (current != null)
            {
                if (IsPaintCanName(current.name))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private bool IsPaintCanName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            string normalizedObjectName = objectName.Replace(" ", "");
            string normalizedMixerName = string.IsNullOrWhiteSpace(mixerToolName)
                ? "PaintCan"
                : mixerToolName.Replace(" ", "");

            return normalizedObjectName.StartsWith("PaintCan", System.StringComparison.OrdinalIgnoreCase)
                || normalizedObjectName.StartsWith(normalizedMixerName, System.StringComparison.OrdinalIgnoreCase);
        }

        private void SpawnWrongRecipeEffect()
        {
            Vector3 position = transform.position + Vector3.up * explosionEffectHeight;

            GameObject effectObject = new GameObject("Wrong Recipe Explosion");
            effectObject.transform.position = position;

            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRenderer = effectObject.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null) particleShader = Shader.Find("Standard");
            
            if (particleShader != null)
            {
                Material particleMat = new Material(particleShader);
                psRenderer.material = particleMat;
            }
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = explosionEffectDuration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.05f), new Color(0.1f, 0.1f, 0.1f));
            main.gravityModifier = 0.05f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 150) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = explosionEffectRadius * 0.8f;

            Light flash = effectObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.3f, 0.0f);
            flash.range = explosionEffectRadius * 8f;
            flash.intensity = 8f;

            AudioClip c4Clip = Resources.Load<AudioClip>("c4_explode1_19");
            if (c4Clip != null)
                AudioSource.PlayClipAtPoint(c4Clip, position, explosionVolume * 2f);
            else
                AudioSource.PlayClipAtPoint(GetExplosionClip(), position, explosionVolume);

            particles.Play();
            Destroy(effectObject, explosionEffectDuration + 2f);
            StartCoroutine(FadeExplosionLight(flash));
        }

        private IEnumerator FadeExplosionLight(Light flash)
        {
            float startIntensity = flash != null ? flash.intensity : 0f;
            float elapsed = 0f;

            while (flash != null && elapsed < explosionEffectDuration)
            {
                elapsed += Time.deltaTime;
                flash.intensity = Mathf.Lerp(startIntensity, 0f, elapsed / explosionEffectDuration);
                yield return null;
            }
        }

        private AudioClip GetExplosionClip()
        {
            if (generatedExplosionClip != null)
                return generatedExplosionClip;

            const int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * 0.45f);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 7.5f);
                float noise = Random.Range(-1f, 1f);
                float thump = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 11f);
                samples[i] = Mathf.Clamp((noise * 0.65f + thump) * envelope, -1f, 1f);
            }

            generatedExplosionClip = AudioClip.Create("GeneratedExplosion", sampleCount, 1, sampleRate, false);
            generatedExplosionClip.SetData(samples, 0);
            return generatedExplosionClip;
        }

        private void CompleteCraft(CraftRecipe recipe)
        {
            if (recipe.outputType == IngredientType.Uranium)
            {
                SpawnUraniumAtPaintCan(recipe);
                UraniumDeliveryObjectiveController.NotifyUraniumCreated();
                SetText($"Recette reussie : {recipe.displayName} ({recipe.formula}) produit.");
            }
            else
            {
                bool wasAlreadyKnown = IsRecipeAlreadyKnown(recipe);
                WorktableParticleSpawner.Instance?.UnlockParticle(recipe.outputType);
                SetText(wasAlreadyKnown
                    ? $"Recette reussie : {recipe.displayName} ({recipe.formula}) validee."
                    : $"Recette reussie : {recipe.displayName} ({recipe.formula}) debloquee sur la worktable.");
            }

            currentCounts.Clear();
            completedRecipeOutputs.Add(recipe.outputType);
            UpdateContentsLabel();
            onRecipeCompleted?.Invoke();
            DestroyOwningPaintCan();
        }

        private bool IsRecipeAlreadyKnown(CraftRecipe recipe)
        {
            return completedRecipeOutputs.Contains(recipe.outputType)
                || WorktableParticleSpawner.IsParticleUnlocked(recipe.outputType);
        }

        private void SpawnUraniumAtPaintCan(CraftRecipe recipe)
        {
            GameObject paintCan = ResolveOwningPaintCan();
            Vector3 spawnPosition = paintCan != null
                ? paintCan.transform.position + Vector3.up * 0.35f
                : transform.position + Vector3.up * 0.35f;

            GameObject output = CreateParticleBall(recipe.displayName, recipe.outputType, recipe.outputColor, recipe.outputRadius, spawnPosition);
            spawnedOutputs.Add(output);
            protectedRecipeOutputs.Add(output);
            StartCoroutine(UnprotectRecipeOutput(output));
            Debug.Log($"[BucketAssembler] Uranium produced at PaintCan position: {spawnPosition}");
        }

        private void DestroyOwningPaintCan()
        {
            GameObject paintCan = ResolveOwningPaintCan();

            if (paintCan == null)
                return;

            Debug.Log($"[BucketAssembler] Destroying used paintcan after recipe: {paintCan.name}");
            DestroyContentsLabel();
            Destroy(paintCan, successDelay);
        }

        private void DestroyContentsLabel()
        {
            if (contentsLabel == null)
                return;

            Destroy(contentsLabel.gameObject);
            contentsLabel = null;
        }

        private GameObject ResolveOwningPaintCan()
        {
            Transform current = transform;

            while (current != null)
            {
                if (IsPaintCanName(current.name))
                    return current.gameObject;

                current = current.parent;
            }

            return null;
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
            GameObject uraniumModel = type == IngredientType.Uranium ? LoadUraniumModel() : null;

            GameObject ball = uraniumModel != null
                ? CreateUraniumModelRoot(uraniumModel, radius)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            ball.name = particleName;
            ball.transform.position = position;
            ball.transform.localScale = uraniumModel != null ? Vector3.one : Vector3.one * (radius * 2f);

            if (uraniumModel == null)
            {
                Renderer renderer = ball.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    material.color = color;
                    if (type == IngredientType.Uranium)
                    {
                        material.color = new Color(0.35f, 1f, 0.25f);
                        material.SetColor("_EmissionColor", new Color(0.08f, 0.35f, 0.04f));
                        material.EnableKeyword("_EMISSION");
                    }
                    renderer.material = material;
                }
            }
            else
            {
                ApplyUraniumGreenMaterial(ball);
            }

            EnsureCollider(ball);

            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = type == IngredientType.Electron ? 0.15f : 0.45f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            IngredientBall ingredient = ball.AddComponent<IngredientBall>();
            ingredient.Configure(type, particleName);

            XRGrabInteractable grabInteractable = ball.GetComponent<XRGrabInteractable>();

            if (grabInteractable == null)
                grabInteractable = ball.AddComponent<XRGrabInteractable>();

            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;

            CreateParticleQuantityLabel(ball.transform, particleName, 1, radius);
            return ball;
        }

        private void CreateParticleQuantityLabel(Transform target, string label, int quantity, float radius)
        {
            if (target == null)
                return;

            GameObject labelObject = new GameObject("Particle Quantity Label");
            float offset = Mathf.Max(radius * 2.8f, 0.24f);
            labelObject.transform.position = target.position + Vector3.up * offset;
            labelObject.transform.localScale = Vector3.one;
            labelObject.AddComponent<PacketQuantityLabel>().Configure(target, offset);

            TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
            text.text = $"{label} x{Mathf.Max(1, quantity)}";
            text.fontSize = 0.18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = new Vector2(2.2f, 0.35f);
        }

        private GameObject LoadUraniumModel()
        {
            GameObject model = LoadResourceModel("uranium");

            if (model != null)
                return model;

            model = LoadResourceModel("UraniumModel");

            if (model != null)
                return model;

            Debug.LogWarning("[BucketAssembler] Uranium model not found. Expected Assets/Resources/uranium.obj");
            return null;
        }

        private GameObject LoadResourceModel(string resourceName)
        {
            GameObject model = Resources.Load<GameObject>(resourceName);

            if (model != null)
                return model;

            Object[] loadedAssets = Resources.LoadAll(resourceName);

            for (int i = 0; i < loadedAssets.Length; i++)
            {
                if (loadedAssets[i] is GameObject loadedModel)
                    return loadedModel;
            }

            return null;
        }

        private GameObject CreateUraniumModelRoot(GameObject uraniumModel, float radius)
        {
            GameObject root = new GameObject("Uranium Model Root");
            GameObject visual = Instantiate(uraniumModel, root.transform);
            visual.name = "Uranium Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.25f;
            CenterVisualOnRoot(root.transform, visual.transform);

            float targetDiameter = Mathf.Max(radius * 2f, 0.35f);
            FitVisualToDiameter(visual.transform, targetDiameter);
            CenterVisualOnRoot(root.transform, visual.transform);
            return root;
        }

        private void ApplyUraniumGreenMaterial(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Material uraniumMaterial = new Material(Shader.Find("Standard"));
            uraniumMaterial.color = new Color(0.1f, 0.9f, 0.18f);
            uraniumMaterial.SetColor("_EmissionColor", new Color(0.02f, 0.2f, 0.03f));
            uraniumMaterial.EnableKeyword("_EMISSION");

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].material = uraniumMaterial;
            }
        }

        private void CenterVisualOnRoot(Transform root, Transform visual)
        {
            Bounds bounds;

            if (!TryGetRendererBounds(visual, out bounds))
                return;

            visual.position += root.position - bounds.center;
        }

        private void FitVisualToDiameter(Transform visual, float targetDiameter)
        {
            Bounds bounds;

            if (!TryGetRendererBounds(visual, out bounds))
                return;

            float largestSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

            if (largestSize <= 0.0001f)
                return;

            visual.localScale *= targetDiameter / largestSize;
        }

        private bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(root.position, Vector3.zero);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
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

            return hasBounds;
        }

        private void EnsureCollider(GameObject target)
        {
            if (target.GetComponentInChildren<Collider>() != null)
                return;

            BoxCollider collider = target.AddComponent<BoxCollider>();
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                collider.size = Vector3.one * 0.25f;
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            collider.center = target.transform.InverseTransformPoint(bounds.center);
            collider.size = Abs(target.transform.InverseTransformVector(bounds.size));
        }

        private Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
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

        private IngredientBall GetValidIngredient(Collider other)
        {
            if (IsPlayerOrInteractorCollider(other))
                return null;

            if (IsCardboxBaseCollider(other))
                return null;

            IngredientBall[] ingredients = other.GetComponentsInParent<IngredientBall>();

            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null && ingredients[i].Type != IngredientType.None)
                    return ingredients[i];
            }

            ingredients = other.GetComponentsInChildren<IngredientBall>();

            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null && ingredients[i].Type != IngredientType.None)
                    return ingredients[i];
            }

            if (other.attachedRigidbody != null)
            {
                ingredients = other.attachedRigidbody.GetComponentsInChildren<IngredientBall>();

                for (int i = 0; i < ingredients.Length; i++)
                {
                    if (ingredients[i] != null && ingredients[i].Type != IngredientType.None)
                        return ingredients[i];
                }
            }

            IngredientType inferredType = InferIngredientType(other.transform);

            if (inferredType == IngredientType.None && other.attachedRigidbody != null)
                inferredType = InferIngredientType(other.attachedRigidbody.transform);

            if (inferredType != IngredientType.None)
            {
                GameObject ingredientObject = other.attachedRigidbody != null
                    ? other.attachedRigidbody.gameObject
                    : other.gameObject;

                IngredientBall ingredient = ingredientObject.GetComponent<IngredientBall>();

                if (ingredient == null)
                    ingredient = ingredientObject.AddComponent<IngredientBall>();

                ingredient.Configure(inferredType, inferredType.ToString());
                Debug.LogWarning($"[BucketAssembler] Inferred ingredient {inferredType} from collider '{other.name}' path='{BuildTransformPath(other.transform)}'");
                return ingredient;
            }

            return null;
        }

        private bool IsCardboxBaseCollider(Collider other)
        {
            return other != null && other.GetComponentInParent<CardboxBaseController>() != null;
        }

        private ParticlePacket GetParticlePacket(Collider other)
        {
            ParticlePacket packet = other.GetComponentInParent<ParticlePacket>();

            if (packet != null)
                return packet;

            packet = other.GetComponentInChildren<ParticlePacket>();

            if (packet != null)
                return packet;

            if (other.attachedRigidbody != null)
                return other.attachedRigidbody.GetComponentInChildren<ParticlePacket>();

            return null;
        }

        private IngredientType InferIngredientType(Transform source)
        {
            if (source == null)
                return IngredientType.None;

            Transform current = source;

            while (current != null)
            {
                string objectName = current.name;

                if (ContainsIngredientWord(objectName, "QuarkDown")
                    || (ContainsIngredientWord(objectName, "Quark") && ContainsIngredientWord(objectName, "Down"))
                    || ContainsIngredientWord(objectName, "Down"))
                {
                    return IngredientType.QuarkDown;
                }

                if (ContainsIngredientWord(objectName, "QuarkUp")
                    || (ContainsIngredientWord(objectName, "Quark") && ContainsIngredientWord(objectName, "Up"))
                    || ContainsIngredientWord(objectName, "Up"))
                {
                    return IngredientType.QuarkUp;
                }

                if (ContainsIngredientWord(objectName, "Electron"))
                    return IngredientType.Electron;

                if (ContainsIngredientWord(objectName, "Proton"))
                    return IngredientType.Proton;

                if (ContainsIngredientWord(objectName, "Neutron"))
                    return IngredientType.Neutron;

                if (ContainsIngredientWord(objectName, "Uranium"))
                    return IngredientType.Uranium;

                current = current.parent;
            }

            return IngredientType.None;
        }

        private bool ContainsIngredientWord(string objectName, string ingredientWord)
        {
            if (string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(ingredientWord))
                return false;

            int searchIndex = 0;

            while (searchIndex < objectName.Length)
            {
                int index = objectName.IndexOf(ingredientWord, searchIndex, System.StringComparison.OrdinalIgnoreCase);

                if (index < 0)
                    return false;

                int endIndex = index + ingredientWord.Length;
                bool startsAtBoundary = index == 0 || !char.IsLetterOrDigit(objectName[index - 1]);
                bool endsAtBoundary = endIndex >= objectName.Length || !char.IsLetterOrDigit(objectName[endIndex]);

                if (startsAtBoundary && endsAtBoundary)
                    return true;

                searchIndex = index + 1;
            }

            return false;
        }

        private bool CanAcceptIngredientType(IngredientType type)
        {
            if (isPaintCanTriggerZone && type == IngredientType.Uranium)
            {
                if (Time.time >= nextUraniumRejectFeedbackTime)
                {
                    nextUraniumRejectFeedbackTime = Time.time + 1f;
                    Debug.Log("[BucketAssembler] PaintCan ignored uranium ingredient.");
                    SetText("La PaintCan ne peut pas prendre l'uranium.");
                }

                return false;
            }

            return true;
        }

        private bool IsPlayerOrInteractorCollider(Collider other)
        {
            if (other == null)
                return true;

            if (IsIngredientInteractable(other) || IsMixerTool(other) || IsCardboxBaseCollider(other))
                return false;

            if (other is CharacterController)
                return true;

            if (other.GetComponentInParent<XROrigin>() != null)
                return true;

            if (other.GetComponentInParent<Camera>() != null)
                return true;

            if (other.GetComponentInParent<XRBaseInteractor>() != null)
                return true;

            return HasPlayerRigNameInParents(other.transform);
        }

        private bool IsIngredientInteractable(Collider other)
        {
            return HasIngredientMarker(other)
                && (other.GetComponentInParent<XRGrabInteractable>() != null
                    || other.GetComponentInChildren<XRGrabInteractable>() != null);
        }

        private bool HasIngredientMarker(Collider other)
        {
            return other != null
                && (other.GetComponentInParent<IngredientBall>() != null
                    || other.GetComponentInChildren<IngredientBall>() != null
                    || other.GetComponentInParent<ParticlePacket>() != null
                    || other.GetComponentInChildren<ParticlePacket>() != null);
        }

        private bool HasPlayerRigNameInParents(Transform source)
        {
            Transform current = source;

            while (current != null)
            {
                string objectName = current.name;

                if (!string.IsNullOrWhiteSpace(objectName)
                    && (objectName.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || objectName.IndexOf("XR Rig", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || objectName.IndexOf("Main Camera", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool HasNameInParents(Transform source, string namePart)
        {
            if (string.IsNullOrWhiteSpace(namePart))
                return false;

            Transform current = source;

            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.name)
                    && current.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private string BuildTransformPath(Transform source)
        {
            if (source == null)
                return "null";

            string path = source.name;
            Transform current = source.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Public method to add an ingredient directly by type and amount.
        /// Called by PaintCanIngredientDetector as a fallback when trigger detection fails.
        /// </summary>
        public void AddIngredientDirect(IngredientType type, int amount)
        {
            if (!CanAcceptIngredientType(type))
                return;

            if (!currentCounts.ContainsKey(type))
                currentCounts[type] = 0;

            currentCounts[type] += amount;
            Debug.Log($"[BucketAssembler] Direct add: {amount} x {type}. {BuildCurrentContentsText()}");

            UpdateContentsLabel();

            if (requireMixerToolPress)
            {
                SetText("Ingredients ajoutes. Appuie sur le recipient avec une PaintCan pour melanger.\n" + BuildCurrentContentsText());
                return;
            }

            TryMixCurrentContents();
        }

        public void ResetBucket()
        {
            currentCounts.Clear();

            if (clearSpawnedOutputsOnReset)
                ClearSpawnedOutputs();

            UpdateFeedback();
            UpdateContentsLabel();
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
                UpdateContentsLabel();
                return;
            }

            SetText(
                "Recettes :\n" +
                "Proton = 2 Up + 1 Down (uud)\n" +
                "Neutron = 1 Up + 2 Down (udd)\n" +
                "Helium = 2 Protons + 2 Neutrons + 2 Electrons\n" +
                "Uranium = 92 Protons + 146 Neutrons + 92 Electrons\n" +
                "Melange avec une PaintCan\n" +
                BuildCurrentContentsText()
            );
            UpdateContentsLabel();
        }

        private void BuildContentsLabel()
        {
            if (contentsLabel != null)
                return;

            GameObject labelObject = new GameObject("Bucket Contents Label");
            labelObject.transform.position = transform.position + contentsLabelOffset;
            labelObject.transform.localScale = Vector3.one;

            TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
            text.fontSize = contentsLabelFontSize;
            text.color = Color.yellow;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(3.5f, 1.1f);
            contentsLabel = text;
            UpdateContentsLabel();
        }

        private void UpdateContentsLabel()
        {
            if (contentsLabel == null)
                return;

            contentsLabel.text =
                $"Up {GetCount(IngredientType.QuarkUp)}   Down {GetCount(IngredientType.QuarkDown)}\n" +
                $"p {GetCount(IngredientType.Proton)}   n {GetCount(IngredientType.Neutron)}   e {GetCount(IngredientType.Electron)}\n" +
                $"U {GetCount(IngredientType.Uranium)}";
        }

        private string BuildCurrentContentsText()
        {
            return "Dans le recipient : " +
                $"Up={GetCount(IngredientType.QuarkUp)}, " +
                $"Down={GetCount(IngredientType.QuarkDown)}, " +
                $"p={GetCount(IngredientType.Proton)}, " +
                $"n={GetCount(IngredientType.Neutron)}, " +
                $"e={GetCount(IngredientType.Electron)}, " +
                $"U={GetCount(IngredientType.Uranium)}";
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
