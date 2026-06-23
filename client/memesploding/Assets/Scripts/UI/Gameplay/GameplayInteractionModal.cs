using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Card;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Gameplay
{
    public class GameplayInteractionModal : MonoBehaviour
    {
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _countdown;
        private RectTransform _options;
        private RectTransform _panel;

        private GridLayoutGroup _optionLayout;
        private Slider _slider;
        private TextMeshProUGUI _sliderValue;
        private Button _confirm;
        private Button _cancelButton;
        private Action _onConfirm;
        private Action _onCancel;
        private DateTime? _endsAtUtc;

        public static GameplayInteractionModal Create(Transform parent)
        {
            var root = CreateRect("GameplayInteractionModal", parent);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var modal = root.gameObject.AddComponent<GameplayInteractionModal>();
            modal.Build();
            modal.Hide();
            return modal;
        }

        private void Update()
        {
            if (!_endsAtUtc.HasValue || _countdown == null)
                return;

            var seconds = Mathf.Max(0, Mathf.CeilToInt((float)(_endsAtUtc.Value - DateTime.UtcNow).TotalSeconds));
            _countdown.text = $"{seconds}s";
        }

        public void ShowConfirm(string title, string message, string confirmLabel, DateTime? endsAtUtc, Action onConfirm, bool showCancel = false, Action onCancel = null)
        {
            Prepare(title, message, endsAtUtc);
            SetPanelSize(new Vector2(520f, 360f));
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            SetButtonLabel(_confirm, confirmLabel);
            _confirm.gameObject.SetActive(true);
            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(showCancel);
            gameObject.SetActive(true);
        }

        public void ShowCardOptions(
            string title,
            string message,
            IEnumerable<string> cardCodes,
            DateTime? endsAtUtc,
            Action<string> onSelected,
            Action onCancel = null)
        {
            Prepare(title, message, endsAtUtc);
            SetPanelSize(new Vector2(620f, 690f));
            ConfigureCardOptions();
            _onCancel = onCancel;
            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(true);

            foreach (var cardCode in cardCodes
                         .Where(code => !string.IsNullOrWhiteSpace(code))
                         .Distinct())
            {
                var value = cardCode;
                DisplayCard card = CardManager.Instance?.CreateDisplayCard(value, _options);
                if (card == null)
                    continue;

                var layout = card.gameObject.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = _optionLayout.cellSize.x;
                layout.preferredHeight = _optionLayout.cellSize.y;
                var clickHandler = card.gameObject.GetComponent<CardOptionClickHandler>() ??
                                   card.gameObject.AddComponent<CardOptionClickHandler>();
                clickHandler.OnClicked = () =>
                {
                    onSelected?.Invoke(value);
                    Hide();
                };
            }

            gameObject.SetActive(true);
        }

        public void ShowSlider(
            string title,
            string message,
            int maxValue,
            DateTime? endsAtUtc,
            Action<int> onConfirm)
        {
            Prepare(title, message, endsAtUtc);
            SetPanelSize(new Vector2(560f, 470f));
            _slider.minValue = 0;
            _slider.maxValue = Mathf.Max(0, maxValue);
            _slider.wholeNumbers = true;
            _slider.value = Mathf.Clamp(Mathf.RoundToInt(maxValue * 0.5f), 0, maxValue);
            _sliderValue.text = PositionLabel(Mathf.RoundToInt(_slider.value), maxValue);
            _slider.onValueChanged.RemoveAllListeners();
            _slider.onValueChanged.AddListener(value =>
                _sliderValue.text = PositionLabel(Mathf.RoundToInt(value), maxValue));
            _slider.gameObject.SetActive(true);
            _sliderValue.gameObject.SetActive(true);
            _onConfirm = () => onConfirm?.Invoke(Mathf.RoundToInt(_slider.value));
            SetButtonLabel(_confirm, "ĐẶT BOMB");
            _confirm.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _onConfirm = null;
            _onCancel = null;
            _endsAtUtc = null;
            gameObject.SetActive(false);
        }

        private void Prepare(string title, string message, DateTime? endsAtUtc)
        {
            ClearOptions();
            _title.text = title;
            _message.text = message;
            _endsAtUtc = NormalizeUtc(endsAtUtc);
            _countdown.gameObject.SetActive(_endsAtUtc.HasValue);
            _slider.gameObject.SetActive(false);
            _sliderValue.gameObject.SetActive(false);
            _confirm.gameObject.SetActive(false);
            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(false);
            _onConfirm = null;
            _onCancel = null;
        }

        private void Build()
        {
            var blocker = gameObject.AddComponent<Image>();
            blocker.color = Color.clear;

            var panel = CreateRect("Panel", transform);
            _panel = panel;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 690f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(1f, 0.94f, 0.72f, 0.99f);
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.09f, 0.07f, 0.06f, 0.82f);
            shadow.effectDistance = new Vector2(10f, -10f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.09f, 0.07f, 0.06f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 26, 26);
            layout.spacing = 14f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _title = CreateText("Title", panel, 38, FontStyles.Bold);
            _title.color = new Color(0.86f, 0.12f, 0.1f, 1f);
            _message = CreateText("Message", panel, 22, FontStyles.Bold);
            _countdown = CreateText("Countdown", panel, 30, FontStyles.Bold);
            _countdown.color = new Color(0.86f, 0.12f, 0.1f, 1f);

            _options = CreateRect("Options", panel);
            _optionLayout = _options.gameObject.AddComponent<GridLayoutGroup>();
            ConfigureTextOptions();
            _options.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sliderRect = CreateRect("PositionSlider", panel);
            sliderRect.sizeDelta = new Vector2(0f, 52f);
            var sliderBackground = CreateRect("Background", sliderRect);
            sliderBackground.anchorMin = new Vector2(0f, 0.36f);
            sliderBackground.anchorMax = new Vector2(1f, 0.64f);
            sliderBackground.offsetMin = Vector2.zero;
            sliderBackground.offsetMax = Vector2.zero;
            sliderBackground.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 1f);
            var sliderFill = CreateRect("Fill", sliderBackground);
            sliderFill.anchorMin = Vector2.zero;
            sliderFill.anchorMax = Vector2.one;
            sliderFill.offsetMin = new Vector2(3f, 3f);
            sliderFill.offsetMax = new Vector2(-3f, -3f);
            sliderFill.gameObject.AddComponent<Image>().color = new Color(1f, 0.72f, 0.08f, 1f);
            var sliderHandle = CreateRect("Handle", sliderRect);
            sliderHandle.sizeDelta = new Vector2(32f, 52f);
            var handleImage = sliderHandle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.86f, 0.12f, 0.1f, 1f);
            var handleOutline = sliderHandle.gameObject.AddComponent<Outline>();
            handleOutline.effectColor = new Color(0.09f, 0.07f, 0.06f, 1f);
            handleOutline.effectDistance = new Vector2(3f, -3f);
            _slider = sliderRect.gameObject.AddComponent<Slider>();
            _slider.fillRect = sliderFill;
            _slider.handleRect = sliderHandle;
            _slider.targetGraphic = handleImage;
            _sliderValue = CreateText("SliderValue", panel, 22, FontStyles.Bold);

            _confirm = CreateButton(panel, "XÁC NHẬN");
            _confirm.onClick.AddListener(() =>
            {
                var callback = _onConfirm;
                Hide();
                callback?.Invoke();
            });

            _cancelButton = CreateButton(panel, "HỦY");
            _cancelButton.onClick.AddListener(() =>
            {
                var callback = _onCancel;
                Hide();
                callback?.Invoke();
            });
        }

        private void ClearOptions()
        {
            for (var i = _options.childCount - 1; i >= 0; i--)
                Destroy(_options.GetChild(i).gameObject);
        }

        private void ConfigureTextOptions()
        {
            _optionLayout.cellSize = new Vector2(260f, 48f);
            _optionLayout.spacing = new Vector2(10f, 8f);
            _optionLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _optionLayout.constraintCount = 2;
        }

        private void ConfigureCardOptions()
        {
            _optionLayout.cellSize = new Vector2(150f, 210f);
            _optionLayout.spacing = new Vector2(14f, 14f);
            _optionLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _optionLayout.constraintCount = 3;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, int size, FontStyles style)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(0.09f, 0.07f, 0.06f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var rect = CreateRect(label, parent);
            rect.sizeDelta = new Vector2(0f, 54f);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 54f;
            layout.preferredHeight = 54f;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = label == "HỦY"
                ? new Color(0.09f, 0.07f, 0.06f, 1f)
                : new Color(0.86f, 0.12f, 0.1f, 1f);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.09f, 0.07f, 0.06f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            var button = rect.gameObject.AddComponent<Button>();
            var text = CreateText("Label", rect, 24, FontStyles.Bold);
            text.color = Color.white;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            return button;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = label;
        }

        private static string PositionLabel(int position, int drawPileCount)
        {
            if (position == 0)
                return "Vị trí: trên cùng";
            if (position == drawPileCount)
                return "Vị trí: dưới cùng";
            return $"Vị trí: {position}";
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue)
                return null;
            return value.Value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        }


        private void SetPanelSize(Vector2 size)
        {
            if (_panel != null)
                _panel.sizeDelta = size;
        }
    }
}
