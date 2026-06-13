using System;
using System.Collections;
using Managers;
using ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Gameplay
{
    public sealed class ReactionWindowModel
    {
        public string ActorName;
        public string TargetText;
        public string CardCode;
        public CardData CardData;
        public DateTime? EndsAtUtc;
        public bool WillActivate;
        public bool CanNope;
        public bool IsNewWindow;
    }

    [ExecuteAlways]
    public class ReactionWindowView : MonoBehaviour
    {
        private const string VisualRootName = "ComicToastVisual";

        [Header("Legacy references used for migration")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI actionText;
        [SerializeField] private TextMeshProUGUI nopeCountText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Button nopeButton;
        [SerializeField] private Image timerFill;

        [Header("Runtime references")]
        [SerializeField] private Button playCardButton;

        private TextMeshProUGUI _descriptionText;
        private TextMeshProUGUI _statusText;
        private Image _statusBadge;
        private RectTransform _visualRoot;
        private RectTransform _timerFillRect;
        private GameObject _timerTrackObject;
        private float _timerFillWidth;
        private TMP_FontAsset _bodyFont;
        private TMP_FontAsset _titleFont;
        private Material _bodyFontMaterial;
        private Material _titleFontMaterial;
        private DateTime? _endsAtUtc;
        private DateTime? _hideAtUtc;
        private float _initialSeconds;
        private bool _localNopePlayed;
        private bool _reactionActive;
        private Coroutine _animationRoutine;

        private static readonly Color Ink = new(0.09f, 0.07f, 0.06f, 1f);
        private static readonly Color Paper = new(1f, 0.94f, 0.72f, 1f);
        private static readonly Color Red = new(0.86f, 0.12f, 0.1f, 1f);
        private static readonly Color Green = new(0.1f, 0.58f, 0.25f, 1f);
        private static readonly Color Yellow = new(1f, 0.72f, 0.08f, 1f);

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            BuildVisuals();
            SetNopeVisible(false);
            SetPlayCardVisible(true);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            BuildVisuals();
            BindButtons();

            if (Application.isPlaying)
                return;

#if UNITY_EDITOR
            ShowEditorPreview();
#endif
        }

        private void OnDisable()
        {
            UnbindButtons();
            SetPlayCardVisible(true);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (_hideAtUtc.HasValue && DateTime.UtcNow >= _hideAtUtc.Value)
            {
                Hide();
                return;
            }

            if (!_endsAtUtc.HasValue)
                return;

            var remaining = Mathf.Max(0f, (float)(_endsAtUtc.Value - DateTime.UtcNow).TotalSeconds);
            SetTimer(remaining);
            if (remaining <= 0f && nopeButton != null)
                nopeButton.interactable = false;
        }

        public void ConfigureAsToast(RectTransform parent)
        {
            if (parent != null && transform.parent != parent)
                transform.SetParent(parent, false);

            BuildVisuals();
        }

        public void Show(ReactionWindowModel model)
        {
            if (Application.isPlaying && !gameObject.activeSelf)
                gameObject.SetActive(true);

            BuildVisuals();
            SetNotificationLayout(false);
            if (model == null)
                return;

            if (model.IsNewWindow)
                _localNopePlayed = false;
            _reactionActive = true;

            titleText.text = model.CardCode ?? "ACTION";
            actionText.text = $"{model.ActorName ?? "Người chơi"} đã đánh {model.CardCode ?? "lá bài"}";
            nopeCountText.text = model.TargetText ?? string.Empty;
            _descriptionText.text = model.CardData != null ? model.CardData.description : string.Empty;

            SetStatus(model.WillActivate ? "SẼ KÍCH HOẠT" : "SẼ BỊ CHẶN", model.WillActivate);
            SetPlayCardVisible(false);
            SetNopeVisible(true);
            nopeButton.interactable = model.CanNope && !_localNopePlayed && model.EndsAtUtc.HasValue;

            _endsAtUtc = null;
            _hideAtUtc = null;
            _initialSeconds = 0f;
            if (model.EndsAtUtc.HasValue)
            {
                _endsAtUtc = model.EndsAtUtc.Value;
                _initialSeconds = Mathf.Max(0.01f, (float)(_endsAtUtc.Value - DateTime.UtcNow).TotalSeconds);
                SetTimer(_initialSeconds);
            }

            gameObject.SetActive(true);
            PlayToastAnimation();
        }

        public void ShowNotification(string title, string message, string detail = null, string status = null, bool danger = false)
        {
            if (Application.isPlaying && !gameObject.activeSelf)
                gameObject.SetActive(true);

            BuildVisuals();
            SetNotificationLayout(true);
            titleText.text = title ?? "THÔNG BÁO";
            actionText.text = message ?? string.Empty;
            nopeCountText.text = detail ?? string.Empty;
            _descriptionText.text = string.Empty;
            SetStatus(status ?? "HÀNH ĐỘNG", !danger);

            if (!_reactionActive)
            {
                SetNopeVisible(false);
                SetPlayCardVisible(true);
                _endsAtUtc = null;
                _hideAtUtc = DateTime.UtcNow.AddSeconds(2f);
                SetTimer(0f);
            }

            gameObject.SetActive(true);
            PlayToastAnimation();
        }

        public void HoldForResolution()
        {
            _endsAtUtc = null;
            if (nopeButton != null)
                nopeButton.interactable = false;
            SetTimer(0f);
        }

        public void ShowResult(bool activated)
        {
            SetNotificationLayout(false);
            SetStatus(activated ? "ĐÃ KÍCH HOẠT" : "ĐÃ BỊ CHẶN", activated);
            _endsAtUtc = null;
            _reactionActive = false;
            _hideAtUtc = DateTime.UtcNow.AddSeconds(1.5f);
            if (nopeButton != null)
                nopeButton.interactable = false;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _endsAtUtc = null;
            _hideAtUtc = null;
            _reactionActive = false;
            SetNopeVisible(false);
            SetPlayCardVisible(true);
            if (Application.isPlaying)
                gameObject.SetActive(false);
        }

        private void PlayToastAnimation()
        {
            if (!Application.isPlaying)
                return;

            if (_animationRoutine != null)
                StopCoroutine(_animationRoutine);
            _animationRoutine = StartCoroutine(AnimateToast());
        }

        private IEnumerator AnimateToast()
        {
            var rect = (RectTransform)transform;
            var target = new Vector2(-24f, -24f);
            var start = target + new Vector2(80f, 0f);
            rect.anchoredPosition = start;
            rect.localScale = new Vector3(0.92f, 0.92f, 1f);
            for (var elapsed = 0f; elapsed < 0.25f; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / 0.25f);
                rect.anchoredPosition = Vector2.Lerp(start, target, t);
                rect.localScale = Vector3.Lerp(new Vector3(0.92f, 0.92f, 1f), Vector3.one, t);
                yield return null;
            }
            rect.anchoredPosition = target;
            rect.localScale = Vector3.one;
            _animationRoutine = null;
        }

        private void BuildVisuals()
        {
            MigrateLegacyReferences();
            EnsurePlayCardButton();
            ConfigureRoot();
            HideLegacyLayout();
            BuildComicToast();
            RemoveAccentStripe();
            ConfigureNopeButton();
            RemoveLegacyActions();
        }

        private void RemoveAccentStripe()
        {
            var stripe = _visualRoot != null ? _visualRoot.Find("AccentStripe") : null;
            if (stripe == null)
                return;

            stripe.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(stripe.gameObject);
            else
                DestroyImmediate(stripe.gameObject);
        }

        private void MigrateLegacyReferences()
        {
            titleText ??= transform.Find("Header/Title")?.GetComponent<TextMeshProUGUI>();
            actionText ??= transform.Find("Body/ActionText")?.GetComponent<TextMeshProUGUI>();
            nopeCountText ??= transform.Find("Body/NopeCountText")?.GetComponent<TextMeshProUGUI>();
            timerText ??= transform.Find("Body/Timer/TimerText")?.GetComponent<TextMeshProUGUI>();
            timerFill ??= transform.Find("Body/Timer/TimerFill")?.GetComponent<Image>();
            nopeButton ??= transform.Find("Actions/NopeButton")?.GetComponent<Button>();
        }

        private void ConfigureRoot()
        {
            gameObject.name = "ReactionToast";
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(500f, 180f);

            var background = GetComponent<Image>();
            if (background != null)
            {
                background.color = Paper;
                background.raycastTarget = false;
            }

            var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(6f, -6f);
            outline.useGraphicAlpha = true;
        }

        private void HideLegacyLayout()
        {
            SetLegacyActive("Header", false);
            SetLegacyActive("Body", false);
            SetLegacyActive("Actions", false);
        }

        private void BuildComicToast()
        {
            if (_visualRoot != null)
            {
                BindComicToastReferences();
                return;
            }

            _titleFont ??= titleText != null ? titleText.font : null;
            _bodyFont ??= actionText != null ? actionText.font : _titleFont;
            _titleFontMaterial ??= titleText != null ? titleText.fontSharedMaterial : null;
            _bodyFontMaterial ??= actionText != null ? actionText.fontSharedMaterial : _titleFontMaterial;
            var existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            _visualRoot = CreateRect(VisualRootName, transform, Vector2.zero, Vector2.zero, stretch: true);
            _visualRoot.gameObject.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSaveInEditor;

            titleText = CreateText("CardName", _visualRoot, new Vector2(-90f, -14f), new Vector2(270f, 34f), 26f, Ink, FontStyles.Bold, TextAlignmentOptions.Center, font: _titleFont, fontMaterial: _titleFontMaterial);
            _statusBadge = CreateImage("StatusBadge", _visualRoot, new Vector2(150f, -16f), new Vector2(160f, 34f), Green);
            _statusBadge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            _statusText = CreateText("StatusText", _statusBadge.rectTransform, Vector2.zero, Vector2.zero, 14f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, stretch: true, font: _bodyFont, fontMaterial: _bodyFontMaterial);

            actionText = CreateText("ActorText", _visualRoot, new Vector2(0f, -52f), new Vector2(455f, 34f), 17f, Ink, FontStyles.Bold, TextAlignmentOptions.Center, font: _bodyFont, fontMaterial: _bodyFontMaterial);
            nopeCountText = CreateText("TargetText", _visualRoot, new Vector2(0f, -88f), new Vector2(455f, 28f), 16f, new Color(0.36f, 0.2f, 0.12f), FontStyles.Bold, TextAlignmentOptions.Center, font: _bodyFont, fontMaterial: _bodyFontMaterial);
            _descriptionText = CreateText("DescriptionText", _visualRoot, new Vector2(0f, -119f), new Vector2(455f, 48f), 14f, Ink, FontStyles.Normal, TextAlignmentOptions.Top, font: _bodyFont, fontMaterial: _bodyFontMaterial);
            _descriptionText.textWrappingMode = TextWrappingModes.Normal;
            _descriptionText.overflowMode = TextOverflowModes.Overflow;

            var timerTrack = CreateImage("TimerTrack", _visualRoot, new Vector2(-20f, -184f), new Vector2(405f, 12f), Ink);
            _timerTrackObject = timerTrack.gameObject;
            timerFill = CreateImage("TimerFill", timerTrack.rectTransform, new Vector2(3f, -2f), new Vector2(399f, 6f), Yellow);
            ConfigureTimerFillRect();
            timerText = CreateText("TimerText", _visualRoot, new Vector2(220f, -177f), new Vector2(45f, 24f), 16f, Ink, FontStyles.Bold, TextAlignmentOptions.Center, font: _bodyFont, fontMaterial: _bodyFontMaterial);
            timerText.overflowMode = TextOverflowModes.Truncate;
        }

        private void BindComicToastReferences()
        {
            titleText = _visualRoot.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            actionText = _visualRoot.Find("ActorText")?.GetComponent<TextMeshProUGUI>();
            nopeCountText = _visualRoot.Find("TargetText")?.GetComponent<TextMeshProUGUI>();
            _descriptionText = _visualRoot.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
            _statusBadge = _visualRoot.Find("StatusBadge")?.GetComponent<Image>();
            _statusText = _statusBadge != null
                ? _statusBadge.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>()
                : null;
            timerFill = _visualRoot.Find("TimerTrack/TimerFill")?.GetComponent<Image>();
            _timerTrackObject = _visualRoot.Find("TimerTrack")?.gameObject;
            timerText = _visualRoot.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
            ConfigureTimerFillRect();
        }

        private void ConfigureNopeButton()
        {
            if (nopeButton == null || playCardButton == null)
                return;

            var playRect = (RectTransform)playCardButton.transform;
            var nopeRect = (RectTransform)nopeButton.transform;
            nopeRect.SetParent(playRect.parent, false);
            nopeRect.anchorMin = playRect.anchorMin;
            nopeRect.anchorMax = playRect.anchorMax;
            nopeRect.pivot = playRect.pivot;
            nopeRect.anchoredPosition = playRect.anchoredPosition;
            nopeRect.sizeDelta = playRect.sizeDelta;
            nopeButton.gameObject.name = "NopeButton";

            var image = nopeButton.targetGraphic as Image ?? nopeButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = Red;
                image.raycastTarget = true;
            }

            var outline = nopeButton.GetComponent<Outline>() ?? nopeButton.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(5f, -5f);

            var label = nopeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "NOPE!";
                label.color = Color.white;
                label.fontSize = 28f;
                label.fontStyle = FontStyles.Bold;
                label.raycastTarget = false;
            }
        }

        private void RemoveLegacyActions()
        {
            var actions = transform.Find("Actions");
            if (actions == null)
                return;

            var pass = actions.Find("PassButton");
            if (pass != null)
            {
                if (Application.isPlaying)
                    Destroy(pass.gameObject);
                else
                    DestroyImmediate(pass.gameObject);
            }

            if (actions.childCount == 0)
            {
                if (Application.isPlaying)
                    Destroy(actions.gameObject);
                else
                    DestroyImmediate(actions.gameObject);
            }
        }

        private void EnsurePlayCardButton()
        {
            if (playCardButton != null)
                return;

            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject.name == "PlayCardButton")
                {
                    playCardButton = button;
                    break;
                }
            }
        }

        private void BindButtons()
        {
            if (nopeButton == null)
                return;
            nopeButton.onClick.RemoveListener(HandleNopeClicked);
            nopeButton.onClick.AddListener(HandleNopeClicked);
        }

        private void UnbindButtons()
        {
            if (nopeButton != null)
                nopeButton.onClick.RemoveListener(HandleNopeClicked);
        }

        private void HandleNopeClicked()
        {
            if (GameManager.Instance == null)
                return;

            _localNopePlayed = true;
            nopeButton.interactable = false;
            GameManager.Instance.Nope();
        }

        private void SetTimer(float remaining)
        {
            var progress = _initialSeconds > 0f ? Mathf.Clamp01(remaining / _initialSeconds) : 0f;
            if (timerText != null)
                timerText.text = $"{Mathf.CeilToInt(remaining)}s";
            if (_timerFillRect != null)
                _timerFillRect.sizeDelta = new Vector2(_timerFillWidth * progress, _timerFillRect.sizeDelta.y);
        }

        private void ConfigureTimerFillRect()
        {
            if (timerFill == null)
                return;

            _timerFillRect = timerFill.rectTransform;
            _timerFillRect.anchorMin = new Vector2(0f, 0.5f);
            _timerFillRect.anchorMax = new Vector2(0f, 0.5f);
            _timerFillRect.pivot = new Vector2(0f, 0.5f);
            _timerFillRect.anchoredPosition = new Vector2(3f, 0f);
            _timerFillWidth = 399f;
            _timerFillRect.sizeDelta = new Vector2(_timerFillWidth, 6f);
        }

        private void SetStatus(string value, bool positive)
        {
            if (_statusText != null)
                _statusText.text = value;
            if (_statusBadge != null)
                _statusBadge.color = positive ? Green : Red;
        }

        private void SetNotificationLayout(bool notification)
        {
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(500f, 215f);

            if (_timerTrackObject != null)
                _timerTrackObject.SetActive(!notification);
            if (timerText != null)
                timerText.gameObject.SetActive(!notification);
            if (_descriptionText != null)
                _descriptionText.gameObject.SetActive(!notification);

            ConfigureToastText(actionText, notification ? new Vector2(455f, 44f) : new Vector2(455f, 34f), true);
            ConfigureToastText(nopeCountText, notification ? new Vector2(455f, 42f) : new Vector2(455f, 28f), true);
            if (actionText != null)
                actionText.rectTransform.anchoredPosition = new Vector2(0f, -52f);
            if (nopeCountText != null)
                nopeCountText.rectTransform.anchoredPosition = notification
                    ? new Vector2(0f, -102f)
                    : new Vector2(0f, -88f);
        }

        private static void ConfigureToastText(TextMeshProUGUI text, Vector2 size, bool wrap)
        {
            if (text == null)
                return;

            text.rectTransform.sizeDelta = size;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        }

        private void SetNopeVisible(bool visible)
        {
            if (nopeButton != null && nopeButton.gameObject.activeSelf != visible)
                nopeButton.gameObject.SetActive(visible);
        }

        private void SetPlayCardVisible(bool visible)
        {
            if (playCardButton != null && playCardButton.gameObject.activeSelf != visible)
                playCardButton.gameObject.SetActive(visible);
        }

        private void SetLegacyActive(string childName, bool active)
        {
            var child = transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        private static RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size, bool stretch = false)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            if ((parent.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                go.hideFlags = HideFlags.DontSaveInEditor;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            return rect;
        }

        private static Image CreateImage(string objectName, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            float fontSize,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment,
            bool stretch = false,
            TMP_FontAsset font = null,
            Material fontMaterial = null)
        {
            var rect = CreateRect(objectName, parent, position, size, stretch);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            if (font != null)
                text.font = font;
            if (fontMaterial != null)
                text.fontSharedMaterial = fontMaterial;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

#if UNITY_EDITOR
        private void ShowEditorPreview()
        {
            BuildVisuals();
            SetNotificationLayout(false);
            titleText.text = "ATTACK";
            actionText.text = "Alice đã đánh Attack";
            nopeCountText.text = "Ảnh hưởng: Bob";
            _descriptionText.text = "Kết thúc lượt hiện tại. Người chơi tiếp theo phải chơi hai lượt.";
            SetStatus("SẼ KÍCH HOẠT", true);
            _initialSeconds = 5f;
            SetTimer(4f);

            SetPlayCardVisible(false);
            SetNopeVisible(true);
            nopeButton.interactable = true;
            gameObject.SetActive(true);
        }
#endif
    }
}
