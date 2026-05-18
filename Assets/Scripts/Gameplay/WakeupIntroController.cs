using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    public class WakeupIntroController : MonoBehaviour
    {
        [SerializeField] private string couchName = "Couch";
        [SerializeField] private string xrOriginName = "XR Origin (XR Rig)";
        [SerializeField] private string snoreClipName = "ronflement";
        [SerializeField] private string yawnClipName = "Baillement";
        [SerializeField] private float sleepingEyeHeight = 0.85f;
        [SerializeField] private float frontDistance = 1.55f;
        [SerializeField] private float awakeEyeHeight = 1.55f;
        [SerializeField] private float fadeDuration = 2.2f;
        [SerializeField] private float sleepingMusicCutoff = 650f;
        [SerializeField] private float vrOverlayDistance = 0.9f;
        [SerializeField] private Vector2 vrOverlaySize = new Vector2(2.6f, 2f);

        private Transform xrOrigin;
        private XROrigin xrRig;
        private Transform couch;
        private Camera mainCamera;
        private Canvas canvas;
        private Image blackImage;
        private TMP_Text promptText;
        private AudioSource audioSource;
        private PcPlayerController pcController;
        private PcMouseGrabber pcGrabber;
        private AudioLowPassFilter[] sleepingFilters;
        private bool introStarted;
        private bool wakingUp;
        private bool movementLocked;

        private void Start()
        {
            BeginIntro();
        }

        public bool BeginIntro()
        {
            if (introStarted)
                return true;

            xrOrigin = FindTransformByName(xrOriginName);
            xrRig = xrOrigin != null ? xrOrigin.GetComponent<XROrigin>() : null;
            mainCamera = Camera.main;
            couch = FindTransformByName(couchName);

            if (xrOrigin == null || couch == null)
                return false;

            introStarted = true;

            BuildOverlay();
            SetupAudio();
            ApplySleepingAudioMuffle();
            MovePlayerToCouch();
            PlayerMovementLock.Lock("wakeup intro");
            movementLocked = true;
            SetPcControlsEnabled(false);
            return true;
        }

        private void OnDestroy()
        {
            if (movementLocked)
            {
                PlayerMovementLock.Unlock("wakeup intro destroyed");
                movementLocked = false;
            }
        }

        private void Update()
        {
            if (wakingUp || blackImage == null)
                return;

            SetPcControlsEnabled(false);

            if (WakeInputPressed())
                StartCoroutine(WakeUpRoutine());
        }

        private void BuildOverlay()
        {
            GameObject canvasObject = new GameObject("Wakeup Intro Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.sortingOrder = 1000;
            canvasObject.AddComponent<CanvasScaler>();
            ConfigureCanvasForCurrentMode(canvas);

            GameObject blackObject = new GameObject("Black Screen");
            blackObject.transform.SetParent(canvasObject.transform, false);
            blackImage = blackObject.AddComponent<Image>();
            blackImage.color = Color.black;
            RectTransform blackRect = blackImage.rectTransform;
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;

            GameObject textObject = new GameObject("Wake Prompt");
            textObject.transform.SetParent(canvasObject.transform, false);
            promptText = textObject.AddComponent<TextMeshProUGUI>();
            promptText.text = BuildPromptText();
            promptText.fontSize = 42f;
            promptText.color = Color.white;
            promptText.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = promptText.rectTransform;
            textRect.anchorMin = new Vector2(0.1f, 0.35f);
            textRect.anchorMax = new Vector2(0.9f, 0.65f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void ConfigureCanvasForCurrentMode(Canvas targetCanvas)
        {
            if (!IsVrActive() || mainCamera == null)
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            targetCanvas.renderMode = RenderMode.WorldSpace;
            targetCanvas.worldCamera = mainCamera;
            RectTransform rectTransform = targetCanvas.GetComponent<RectTransform>();
            rectTransform.SetParent(mainCamera.transform, false);
            rectTransform.localPosition = new Vector3(0f, 0f, vrOverlayDistance);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one * 0.001f;
            rectTransform.sizeDelta = vrOverlaySize * 1000f;
        }

        private void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.75f;

            AudioClip snoreClip = Resources.Load<AudioClip>(snoreClipName);

            if (snoreClip != null)
            {
                audioSource.clip = snoreClip;
                audioSource.Play();
            }
        }

        private void MovePlayerToCouch()
        {
            Bounds bounds = GetCouchBounds();
            Vector3 position = bounds.center;
            position.y = bounds.min.y + sleepingEyeHeight;
            MoveCameraToWorldPosition(position);
            LookAtCouch(bounds.center);
        }

        private IEnumerator WakeUpRoutine()
        {
            wakingUp = true;

            if (promptText != null)
                promptText.enabled = false;

            if (audioSource != null)
                audioSource.Stop();

            RestoreSleepingAudio();

            AudioClip yawnClip = Resources.Load<AudioClip>(yawnClipName);

            if (yawnClip != null)
                AudioSource.PlayClipAtPoint(yawnClip, xrOrigin.position, 0.9f);

            Bounds bounds = GetCouchBounds();
            Vector3 frontPosition = GetCouchFrontPosition(bounds);
            MoveCameraToWorldPosition(frontPosition);
            LookAtCouch(bounds.center);

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                blackImage.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }

            SetPcControlsEnabled(true);
            wakingUp = false;
            if (movementLocked)
            {
                PlayerMovementLock.Unlock("wakeup intro");
                movementLocked = false;
            }

            LevelTimerController.NotifyLevelStarted();

            if (canvas != null)
                Destroy(canvas.gameObject);

            Destroy(gameObject);
        }

        private void ApplySleepingAudioMuffle()
        {
            AudioSource[] sources = Object.FindObjectsOfType<AudioSource>();
            System.Collections.Generic.List<AudioLowPassFilter> filters = new System.Collections.Generic.List<AudioLowPassFilter>();

            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null || sources[i] == audioSource)
                    continue;

                AudioLowPassFilter filter = sources[i].GetComponent<AudioLowPassFilter>();

                if (filter == null)
                    filter = sources[i].gameObject.AddComponent<AudioLowPassFilter>();

                filter.cutoffFrequency = sleepingMusicCutoff;
                filter.lowpassResonanceQ = 1f;
                filter.enabled = true;
                filters.Add(filter);
            }

            sleepingFilters = filters.ToArray();
        }

        private void RestoreSleepingAudio()
        {
            if (sleepingFilters == null)
                return;

            for (int i = 0; i < sleepingFilters.Length; i++)
            {
                if (sleepingFilters[i] != null)
                    sleepingFilters[i].enabled = false;
            }
        }

        private Vector3 GetCouchFrontPosition(Bounds bounds)
        {
            Vector3 awayFromCouch = couch.forward;

            if (mainCamera != null)
            {
                Vector3 cameraDirection = mainCamera.transform.position - bounds.center;
                cameraDirection.y = 0f;

                if (cameraDirection.sqrMagnitude > 0.001f)
                    awayFromCouch = cameraDirection.normalized;
            }

            awayFromCouch.y = 0f;
            awayFromCouch.Normalize();

            Vector3 position = bounds.center + awayFromCouch * frontDistance;
            position.y = bounds.min.y + awakeEyeHeight;
            return position;
        }

        private void MoveCameraToWorldPosition(Vector3 cameraWorldPosition)
        {
            CharacterController characterController = xrOrigin != null ? xrOrigin.GetComponent<CharacterController>() : null;
            bool wasControllerEnabled = characterController != null && characterController.enabled;

            if (characterController != null)
                characterController.enabled = false;

            if (xrRig != null)
            {
                xrRig.MoveCameraToWorldLocation(cameraWorldPosition);
            }
            else if (mainCamera != null)
            {
                xrOrigin.position += cameraWorldPosition - mainCamera.transform.position;
            }
            else
            {
                xrOrigin.position = cameraWorldPosition;
            }

            if (characterController != null)
                characterController.enabled = wasControllerEnabled;

            Physics.SyncTransforms();
        }

        private void LookAtCouch(Vector3 target)
        {
            Vector3 direction = target - xrOrigin.position;

            if (mainCamera != null)
                direction = target - mainCamera.transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                if (xrRig != null)
                {
                    CharacterController characterController = xrOrigin != null ? xrOrigin.GetComponent<CharacterController>() : null;
                    bool wasControllerEnabled = characterController != null && characterController.enabled;

                    if (characterController != null)
                        characterController.enabled = false;

                    xrRig.MatchOriginUpCameraForward(Vector3.up, direction.normalized);

                    if (characterController != null)
                        characterController.enabled = wasControllerEnabled;
                }
                else
                {
                    xrOrigin.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }

        private Bounds GetCouchBounds()
        {
            Renderer[] renderers = couch.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return new Bounds(couch.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private void SetPcControlsEnabled(bool enabled)
        {
            if (mainCamera == null)
                return;

            if (pcController == null)
                pcController = mainCamera.GetComponent<PcPlayerController>();

            if (pcGrabber == null)
                pcGrabber = mainCamera.GetComponent<PcMouseGrabber>();

            if (pcController != null)
                pcController.enabled = enabled;

            if (pcGrabber != null)
                pcGrabber.enabled = enabled;
        }

        private bool WakeInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetMouseButtonDown(0)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetKeyDown(KeyCode.JoystickButton14)
                || Input.GetKeyDown(KeyCode.JoystickButton15))
            {
                return true;
            }
#endif

            return IsXRWakeButtonPressed();
        }

        private bool IsXRWakeButtonPressed()
        {
            return IsXRButtonPressed(UnityEngine.XR.XRNode.LeftHand)
                || IsXRButtonPressed(UnityEngine.XR.XRNode.RightHand);
        }

        private bool IsXRButtonPressed(UnityEngine.XR.XRNode node)
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

        private string BuildPromptText()
        {
            if (IsVrActive())
                return "Appuyer sur A ou sur la gachette";

#if ENABLE_INPUT_SYSTEM
            return "Appuyer sur Espace";
#else
            return "Appuyer sur Espace";
#endif
        }

        private bool IsVrActive()
        {
            return XRSettings.isDeviceActive;
        }

        private Transform FindTransformByName(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.transform : null;
        }
    }
}
