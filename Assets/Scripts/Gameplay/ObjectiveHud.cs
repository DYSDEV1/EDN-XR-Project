using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EDNXR.Gameplay
{
    public class ObjectiveHud : MonoBehaviour
    {
        public static ObjectiveHud Instance { get; private set; }

        [SerializeField] private string startingMessage = "Rallumer les lumieres";

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

        public void SetMessage(string message)
        {
            if (objectiveText != null)
                objectiveText.text = message;
        }

        private void BuildHud()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            gameObject.AddComponent<CanvasScaler>();

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
    }
}
