using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class SessionResetButton : MonoBehaviour
    {
        private const string ButtonName = "Bouton Reinitialiser";
        private const string GameplaySceneName = "VR Scene 1";

        private TMP_Text label;
        private bool isResetting;
        private bool leftPrimaryWasPressed;
        private static bool resetInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateButtonAfterSceneLoad()
        {
            EnsureButtonExists();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetRuntimeState();
            CameraRenderGuard.EnsureCameraIsRenderingNow();
            WakeupIntroBootstrap.EnsureWakeupIntroExists();
            EnsureButtonExists();
            resetInProgress = false;
        }

        private static void EnsureButtonExists()
        {
            if (Object.FindObjectOfType<SessionResetButton>() != null)
                return;

            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = ButtonName;
            button.transform.localScale = new Vector3(0.28f, 0.06f, 0.16f);

            SessionResetButton resetButton = button.AddComponent<SessionResetButton>();
            resetButton.PlaceInScene();
            resetButton.BuildVisuals();
        }

        private void Awake()
        {
            Collider buttonCollider = GetComponent<Collider>();
            buttonCollider.isTrigger = true;

            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void LateUpdate()
        {
            if (label == null || Camera.main == null)
                return;

            Vector3 toLabel = label.transform.position - Camera.main.transform.position;

            if (toLabel.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(toLabel.normalized, Vector3.up);
        }

        private void Update()
        {
            if (!isResetting && ResetHotkeyPressedThisFrame())
            {
                isResetting = true;
                ResetSessionNow("left controller X button");
            }
        }

        private void OnMouseDown()
        {
            ResetSession();
        }

        public void ResetSession()
        {
            if (isResetting)
                return;

            isResetting = true;
            ResetSessionNow("reset button");
        }

        public static void ResetSessionNow(string reason)
        {
            if (resetInProgress)
                return;

            resetInProgress = true;
            ResetRuntimeState();
            StopAllAudioSources();
            DestroyPersistentSessionObjects();

            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log($"[SessionResetButton] Reloading session from scene '{activeScene.name}' buildIndex={activeScene.buildIndex} path='{activeScene.path}'. reason={reason}");

            SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            ResetSession();
        }

        private bool ResetHotkeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.JoystickButton2))
                return true;
#endif

            UnityEngine.XR.InputDevice leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);

            if (!leftHand.isValid)
            {
                leftPrimaryWasPressed = false;
                return false;
            }

            if (!leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed))
                pressed = false;

            bool pressedThisFrame = pressed && !leftPrimaryWasPressed;
            leftPrimaryWasPressed = pressed;
            return pressedThisFrame;
        }

        private static void ResetRuntimeState()
        {
            PlayerMovementLock.ForceUnlockAll(resetInProgress ? "session reset" : "scene loaded");
            WorktableParticleSpawner.ResetUnlocks();
            BucketAssembler.ResetCompletedRecipes();
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private static void StopAllAudioSources()
        {
            AudioSource[] sources = Object.FindObjectsOfType<AudioSource>(true);

            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                    sources[i].Stop();
            }
        }

        private static void DestroyPersistentSessionObjects()
        {
            DestroyAllNow<ObjectiveHud>();
            DestroyAllNow<UraniumDeliveryObjectiveController>();
        }

        private static void DestroyAllNow<T>() where T : Component
        {
            T[] components = Object.FindObjectsOfType<T>(true);

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    Object.DestroyImmediate(components[i].gameObject);
            }
        }

        private void PlaceInScene()
        {
            Transform table = FindTransformByName("WorkTable")
                ?? FindTransformByName("WorkBench")
                ?? FindTransformByName("Table");

            if (table == null || !TryGetRendererBounds(table, out Bounds bounds))
            {
                PlaceInFrontOfCamera();
                return;
            }

            Vector3 towardPlayer = Camera.main != null
                ? Camera.main.transform.position - bounds.center
                : -table.forward;
            towardPlayer.y = 0f;

            if (towardPlayer.sqrMagnitude < 0.0001f)
                towardPlayer = -table.forward;

            towardPlayer.Normalize();
            float edgeDistance = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.72f;
            transform.position = bounds.center
                + towardPlayer * edgeDistance
                + Vector3.up * (bounds.extents.y + 0.08f);
            transform.rotation = Quaternion.LookRotation(-towardPlayer, Vector3.up);
        }

        private void PlaceInFrontOfCamera()
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                transform.position = new Vector3(0f, 1.1f, -1.2f);
                transform.rotation = Quaternion.identity;
                return;
            }

            transform.position = camera.transform.position + camera.transform.forward * 1.4f + Vector3.down * 0.35f;
            transform.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
        }

        private void BuildVisuals()
        {
            Renderer renderer = GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = new Color(0.9f, 0.08f, 0.06f);
                renderer.material = material;
            }

            GameObject labelObject = new GameObject("Texte Reinitialiser");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            labelObject.transform.localScale = Vector3.one * 0.018f;

            label = labelObject.AddComponent<TextMeshPro>();
            label.text = "REINITIALISER";
            label.fontSize = 4.2f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.rectTransform.sizeDelta = new Vector2(18f, 3f);
        }

        private static Transform FindTransformByName(string objectName)
        {
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);

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

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
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
    }
}
