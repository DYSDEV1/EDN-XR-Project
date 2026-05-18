using UnityEngine;
using UnityEngine.SceneManagement;

namespace EDNXR.Gameplay
{
    public class CameraRenderGuard : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureGuardExists();
            EnsureCameraIsRendering();
        }

        public static void EnsureCameraIsRenderingNow()
        {
            EnsureGuardExists();
            EnsureCameraIsRendering();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureCameraIsRenderingNow();
        }

        private static void EnsureGuardExists()
        {
            if (FindObjectOfType<CameraRenderGuard>() != null)
                return;

            GameObject guard = new GameObject("Camera Render Guard");
            guard.AddComponent<CameraRenderGuard>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureCameraIsRendering();
        }

        private void Update()
        {
            EnsureCameraIsRendering();
        }

        private void LateUpdate()
        {
            EnsureCameraIsRendering();
        }

        private static void EnsureCameraIsRendering()
        {
            Camera activeCamera = FindActiveRenderingCamera();

            if (activeCamera != null)
            {
                EnsureAudioListener(activeCamera);
                return;
            }

            Camera fallbackCamera = FindBestInactiveCamera();

            if (fallbackCamera != null)
            {
                ActivateHierarchy(fallbackCamera.transform);
                fallbackCamera.enabled = true;
                fallbackCamera.targetDisplay = 0;
                EnsureAudioListener(fallbackCamera);
                Debug.LogWarning($"[CameraRenderGuard] Re-enabled camera '{fallbackCamera.name}' because Display 1 had no rendering camera.");
                return;
            }

            GameObject cameraObject = new GameObject("Fallback Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.6f, -3f);
            camera.transform.rotation = Quaternion.identity;
            EnsureAudioListener(camera);
            Debug.LogWarning("[CameraRenderGuard] Created a fallback camera because no camera was available in the scene.");
        }

        private static Camera FindActiveRenderingCamera()
        {
            Camera[] cameras = FindObjectsOfType<Camera>();

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null
                    && cameras[i].isActiveAndEnabled
                    && cameras[i].targetTexture == null
                    && cameras[i].targetDisplay == 0)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private static Camera FindBestInactiveCamera()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                return mainCamera;

            Camera[] cameras = FindObjectsOfType<Camera>(true);
            Camera firstCamera = null;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];

                if (camera == null)
                    continue;

                if (firstCamera == null)
                    firstCamera = camera;

                if (camera.name.IndexOf("Main Camera", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return camera;
            }

            return firstCamera;
        }

        private static void ActivateHierarchy(Transform transform)
        {
            if (transform.parent != null)
                ActivateHierarchy(transform.parent);

            if (!transform.gameObject.activeSelf)
                transform.gameObject.SetActive(true);
        }

        private static void EnsureAudioListener(Camera camera)
        {
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();

            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].isActiveAndEnabled)
                    return;
            }

            AudioListener listener = camera.GetComponent<AudioListener>();

            if (listener == null)
                listener = camera.gameObject.AddComponent<AudioListener>();

            listener.enabled = true;
        }
    }
}
