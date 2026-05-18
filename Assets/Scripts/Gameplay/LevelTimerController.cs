using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public class LevelTimerController : MonoBehaviour
    {
        public static LevelTimerController Instance { get; private set; }

        [SerializeField] private string displayObjectName = "Chronometre Niveau";
        [SerializeField] private bool useManualDisplayWhenPresent;
        [SerializeField] private bool useAlarmScreenAsDisplay = true;
        [SerializeField] private string fallbackAnchorName = "Couch";
        [SerializeField] private Color digitColor = new Color(1f, 0.08f, 0.04f);
        [SerializeField] private Color stoppedDigitColor = new Color(1f, 0.08f, 0.04f);
        [SerializeField] private Color screenFaceColor = new Color(0.015f, 0.018f, 0.014f);
        [SerializeField] private bool useFixedAlarmScreenFaceDirection = true;
        [SerializeField] private Vector3 alarmScreenFaceWorldDirection = Vector3.back;
        [SerializeField] private bool invertAlarmScreenFace = true;
        [SerializeField] private bool useFixedAlarmScreenTextTransform = true;
        [SerializeField] private Vector3 alarmScreenTextLocalPosition = new Vector3(5.60733e-05f, 0f, 1.18345e-05f);
        [SerializeField] private Vector3 alarmScreenTextLocalRotation = new Vector3(0f, -89.75f, 177.2f);
        [SerializeField] private Vector3 alarmScreenTextLocalScale = new Vector3(0.018f, 0.018f, 0.018f);
        [SerializeField] private Vector2 alarmScreenTextRectSize = new Vector2(3f, 1f);
        [SerializeField] private float alarmScreenTextFontSize = 2f;
        [SerializeField] private Vector3 screenLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 screenLocalRotation = new Vector3(0f, 180f, 0f);
        [SerializeField] private float displayRefreshInterval = 0.08f;
        [SerializeField] private float alarmSearchInterval = 1f;

        private Transform alarmTarget;
        private Transform screenTarget;
        private const string TimerTextObjectName = "Timer Digit";
        private GameObject displayRoot;
        private Transform panelTransform;
        private TMP_Text timerText;
        private bool isManualDisplay;
        private float startTime;
        private float stoppedElapsed;
        private float nextDisplayRefreshTime;
        private float nextAlarmSearchTime;
        private bool isRunning;
        private bool isStopped;

        private struct ScreenPlane
        {
            public Vector3 center;
            public Vector3 normal;
            public Vector3 up;
            public float width;
            public float height;
            public float depth;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInSceneAfterLoad()
        {
            EnsureInScene();
        }

        public static float NotifyLevelStarted()
        {
            LevelTimerController controller = EnsureInScene();
            controller.StartTimer();
            return controller.ElapsedSeconds;
        }

        public static float NotifyLevelFinished()
        {
            LevelTimerController controller = EnsureInScene();
            controller.StopTimer();
            return controller.ElapsedSeconds;
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int totalSeconds = Mathf.FloorToInt(seconds);
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds / 60) % 60;
            int remainingSeconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}:{minutes:00}:{remainingSeconds:00}";

            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInScene();
        }

        private static LevelTimerController EnsureInScene()
        {
            if (Instance != null)
                return Instance;

            LevelTimerController existing = FindObjectOfType<LevelTimerController>();

            if (existing != null)
                return existing;

            GameObject host = new GameObject("Level Timer Controller");
            return host.AddComponent<LevelTimerController>();
        }

        private float ElapsedSeconds
        {
            get
            {
                if (isRunning)
                    return Time.time - startTime;

                return stoppedElapsed;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            stoppedElapsed = 0f;
            ResolveAlarmTarget();
            BuildDisplay();
            UpdateDisplay(true);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!isManualDisplay && (alarmTarget == null || screenTarget == null) && Time.time >= nextAlarmSearchTime)
            {
                nextAlarmSearchTime = Time.time + alarmSearchInterval;
                ResolveAlarmTarget();
                RepositionDisplay();
            }

            if (Time.time < nextDisplayRefreshTime)
                return;

            nextDisplayRefreshTime = Time.time + displayRefreshInterval;
            UpdateDisplay(false);
        }

        private void StartTimer()
        {
            if (isRunning)
                return;

            isStopped = false;
            stoppedElapsed = 0f;
            startTime = Time.time;
            isRunning = true;
            UpdateDisplay(true);
            Debug.Log("[LevelTimer] Timer started.");
        }

        private void StopTimer()
        {
            if (!isRunning && isStopped)
                return;

            stoppedElapsed = ElapsedSeconds;
            isRunning = false;
            isStopped = true;
            UpdateDisplay(true);
            Debug.Log($"[LevelTimer] Timer stopped at {FormatTime(stoppedElapsed)}.");
        }

        private void UpdateDisplay(bool force)
        {
            if (timerText == null)
                BuildDisplay();

            if (timerText == null)
                return;

            timerText.text = FormatTime(ElapsedSeconds);
            timerText.color = isStopped ? stoppedDigitColor : digitColor;

            if (displayRoot == screenTarget?.gameObject)
                FitTextToScreen(screenTarget);

            if (force)
                RepositionDisplay();
        }

        private void BuildDisplay()
        {
            if (displayRoot != null)
                return;

            if (useAlarmScreenAsDisplay && TryUseAlarmScreenDisplay())
                return;

            if (useManualDisplayWhenPresent && TryUseManualDisplay())
                return;

            displayRoot = new GameObject(displayObjectName);
            displayRoot.transform.SetParent(transform, false);
            isManualDisplay = false;

            panelTransform = CreateTimerPanel(displayRoot.transform);
            timerText = CreateTimerText(displayRoot.transform);

            RepositionDisplay();
        }

        private bool TryUseManualDisplay()
        {
            Transform manualDisplay = FindManualDisplay();

            if (manualDisplay == null)
                return false;

            displayRoot = manualDisplay.gameObject;
            isManualDisplay = true;
            panelTransform = FindExistingPanel(displayRoot.transform);
            timerText = FindTimerText(displayRoot.transform);

            if (panelTransform == null)
                panelTransform = CreateTimerPanel(displayRoot.transform);

            if (timerText == null)
                timerText = CreateTimerText(displayRoot.transform);

            Debug.Log($"[LevelTimer] Using manually placed display: {GetTransformPath(manualDisplay)}.");
            return true;
        }

        private bool TryUseAlarmScreenDisplay()
        {
            if (screenTarget == null)
                return false;

            displayRoot = screenTarget.gameObject;
            isManualDisplay = true;
            panelTransform = null;
            timerText = FindTimerText(displayRoot.transform);

            TintScreenBlack(screenTarget);

            if (timerText == null)
                timerText = CreateTimerText(displayRoot.transform);

            FitTextToScreen(screenTarget);
            Debug.Log($"[LevelTimer] Using alarm screen as timer display: {GetTransformPath(screenTarget)}.");
            return true;
        }

        private Transform CreateTimerPanel(Transform parent)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Timer Digital Face";
            panel.transform.SetParent(parent, false);
            panel.transform.localScale = new Vector3(0.35f, 0.12f, 0.012f);

            Collider panelCollider = panel.GetComponent<Collider>();

            if (panelCollider != null)
                Destroy(panelCollider);

            Renderer panelRenderer = panel.GetComponent<Renderer>();

            if (panelRenderer != null)
            {
                Material panelMaterial = new Material(Shader.Find("Standard"));
                panelMaterial.color = new Color(0.015f, 0.018f, 0.014f);
                panelMaterial.SetColor("_EmissionColor", new Color(0.005f, 0.02f, 0.006f));
                panelMaterial.EnableKeyword("_EMISSION");
                panelRenderer.material = panelMaterial;
            }

            return panel.transform;
        }

        private TMP_Text CreateTimerText(Transform parent)
        {
            TMP_Text existingText = FindTimerText(parent);

            if (existingText != null)
                return existingText;

            GameObject textObject = new GameObject(TimerTextObjectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = Vector3.forward * 0.0125f;
            textObject.transform.localRotation = Quaternion.Euler(screenLocalRotation);
            textObject.transform.localScale = Vector3.one * 0.045f;

            TMP_Text text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = alarmScreenTextFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = alarmScreenTextRectSize;
            return text;
        }

        private TMP_Text FindTimerText(Transform parent)
        {
            if (parent == null)
                return null;

            TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];

                if (text == null || !IsTimerTextName(text.name))
                    continue;

                if (text.transform.parent != parent)
                    text.transform.SetParent(parent, false);

                return text;
            }

            return null;
        }

        private bool IsTimerTextName(string objectName)
        {
            return objectName.Equals("Timer Digit", System.StringComparison.OrdinalIgnoreCase)
                || objectName.Equals("Timer Digits", System.StringComparison.OrdinalIgnoreCase);
        }

        private void FitTextToScreen(Transform target)
        {
            if (timerText == null || target == null)
                return;

            if (useFixedAlarmScreenTextTransform)
            {
                timerText.rectTransform.localPosition = alarmScreenTextLocalPosition;
                timerText.rectTransform.localRotation = Quaternion.Euler(alarmScreenTextLocalRotation);
                timerText.rectTransform.localScale = alarmScreenTextLocalScale;
                timerText.fontSize = alarmScreenTextFontSize;
                timerText.rectTransform.sizeDelta = alarmScreenTextRectSize;
                return;
            }

            Bounds localBounds = GetLocalBounds(target, true);
            ScreenPlane screenPlane = GetScreenPlane(target, localBounds);
            float depthOffset = Mathf.Max(0.003f, screenPlane.depth * 0.5f + 0.002f);
            float textScale = Mathf.Min(screenPlane.width / 8f, screenPlane.height / 2f) * 0.82f;

            timerText.transform.localPosition = screenPlane.center + screenPlane.normal * depthOffset;
            timerText.transform.localRotation = Quaternion.LookRotation(-screenPlane.normal, screenPlane.up);
            timerText.transform.localScale = Vector3.one * Mathf.Max(textScale, 0.004f);
        }

        private ScreenPlane GetScreenPlane(Transform target, Bounds localBounds)
        {
            Vector3 size = localBounds.size;
            int depthIndex = useFixedAlarmScreenFaceDirection
                ? GetClosestAxisIndexToWorldDirection(target, alarmScreenFaceWorldDirection, size)
                : GetSmallestAxisIndex(size);
            int firstSurfaceIndex = (depthIndex + 1) % 3;
            int secondSurfaceIndex = (depthIndex + 2) % 3;

            if (GetAxisSize(size, firstSurfaceIndex) < GetAxisSize(size, secondSurfaceIndex))
            {
                int temp = firstSurfaceIndex;
                firstSurfaceIndex = secondSurfaceIndex;
                secondSurfaceIndex = temp;
            }

            Vector3 normal = useFixedAlarmScreenFaceDirection
                ? GetSignedLocalAxisTowardWorldDirection(target, depthIndex, alarmScreenFaceWorldDirection)
                : GetLocalAxis(depthIndex);
            Vector3 up = GetLocalAxis(secondSurfaceIndex);

            if (!useFixedAlarmScreenFaceDirection)
            {
                Vector3 cameraLocalDirection = GetLocalDirectionTowardCamera(target, localBounds.center);

                if (Vector3.Dot(cameraLocalDirection, normal) < 0f)
                    normal = -normal;
            }

            if (invertAlarmScreenFace)
                normal = -normal;

            if (Vector3.Dot(target.TransformDirection(up), Vector3.up) < 0f)
                up = -up;

            return new ScreenPlane
            {
                center = localBounds.center,
                normal = normal,
                up = up,
                width = Mathf.Max(GetAxisSize(size, firstSurfaceIndex), 0.02f),
                height = Mathf.Max(GetAxisSize(size, secondSurfaceIndex), 0.02f),
                depth = Mathf.Max(GetAxisSize(size, depthIndex), 0.002f)
            };
        }

        private int GetSmallestAxisIndex(Vector3 size)
        {
            if (size.x <= size.y && size.x <= size.z)
                return 0;

            if (size.y <= size.x && size.y <= size.z)
                return 1;

            return 2;
        }

        private int GetClosestAxisIndexToWorldDirection(Transform target, Vector3 worldDirection, Vector3 fallbackSize)
        {
            if (target == null || worldDirection.sqrMagnitude < 0.0001f)
                return GetSmallestAxisIndex(fallbackSize);

            Vector3 desiredDirection = worldDirection.normalized;
            int bestAxisIndex = 0;
            float bestDot = -1f;

            for (int axisIndex = 0; axisIndex < 3; axisIndex++)
            {
                Vector3 worldAxis = target.TransformDirection(GetLocalAxis(axisIndex)).normalized;
                float dot = Mathf.Abs(Vector3.Dot(worldAxis, desiredDirection));

                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestAxisIndex = axisIndex;
                }
            }

            return bestAxisIndex;
        }

        private Vector3 GetSignedLocalAxisTowardWorldDirection(Transform target, int axisIndex, Vector3 worldDirection)
        {
            Vector3 localAxis = GetLocalAxis(axisIndex);

            if (target == null || worldDirection.sqrMagnitude < 0.0001f)
                return localAxis;

            Vector3 worldAxis = target.TransformDirection(localAxis);

            if (Vector3.Dot(worldAxis, worldDirection.normalized) < 0f)
                return -localAxis;

            return localAxis;
        }

        private float GetAxisSize(Vector3 size, int axisIndex)
        {
            if (axisIndex == 0)
                return size.x;

            if (axisIndex == 1)
                return size.y;

            return size.z;
        }

        private Vector3 GetLocalAxis(int axisIndex)
        {
            if (axisIndex == 0)
                return Vector3.right;

            if (axisIndex == 1)
                return Vector3.up;

            return Vector3.forward;
        }

        private Vector3 GetLocalDirectionTowardCamera(Transform target, Vector3 localCenter)
        {
            Camera camera = Camera.main;

            if (camera == null)
                return Vector3.forward;

            Vector3 localCameraPosition = target.InverseTransformPoint(camera.transform.position);
            Vector3 direction = localCameraPosition - localCenter;

            if (direction.sqrMagnitude < 0.0001f)
                return Vector3.forward;

            return direction.normalized;
        }

        private void TintScreenBlack(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null
                    || renderer.GetComponentInParent<TMP_Text>() != null
                    || (timerText != null && renderer.transform.IsChildOf(timerText.transform)))
                {
                    continue;
                }

                Material[] materials = renderer.materials;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];

                    if (material == null)
                        continue;

                    if (material.HasProperty("_Color"))
                        material.color = screenFaceColor;

                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", screenFaceColor);

                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        private Transform FindManualDisplay()
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];

                if (candidate == null
                    || candidate == transform
                    || candidate.IsChildOf(transform)
                    || !candidate.name.Equals(displayObjectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private Transform FindExistingPanel(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null || renderer.GetComponentInParent<TMP_Text>() != null)
                    continue;

                return renderer.transform;
            }

            return null;
        }

        private string GetTransformPath(Transform target)
        {
            if (target == null)
                return string.Empty;

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private void RepositionDisplay()
        {
            if (displayRoot == null)
                return;

            if (isManualDisplay)
                return;

            if (screenTarget != null)
            {
                displayRoot.transform.SetParent(screenTarget, false);
                Bounds localBounds = GetLocalBounds(screenTarget, true);
                TintScreenBlack(screenTarget);
                displayRoot.transform.localPosition = localBounds.center + screenLocalOffset;
                displayRoot.transform.localRotation = Quaternion.identity;

                float screenWidth = Mathf.Max(localBounds.size.x, 0.02f);
                float screenHeight = Mathf.Max(localBounds.size.y, 0.02f);
                ResizeDisplay(screenWidth, screenHeight);
                return;
            }

            displayRoot.transform.SetParent(transform, false);
            Bounds bounds = alarmTarget != null ? GetWorldBounds(alarmTarget) : GetFallbackBounds();
            Vector3 towardCamera = GetDirectionTowardCamera(bounds.center);
            Vector3 position = bounds.center
                + towardCamera * Mathf.Max(0.08f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.62f)
                + Vector3.up * Mathf.Max(0.04f, bounds.size.y * 0.08f);

            displayRoot.transform.position = position;
            displayRoot.transform.rotation = Quaternion.LookRotation(towardCamera, Vector3.up);

            float fallbackWidth = Mathf.Clamp(Mathf.Max(bounds.size.x * 0.85f, 0.24f), 0.24f, 0.55f);
            float fallbackHeight = fallbackWidth * 0.34f;
            ResizeDisplay(fallbackWidth, fallbackHeight);
        }

        private void ResizeDisplay(float width, float height)
        {
            if (panelTransform != null)
                panelTransform.localScale = new Vector3(width, height, 0.012f);

            if (timerText != null)
            {
                timerText.transform.localPosition = Vector3.forward * 0.0125f;
                timerText.transform.localRotation = Quaternion.Euler(screenLocalRotation);
                timerText.transform.localScale = Vector3.one * (width / 8f);
            }
        }

        private void ResolveAlarmTarget()
        {
            alarmTarget = FindAlarmByName() ?? FindAlarmByMaterial();
            screenTarget = FindScreenTarget(alarmTarget);
        }

        private Transform FindScreenTarget(Transform alarm)
        {
            Transform screen = alarm != null ? FindChildByName(alarm, "screen") : null;

            if (screen != null)
                return screen;

            screen = alarm != null ? FindChildByName(alarm, "display") : null;

            if (screen != null)
                return screen;

            Transform[] transforms = FindObjectsOfType<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];

                if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
                    continue;

                string lowerName = candidate.name.ToLowerInvariant();

                if (!lowerName.Contains("screen") && !lowerName.Contains("display"))
                    continue;

                if (HasAlarmAncestor(candidate) || HasAlarmMaterial(candidate))
                    return candidate;
            }

            return null;
        }

        private Transform FindChildByName(Transform root, string requiredNamePart)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];

                if (child == null || child == root)
                    continue;

                string lowerName = child.name.ToLowerInvariant();

                if (lowerName.Contains(requiredNamePart))
                    return child;
            }

            return null;
        }

        private bool HasAlarmAncestor(Transform target)
        {
            Transform current = target;

            while (current != null)
            {
                string lowerName = current.name.ToLowerInvariant();

                if (IsAlarmName(lowerName))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private bool HasAlarmMaterial(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInParent<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (RendererHasAlarmMaterial(renderers[i]))
                    return true;
            }

            renderers = target.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (RendererHasAlarmMaterial(renderers[i]))
                    return true;
            }

            return false;
        }

        private Transform FindAlarmByName()
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];

                if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
                    continue;

                string lowerName = candidate.name.ToLowerInvariant();

                if (IsAlarmName(lowerName))
                    return candidate;
            }

            return null;
        }

        private bool IsAlarmName(string lowerName)
        {
            return lowerName.Contains("conair")
                || lowerName.Contains("conaire")
                || lowerName.Contains("connaire")
                || lowerName.Contains("alarm")
                || lowerName.Contains("clock")
                || lowerName.Contains("reveil")
                || lowerName.Contains("r\u00e9veil");
        }

        private Transform FindAlarmByMaterial()
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null || renderer.GetComponentInParent<TMP_Text>() != null)
                    continue;

                if (RendererHasAlarmMaterial(renderer))
                    return renderer.transform;
            }

            return null;
        }

        private bool RendererHasAlarmMaterial(Renderer renderer)
        {
            if (renderer == null)
                return false;

            Material[] materials = renderer.sharedMaterials;

            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];

                if (material == null)
                    continue;

                string lowerName = material.name.ToLowerInvariant();

                if (lowerName.Contains("alarm")
                    || lowerName.Contains("alarmclock")
                    || lowerName.Contains("clock"))
                {
                    return true;
                }
            }

            return false;
        }

        private Bounds GetFallbackBounds()
        {
            Transform anchor = FindTransformByName(fallbackAnchorName)
                ?? FindTransformByName("WorkTable")
                ?? FindTransformByName("WorkBench")
                ?? (Camera.main != null ? Camera.main.transform : null);

            if (anchor == null)
                return new Bounds(new Vector3(0f, 1.1f, -1.2f), Vector3.one * 0.4f);

            if (anchor == Camera.main?.transform)
                return new Bounds(anchor.position + anchor.forward * 1.25f + Vector3.down * 0.18f, Vector3.one * 0.35f);

            return GetWorldBounds(anchor);
        }

        private Bounds GetWorldBounds(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(target.position, Vector3.one * 0.35f);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null || !renderer.enabled || renderer.GetComponentInParent<TMP_Text>() != null)
                    continue;

                if (renderer.transform.IsChildOf(transform)
                    || (displayRoot != null
                        && displayRoot.transform != target
                        && renderer.transform.IsChildOf(displayRoot.transform)))
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private Bounds GetLocalBounds(Transform target, bool includeDisabled = false)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(0.28f, 0.11f, 0.02f));

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null
                    || (!includeDisabled && !renderer.enabled)
                    || renderer.GetComponentInParent<TMP_Text>() != null)
                {
                    continue;
                }

                if (displayRoot != null
                    && displayRoot.transform != target
                    && renderer.transform.IsChildOf(displayRoot.transform))
                    continue;

                Bounds rendererBounds = ToLocalBounds(target, renderer.bounds);

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            if (!hasBounds)
                return bounds;

            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, 0.18f);
            size.y = Mathf.Max(size.y, 0.08f);
            size.z = Mathf.Max(size.z, 0.02f);
            bounds.size = size;
            return bounds;
        }

        private Bounds ToLocalBounds(Transform root, Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds bounds = new Bounds(root.InverseTransformPoint(min), Vector3.zero);
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            bounds.Encapsulate(root.InverseTransformPoint(max));
            return bounds;
        }

        private Vector3 GetDirectionTowardCamera(Vector3 origin)
        {
            Camera camera = Camera.main;

            if (camera == null)
                return Vector3.back;

            Vector3 direction = camera.transform.position - origin;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = -camera.transform.forward;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                return Vector3.back;

            direction.Normalize();
            return direction;
        }

        private Transform FindTransformByName(string objectName)
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null
                    && transforms[i].name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }
    }
}
