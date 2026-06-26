using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField, Min(0.1f)] private float defaultDuration = 0.2f;
        [SerializeField] private Color successColor = new Color(0.18f, 0.58f, 0.32f, 0.96f);
        [SerializeField] private Color errorColor = new Color(0.75f, 0.20f, 0.20f, 0.96f);
        [SerializeField] private Color infoColor = new Color(0.18f, 0.35f, 0.67f, 0.96f);

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
            canvasObject.AddComponent<CanvasScaler>();
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

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            MakePersistent(transform.root.gameObject);
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (WasDismissInputPressed())
                DismissImmediate();
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
            DismissImmediate();
        }

        private void DismissImmediate()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            Hide();
        }

        private static bool WasDismissInputPressed()
        {
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
                return true;

            if (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true)
                return true;

            return false;
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
