using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UniversalPopup : Popup
    {
        private const string ResourcePath = "UniversalMessagePopup";

        private static UniversalPopup _instance;

        [Header("Message")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image backgroundImage;
        [SerializeField, Min(0.1f)] private float defaultDuration = 2.5f;
        [SerializeField] private Color successColor = new Color(0.1f, 0.58f, 0.25f, 0.96f);
        [SerializeField] private Color errorColor = new Color(0.86f, 0.12f, 0.1f, 0.96f);
        [SerializeField] private Color infoColor = new Color(0.09f, 0.07f, 0.06f, 0.96f);

        [Header("Preloaded Databases")]
        [SerializeField] private ScriptableObjects.CardDatabase cardDatabase;
        public ScriptableObjects.CardDatabase CardDatabase => cardDatabase;
        public TMP_FontAsset PopupFont => messageText != null ? messageText.font : null;

        private Coroutine _hideRoutine;

        public static UniversalPopup Instance => EnsureInstance();

        public void ShowSuccessMessage(string message, float duration = 0f)
        {
            Display(message, MessageKind.Success, duration);
        }

        public void ShowErrorMessage(string message, float duration = 0f)
        {
            Display(message, MessageKind.Error, duration);
        }

        public void ShowInfoMessage(string message, float duration = 0f)
        {
            Display(message, MessageKind.Info, duration);
        }

        public void ShowMessageInstance(string message, MessageKind kind = MessageKind.Info, float duration = 0f)
        {
            Display(message, kind, duration);
        }

        public static void ShowSuccess(string message, float duration = 0f)
        {
            Instance.ShowSuccessMessage(message, duration);
        }

        public static void ShowError(string message, float duration = 0f)
        {
            Instance.ShowErrorMessage(message, duration);
        }

        public static void ShowInfo(string message, float duration = 0f)
        {
            Instance.ShowInfoMessage(message, duration);
        }

        public static void ShowMessage(string message, MessageKind kind = MessageKind.Info, float duration = 0f)
        {
            Instance.ShowMessageInstance(message, kind, duration);
        }

        private static UniversalPopup EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            _instance = FindFirstObjectByType<UniversalPopup>(FindObjectsInactive.Include);
            if (_instance != null)
            {
                MakePersistent(_instance.transform.root.gameObject);
                return _instance;
            }

            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab != null)
            {
                var parent = EnsureCanvas();
                var instance = Instantiate(prefab, parent.transform);
                instance.name = nameof(UniversalPopup);
                _instance = instance.GetComponent<UniversalPopup>() ?? instance.AddComponent<UniversalPopup>();
                _instance.AutoBind();
                _instance.gameObject.SetActive(false);
                return _instance;
            }

            return CreateFallback();
        }

        private static Canvas EnsureCanvas()
        {
            var canvasObject = new GameObject("UniversalPopupCanvas");
            MakePersistent(canvasObject);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static UniversalPopup CreateFallback()
        {
            var canvas = EnsureCanvas();
            var root = new GameObject(nameof(UniversalPopup), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -72f);
            rect.sizeDelta = new Vector2(760f, 96f);

            var textObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(32f, 16f);
            textRect.offsetMax = new Vector2(-32f, -16f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 30f;
            text.color = Color.white;

            _instance = root.AddComponent<UniversalPopup>();
            _instance.backgroundImage = root.GetComponent<Image>();
            _instance.messageText = text;
            _instance.gameObject.SetActive(false);
            return _instance;
        }

        private void Awake()
        {
            AutoBind();

#if UNITY_EDITOR
            if (cardDatabase == null)
            {
                cardDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObjects.CardDatabase>("Assets/ScriptableObjects/Cards/CardDatabase/ClassicCardDatabase.asset");
                if (cardDatabase != null)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
#endif

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            MakePersistent(transform.root.gameObject);

            ConfigureTheme();
        }

        private void Display(string message, MessageKind kind, float duration)
        {
            AutoBind();

            if (messageText != null)
                messageText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;

            if (backgroundImage != null)
                backgroundImage.color = GetColor(kind);

            Show();

            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);

            _hideRoutine = StartCoroutine(HideAfter(duration > 0f ? duration : defaultDuration));
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Hide();
            _hideRoutine = null;
        }

        private Color GetColor(MessageKind kind)
        {
            return kind switch
            {
                MessageKind.Success => successColor,
                MessageKind.Error => errorColor,
                _ => infoColor
            };
        }

        private void Start()
        {
            ConfigureTheme();
        }

        private void ConfigureTheme()
        {
            // Configure root RectTransform layout
            var rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-232f, -24f); // offset from right (Settings/Help buttons start at x=-24)
                rect.sizeDelta = new Vector2(450f, 96f);
            }

            // Configure comic outline theme
            var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.09f, 0.07f, 0.06f, 1f); // Ink
            outline.effectDistance = new Vector2(6f, -6f);
            outline.useGraphicAlpha = true;

            // Configure text to fit and wrap nicely
            if (messageText != null)
            {
                var textFitter = messageText.GetComponent<ContentSizeFitter>();
                if (textFitter != null)
                {
                    textFitter.enabled = false;
                    if (Application.isPlaying) Destroy(textFitter); else DestroyImmediate(textFitter);
                }
                var textRect = messageText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.offsetMin = new Vector2(24f, 12f);
                textRect.offsetMax = new Vector2(-24f, -12f);

                messageText.alignment = TextAlignmentOptions.Center;
                messageText.fontSize = 20f;
                messageText.color = Color.white;
                messageText.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private void AutoBind()
        {
            if (messageText == null)
                messageText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }

        private static void MakePersistent(GameObject target)
        {
            if (Application.isPlaying && target != null)
                DontDestroyOnLoad(target);
        }
    }

    public enum MessageKind
    {
        Info,
        Success,
        Error
    }
}
