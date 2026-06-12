using System;
using System.Collections.Generic;
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
        private Slider _slider;
        private TextMeshProUGUI _sliderValue;
        private Button _confirm;
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

        public void ShowConfirm(string title, string message, string confirmLabel, DateTime? endsAtUtc, Action onConfirm)
        {
            Prepare(title, message, endsAtUtc);
            _onConfirm = onConfirm;
            SetButtonLabel(_confirm, confirmLabel);
            _confirm.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }

        public void ShowOptions(
            string title,
            string message,
            IEnumerable<string> options,
            DateTime? endsAtUtc,
            Action<string> onSelected,
            Action onCancel = null)
        {
            Prepare(title, message, endsAtUtc);
            _onCancel = onCancel;
            foreach (var option in options)
            {
                var value = option;
                var button = CreateButton(_options, value);
                button.onClick.AddListener(() =>
                {
                    onSelected?.Invoke(value);
                    Hide();
                });
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
            _onConfirm = null;
            _onCancel = null;
        }

        private void Build()
        {
            var blocker = gameObject.AddComponent<Image>();
            blocker.color = Color.clear;

            var panel = CreateRect("Panel", transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 760f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.96f, 0.9f, 0.72f, 1f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 24);
            layout.spacing = 14f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _title = CreateText("Title", panel, 34, FontStyles.Bold);
            _message = CreateText("Message", panel, 22, FontStyles.Normal);
            _countdown = CreateText("Countdown", panel, 28, FontStyles.Bold);

            _options = CreateRect("Options", panel);
            var optionLayout = _options.gameObject.AddComponent<GridLayoutGroup>();
            optionLayout.cellSize = new Vector2(260f, 48f);
            optionLayout.spacing = new Vector2(10f, 8f);
            optionLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            optionLayout.constraintCount = 2;
            _options.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sliderRect = CreateRect("PositionSlider", panel);
            sliderRect.sizeDelta = new Vector2(0f, 44f);
            var sliderBackground = CreateRect("Background", sliderRect);
            sliderBackground.anchorMin = new Vector2(0f, 0.35f);
            sliderBackground.anchorMax = new Vector2(1f, 0.65f);
            sliderBackground.offsetMin = Vector2.zero;
            sliderBackground.offsetMax = Vector2.zero;
            sliderBackground.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var sliderFill = CreateRect("Fill", sliderBackground);
            sliderFill.anchorMin = Vector2.zero;
            sliderFill.anchorMax = Vector2.one;
            sliderFill.offsetMin = Vector2.zero;
            sliderFill.offsetMax = Vector2.zero;
            sliderFill.gameObject.AddComponent<Image>().color = new Color(0.83f, 0.17f, 0.13f, 1f);
            var sliderHandle = CreateRect("Handle", sliderRect);
            sliderHandle.sizeDelta = new Vector2(28f, 44f);
            sliderHandle.gameObject.AddComponent<Image>().color = Color.white;
            _slider = sliderRect.gameObject.AddComponent<Slider>();
            _slider.fillRect = sliderFill;
            _slider.handleRect = sliderHandle;
            _slider.targetGraphic = sliderHandle.GetComponent<Image>();
            _sliderValue = CreateText("SliderValue", panel, 22, FontStyles.Bold);

            _confirm = CreateButton(panel, "XÁC NHẬN");
            _confirm.onClick.AddListener(() =>
            {
                var callback = _onConfirm;
                Hide();
                callback?.Invoke();
            });

            var cancel = CreateButton(panel, "HỦY");
            cancel.onClick.AddListener(() =>
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
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var rect = CreateRect(label, parent);
            rect.sizeDelta = new Vector2(0f, 48f);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 48f;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.83f, 0.17f, 0.13f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            var text = CreateText("Label", rect, 22, FontStyles.Bold);
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
    }
}
