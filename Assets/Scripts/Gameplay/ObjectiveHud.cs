using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace EDNXR.Gameplay
{
    public class ObjectiveHud : MonoBehaviour
    {
        public static ObjectiveHud Instance { get; private set; }

        [SerializeField] private string startingMessage = "Rallumer les lumieres";
        [SerializeField] private Vector3 vrHudLocalPosition = new Vector3(0f, -0.38f, 1.15f);
        [SerializeField] private Vector2 vrHudSize = new Vector2(1.15f, 0.16f);

        private Canvas canvas;
        private TMP_Text objectiveText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildHud();
            SetMessage(startingMessage);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (!XRSettings.isDeviceActive || canvas == null)
                return;

            Camera mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = mainCamera;
            }

            if (canvas.transform.parent == mainCamera.transform)
                return;

            AttachCanvasToVrCamera(mainCamera);
        }

        public void SetMessage(string message)
        {
            if (objectiveText != null)
                objectiveText.text = message;
        }

        private void BuildHud()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.sortingOrder = 900;
            gameObject.AddComponent<CanvasScaler>();
            ConfigureCanvasForCurrentMode();

            GameObject background = new GameObject("Objective Background");
            background.transform.SetParent(transform, false);
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform backgroundRect = backgroundImage.rectTransform;
            backgroundRect.anchorMin = new Vector2(0.18f, 0.035f);
            backgroundRect.anchorMax = new Vector2(0.82f, 0.13f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            GameObject textObject = new GameObject("Objective Text");
            textObject.transform.SetParent(background.transform, false);
            objectiveText = textObject.AddComponent<TextMeshProUGUI>();
            objectiveText.fontSize = 28f;
            objectiveText.color = Color.white;
            objectiveText.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = objectiveText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);
        }

        private void ConfigureCanvasForCurrentMode()
        {
            if (!XRSettings.isDeviceActive)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            Camera mainCamera = Camera.main;

            if (mainCamera == null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
            AttachCanvasToVrCamera(mainCamera);
        }

        private void AttachCanvasToVrCamera(Camera mainCamera)
        {
            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            rectTransform.SetParent(mainCamera.transform, false);
            rectTransform.localPosition = vrHudLocalPosition;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one * 0.001f;
            rectTransform.sizeDelta = vrHudSize * 1000f;
        }
    }
}
