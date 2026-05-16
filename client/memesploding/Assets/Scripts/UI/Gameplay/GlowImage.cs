using UnityEngine;
using UnityEngine.UI;

namespace UI.Gameplay
{
    [RequireComponent(typeof(Image))]
    public class GlowImage : MonoBehaviour
    {
        [Header("Pulse Settings")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float minAlpha = 0.15f;
        [SerializeField] private float maxAlpha = 0.65f;

        [Header("Scale Settings")]
        [SerializeField] private float minScale = 1f;
        [SerializeField] private float maxScale = 1.12f;

        [Header("State")]
        [SerializeField] private bool startEnabled = true;

        private Image _glowImage;
        private RectTransform _rectTransform;
        private Color _baseColor;
        private Vector3 _baseScale;
        private bool _isGlowing;

        private void Awake()
        {
            _glowImage = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();

            _baseColor = _glowImage.color;
            _baseScale = _rectTransform.localScale;

            SetGlowActive(startEnabled);
        }

        private void Update()
        {
            if (!_isGlowing)
                return;

            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            float scale = Mathf.Lerp(minScale, maxScale, t);

            Color color = _baseColor;
            color.a = alpha;
            _glowImage.color = color;

            _rectTransform.localScale = _baseScale * scale;
        }

        public void SetGlowActive(bool active)
        {
            _isGlowing = active;

            if (_glowImage == null)
                return;

            _glowImage.enabled = active;

            if (!active)
            {
                Color color = _baseColor;
                color.a = 0f;
                _glowImage.color = color;

                if (_rectTransform != null)
                    _rectTransform.localScale = _baseScale;
            }
        }

        public void TurnOnGlow()
        {
            SetGlowActive(true);
        }

        public void TurnOffGlow()
        {
            SetGlowActive(false);
        }

        public void ToggleGlow()
        {
            SetGlowActive(!_isGlowing);
        }
    }
}