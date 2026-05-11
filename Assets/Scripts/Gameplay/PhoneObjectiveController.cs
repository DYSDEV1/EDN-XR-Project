using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class PhoneObjectiveController : MonoBehaviour
    {
        private struct BackgroundAudioState
        {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
            public bool CreatedFilter;
            public bool WasFilterEnabled;
            public float CutoffFrequency;
            public float ResonanceQ;
            public float Volume;

            public BackgroundAudioState(
                AudioSource source,
                AudioLowPassFilter filter,
                bool createdFilter,
                bool wasFilterEnabled,
                float cutoffFrequency,
                float resonanceQ,
                float volume)
            {
                Source = source;
                Filter = filter;
                CreatedFilter = createdFilter;
                WasFilterEnabled = wasFilterEnabled;
                CutoffFrequency = cutoffFrequency;
                ResonanceQ = resonanceQ;
                Volume = volume;
            }
        }

        public static PhoneObjectiveController Instance { get; private set; }

        private const string PhoneObjectName = "Phone";

        [Header("Objective")]
        [SerializeField] private string objectiveMessage = "Repondre au telephone";

        [Header("Audio")]
        [SerializeField] private string ringClipName = "telephone";
        [SerializeField] private string voiceClipName = "VoixVilain";
        [SerializeField] private string hangupClipName = "Raccroche";
        [SerializeField] private float hangupDelay = 0.5f;
        [SerializeField] private float volume = 0.9f;
        [SerializeField] private bool debugAudio = true;
        [SerializeField] private float backgroundMuffleCutoff = 850f;
        [SerializeField] private float backgroundMuffleResonance = 1.15f;
        [SerializeField, Range(0f, 1f)] private float backgroundMuffleVolumeMultiplier = 0.32f;

        [Header("Interaction")]
        [SerializeField] private float interactDistance = 4f;

        [Header("Highlight")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.88f, 0.28f);
        [SerializeField] private float highlightPulseSpeed = 4f;
        [SerializeField] private float haloScale = 1.45f;

        private Transform phoneTransform;
        private Camera mainCamera;
        private AudioSource ringSource;
        private AudioSource callSource;
        private AudioClip ringClip;
        private AudioClip voiceClip;
        private AudioClip hangupClip;
        private AudioLowPassFilter voiceLowPassFilter;
        private AudioHighPassFilter voiceHighPassFilter;
        private AudioDistortionFilter voiceDistortionFilter;
        private readonly List<BackgroundAudioState> muffledBackgroundAudio = new List<BackgroundAudioState>();
        private GameObject highlightObject;
        private Renderer highlightRenderer;
        private Material highlightMaterial;
        private bool objectiveActive;
        private bool hasAnswered;
        private bool callInProgress;
        private bool leftPrimaryWasPressed;
        private bool rightPrimaryWasPressed;

        public static PhoneObjectiveController EnsureInScene()
        {
            if (Instance != null)
                return Instance;

            GameObject phone = FindPhoneObject();

            if (phone == null)
            {
                Debug.LogWarning("[PhoneObjective] Bootstrap failed: no object named 'Phone' found in the loaded scene.");
                return null;
            }

            PhoneObjectiveController controller = phone.GetComponent<PhoneObjectiveController>();

            if (controller == null)
            {
                controller = phone.AddComponent<PhoneObjectiveController>();
                Debug.Log($"[PhoneObjective] Added controller to '{phone.name}' at pos={phone.transform.position}.");
            }

            return controller;
        }

        public static bool TryActivatePhoneObjective()
        {
            PhoneObjectiveController controller = EnsureInScene();

            if (controller == null)
                return false;

            controller.ActivateObjective();
            return true;
        }

        private static GameObject FindPhoneObject()
        {
            GameObject found = GameObject.Find(PhoneObjectName);

            if (found != null)
                return found;

            GameObject[] objects = Object.FindObjectsOfType<GameObject>(true);

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null
                    && string.Equals(objects[i].name, PhoneObjectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return objects[i];
                }
            }

            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            phoneTransform = transform;
            mainCamera = Camera.main;
            MakeDynamic();
            EnsureCollider();
            EnsureXRInteraction();
            EnsureAudio();
            StartRinging();
            Debug.Log($"[PhoneObjective] Awake ready on '{name}'. ring={(ringClip != null ? ringClip.name : "missing")}, voice={(voiceClip != null ? voiceClip.name : "missing")}, hangup={(hangupClip != null ? hangupClip.name : "missing")}.");
            LogAudioSourceState("awake-ring-source", ringSource);
            LogAudioSourceState("awake-call-source", callSource);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (highlightMaterial != null)
                Destroy(highlightMaterial);

            RestoreBackgroundAudio();
        }

        private void Update()
        {
            if (!objectiveActive || hasAnswered)
                return;

            UpdateHighlightPulse();
        }

        private void LateUpdate()
        {
            if (!objectiveActive || hasAnswered || highlightObject == null)
                return;

            Bounds bounds = GetWorldBounds();
            highlightObject.transform.position = bounds.center;
            highlightObject.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.3f) * haloScale;

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
            {
                Vector3 toMarker = highlightObject.transform.position - mainCamera.transform.position;

                if (toMarker.sqrMagnitude > 0.001f)
                    highlightObject.transform.rotation = Quaternion.LookRotation(toMarker.normalized, Vector3.up);
            }
        }

        public void ActivateObjective()
        {
            if (hasAnswered)
            {
                GlovesObjectiveController.TryActivateGlovesObjective();
                return;
            }

            objectiveActive = true;

            if (ObjectiveHud.Instance != null)
                ObjectiveHud.Instance.SetMessage(objectiveMessage);

            EnableHighlight();
            StartRinging();
            Debug.Log("[PhoneObjective] Objective activated, phone is already ringing.");
        }

        public void TryAnswer()
        {
            Debug.Log($"[PhoneObjective] TryAnswer called. objectiveActive={objectiveActive}, hasAnswered={hasAnswered}, callInProgress={callInProgress}.");

            if (!objectiveActive || hasAnswered || callInProgress)
                return;

            StartCoroutine(AnswerRoutine());
        }

        private IEnumerator AnswerRoutine()
        {
            callInProgress = true;
            hasAnswered = true;
            objectiveActive = false;
            DisableHighlight();
            StopRinging();
            MuffleBackgroundAudio();
            PlayerMovementLock.Lock("phone call");
            Debug.Log($"[PhoneObjective] AnswerRoutine started. voiceClipName='{voiceClipName}', voiceClip={(voiceClip != null ? $"{voiceClip.name}, length={voiceClip.length:F2}, loadState={voiceClip.loadState}, frequency={voiceClip.frequency}, channels={voiceClip.channels}" : "null")}.");
            LogAudioSourceState("before-voice-play", callSource);

            if (voiceClip == null)
            {
                Debug.LogWarning("[PhoneObjective] VoixVilain audio is missing. Cannot play hangup because the voice never started.");
                RestoreBackgroundAudio();
                PlayerMovementLock.Unlock("phone call");
                callInProgress = false;
                GlovesObjectiveController.TryActivateGlovesObjective();
                yield break;
            }

            SetTelephoneVoiceEffect(true);
            callSource.Stop();
            callSource.clip = voiceClip;
            callSource.loop = false;
            callSource.volume = volume;
            callSource.spatialBlend = 0f;
            callSource.Play();
            Debug.Log($"[PhoneObjective] Voice started. clip={voiceClip.name}, length={voiceClip.length:F2}s, isPlaying={callSource.isPlaying}.");
            yield return null;
            Debug.Log($"[PhoneObjective] Voice after 1 frame. isPlaying={callSource.isPlaying}, time={callSource.time:F3}, timeSamples={callSource.timeSamples}, enabled={callSource.enabled}, active={callSource.gameObject.activeInHierarchy}.");

            if (!callSource.isPlaying)
            {
                Debug.LogWarning("[PhoneObjective] Voice clip was assigned but did not start playing. Hangup will not play because the villain voice did not finish.");
                LogAudioSourceState("voice-failed-to-start", callSource);
                SetTelephoneVoiceEffect(false);
                RestoreBackgroundAudio();
                PlayerMovementLock.Unlock("phone call");
                callInProgress = false;
                GlovesObjectiveController.TryActivateGlovesObjective();
                yield break;
            }

            while (callSource != null && callSource.isPlaying)
            {
                if (debugAudio && Time.frameCount % 120 == 0)
                    Debug.Log($"[PhoneObjective] Voice playing... time={callSource.time:F2}/{voiceClip.length:F2}, volume={callSource.volume}, muted={callSource.mute}, listenerVolume={AudioListener.volume}, paused={AudioListener.pause}.");

                yield return null;
            }

            Debug.Log($"[PhoneObjective] Voice finished. finalTime={callSource.time:F2}, isPlaying={(callSource != null && callSource.isPlaying)}.");

            SetTelephoneVoiceEffect(false);
            yield return new WaitForSeconds(hangupDelay);

            if (hangupClip != null)
            {
                callSource.Stop();
                callSource.clip = hangupClip;
                callSource.loop = false;
                callSource.volume = volume;
                callSource.spatialBlend = 0f;
                callSource.Play();
                Debug.Log($"[PhoneObjective] Hangup started. length={hangupClip.length:F2}s.");

                while (callSource != null && callSource.isPlaying)
                    yield return null;
            }

            RestoreBackgroundAudio();
            PlayerMovementLock.Unlock("phone call");
            callInProgress = false;
            GlovesObjectiveController.TryActivateGlovesObjective();
            Debug.Log("[PhoneObjective] Call completed, moving to gloves objective.");
        }

        private void StartRinging()
        {
            if (ringSource == null || ringClip == null)
                return;

            if (ringSource.isPlaying && ringSource.clip == ringClip)
                return;

            SetTelephoneVoiceEffect(false);
            ringSource.clip = ringClip;
            ringSource.loop = true;
            ringSource.volume = volume;
            ringSource.Play();
            Debug.Log("[PhoneObjective] Phone ringing started.");
        }

        private void StopRinging()
        {
            if (ringSource == null)
                return;

            ringSource.Stop();
            ringSource.clip = null;
            Debug.Log("[PhoneObjective] Phone ringing stopped immediately.");
        }

        private void EnsureAudio()
        {
            AudioSource[] sources = GetComponents<AudioSource>();

            ringSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            callSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

            ConfigureAudioSource(ringSource);
            ConfigureAudioSource(callSource);
            ringClip = Resources.Load<AudioClip>(ringClipName);
            voiceClip = Resources.Load<AudioClip>(voiceClipName);
            hangupClip = Resources.Load<AudioClip>(hangupClipName);

            Debug.Log(
                "[PhoneObjective] Resource load results | " +
                $"ring='{ringClipName}' => {DescribeClip(ringClip)}, " +
                $"voice='{voiceClipName}' => {DescribeClip(voiceClip)}, " +
                $"hangup='{hangupClipName}' => {DescribeClip(hangupClip)}");

            if (voiceClip == null)
                Debug.LogWarning($"[PhoneObjective] Could not load voice clip Resources/{voiceClipName}. Expected Assets/Resources/{voiceClipName}.mp3.");

            voiceLowPassFilter = callSource.GetComponent<AudioLowPassFilter>();

            if (voiceLowPassFilter == null)
                voiceLowPassFilter = callSource.gameObject.AddComponent<AudioLowPassFilter>();

            voiceLowPassFilter.cutoffFrequency = 3200f;
            voiceLowPassFilter.lowpassResonanceQ = 1.4f;
            voiceLowPassFilter.enabled = false;

            voiceHighPassFilter = callSource.GetComponent<AudioHighPassFilter>();

            if (voiceHighPassFilter == null)
                voiceHighPassFilter = callSource.gameObject.AddComponent<AudioHighPassFilter>();

            voiceHighPassFilter.cutoffFrequency = 350f;
            voiceHighPassFilter.highpassResonanceQ = 1.1f;
            voiceHighPassFilter.enabled = false;

            voiceDistortionFilter = callSource.GetComponent<AudioDistortionFilter>();

            if (voiceDistortionFilter == null)
                voiceDistortionFilter = callSource.gameObject.AddComponent<AudioDistortionFilter>();

            voiceDistortionFilter.distortionLevel = 0.12f;
            voiceDistortionFilter.enabled = false;
        }

        private void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.volume = volume;
            source.mute = false;
            source.enabled = true;
        }

        private string DescribeClip(AudioClip clip)
        {
            if (clip == null)
                return "null";

            return $"{clip.name} length={clip.length:F2}s loadState={clip.loadState} preload={clip.preloadAudioData} freq={clip.frequency} channels={clip.channels}";
        }

        private void LogAudioSourceState(string label, AudioSource source)
        {
            if (!debugAudio)
                return;

            if (source == null)
            {
                Debug.LogWarning($"[PhoneObjective] AudioSource {label}: null");
                return;
            }

            Debug.Log(
                $"[PhoneObjective] AudioSource {label}: " +
                $"enabled={source.enabled}, active={source.gameObject.activeInHierarchy}, " +
                $"clip={(source.clip != null ? source.clip.name : "null")}, isPlaying={source.isPlaying}, " +
                $"volume={source.volume}, mute={source.mute}, spatialBlend={source.spatialBlend}, " +
                $"minDistance={source.minDistance}, maxDistance={source.maxDistance}, " +
                $"listenerVolume={AudioListener.volume}, listenerPause={AudioListener.pause}");
        }

        private void SetTelephoneVoiceEffect(bool enabled)
        {
            if (voiceLowPassFilter != null)
            {
                if (enabled)
                {
                    voiceLowPassFilter.cutoffFrequency = 3200f;
                    voiceLowPassFilter.lowpassResonanceQ = 1.4f;
                }

                voiceLowPassFilter.enabled = enabled;
            }

            if (voiceHighPassFilter != null)
            {
                if (enabled)
                {
                    voiceHighPassFilter.cutoffFrequency = 350f;
                    voiceHighPassFilter.highpassResonanceQ = 1.1f;
                }

                voiceHighPassFilter.enabled = enabled;
            }

            if (voiceDistortionFilter != null)
            {
                if (enabled)
                    voiceDistortionFilter.distortionLevel = 0.12f;

                voiceDistortionFilter.enabled = enabled;
            }
        }

        private void MuffleBackgroundAudio()
        {
            RestoreBackgroundAudio();

            AudioSource[] sources = Object.FindObjectsOfType<AudioSource>();

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (!ShouldMuffleSource(source))
                    continue;

                AudioLowPassFilter filter = source.GetComponent<AudioLowPassFilter>();
                bool createdFilter = false;

                if (filter == null)
                {
                    filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                    createdFilter = true;
                }

                muffledBackgroundAudio.Add(new BackgroundAudioState(
                    source,
                    filter,
                    createdFilter,
                    filter.enabled,
                    filter.cutoffFrequency,
                    filter.lowpassResonanceQ,
                    source.volume));

                filter.cutoffFrequency = backgroundMuffleCutoff;
                filter.lowpassResonanceQ = backgroundMuffleResonance;
                filter.enabled = true;
                source.volume *= backgroundMuffleVolumeMultiplier;
            }

            Debug.Log($"[PhoneObjective] Background audio muffled. sources={muffledBackgroundAudio.Count}, cutoff={backgroundMuffleCutoff}, volumeMultiplier={backgroundMuffleVolumeMultiplier}.");
        }

        private bool ShouldMuffleSource(AudioSource source)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
                return false;

            if (source == ringSource || source == callSource)
                return false;

            if (source.transform == phoneTransform || source.transform.IsChildOf(phoneTransform))
                return false;

            return source.isPlaying || source.playOnAwake || source.clip != null;
        }

        private void RestoreBackgroundAudio()
        {
            if (muffledBackgroundAudio.Count == 0)
                return;

            for (int i = 0; i < muffledBackgroundAudio.Count; i++)
            {
                BackgroundAudioState state = muffledBackgroundAudio[i];

                if (state.Source != null)
                    state.Source.volume = state.Volume;

                if (state.Filter != null)
                {
                    if (state.CreatedFilter)
                    {
                        Destroy(state.Filter);
                    }
                    else
                    {
                        state.Filter.cutoffFrequency = state.CutoffFrequency;
                        state.Filter.lowpassResonanceQ = state.ResonanceQ;
                        state.Filter.enabled = state.WasFilterEnabled;
                    }
                }
            }

            Debug.Log($"[PhoneObjective] Background audio restored. sources={muffledBackgroundAudio.Count}.");
            muffledBackgroundAudio.Clear();
        }

        private void EnsureXRInteraction()
        {
            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            TryAnswer();
        }

        private void OnMouseDown()
        {
            TryAnswer();
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

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>() != null)
                return;

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Bounds localBounds = GetLocalBounds();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }

        private Bounds GetLocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.3f);

            Bounds bounds = ToLocalBounds(renderers[0].bounds);

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(ToLocalBounds(renderers[i].bounds));

            return bounds;
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

        private Bounds GetWorldBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
                return new Bounds(transform.position, Vector3.one * 0.3f);

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private void EnableHighlight()
        {
            if (highlightObject != null)
                return;

            Bounds bounds = GetWorldBounds();
            highlightObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightObject.name = "Phone Objective Highlight";
            highlightObject.transform.position = bounds.center;
            highlightObject.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.3f) * haloScale;

            Collider markerCollider = highlightObject.GetComponent<Collider>();
            if (markerCollider != null)
                Destroy(markerCollider);

            highlightRenderer = highlightObject.GetComponent<Renderer>();
            Shader shader = Shader.Find("Unlit/Transparent");
            highlightMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            highlightMaterial.mainTexture = BuildHaloTexture();
            highlightMaterial.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.65f);
            highlightMaterial.renderQueue = 3000;

            if (highlightRenderer != null)
                highlightRenderer.material = highlightMaterial;
        }

        private void DisableHighlight()
        {
            if (highlightObject != null)
                Destroy(highlightObject);

            highlightObject = null;
            highlightRenderer = null;
        }

        private void UpdateHighlightPulse()
        {
            if (highlightRenderer == null)
                return;

            float pulse = 0.65f + Mathf.Sin(Time.time * highlightPulseSpeed) * 0.35f;
            Color markerColor = Color.Lerp(highlightColor * 0.75f, highlightColor * 1.7f, pulse);
            markerColor.a = Mathf.Lerp(0.35f, 0.78f, pulse);
            highlightRenderer.material.color = markerColor;
        }

        private Texture2D BuildHaloTexture()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
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
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private bool CanInteractFromPlayer()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
                return true;

            Bounds bounds = GetWorldBounds();
            float distance = Vector3.Distance(mainCamera.transform.position, bounds.center);

            if (distance <= interactDistance)
                return true;

            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
                return false;

            return hit.collider != null && hit.collider.GetComponentInParent<PhoneObjectiveController>() == this;
        }

        private bool InteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetKeyDown(KeyCode.JoystickButton14)
                || Input.GetKeyDown(KeyCode.JoystickButton15))
            {
                return true;
            }
#endif

            return XRPrimaryPressedThisFrame(UnityEngine.XR.XRNode.LeftHand, ref leftPrimaryWasPressed)
                || XRPrimaryPressedThisFrame(UnityEngine.XR.XRNode.RightHand, ref rightPrimaryWasPressed);
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
    }
}
