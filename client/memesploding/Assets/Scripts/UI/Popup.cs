using System.Collections;
using UnityEngine;

namespace UI
{
    public class Popup : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField, Min(0f)] private float animationDuration = 0.18f;
        [SerializeField] private Vector3 hiddenScale = new Vector3(0.92f, 0.92f, 1f);
        [SerializeField] private Vector3 visibleScale = Vector3.one;
        [SerializeField] private bool animateFade = true;

        private CanvasGroup _canvasGroup;
        private Coroutine _animationRoutine;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null && animateFade)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public virtual void Show()
        {
            PlayAnimation(true);
        }

        public virtual void Hide()
        {
            PlayAnimation(false);
        }

        public virtual void Toggle()
        {
            PlayAnimation(!gameObject.activeSelf);
        }

        private void PlayAnimation(bool show)
        {
            if (_animationRoutine != null)
                StopCoroutine(_animationRoutine);

            gameObject.SetActive(true);
            _animationRoutine = StartCoroutine(Animate(show));
        }

        private IEnumerator Animate(bool show)
        {
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = show ? visibleScale : hiddenScale;

            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : (show ? 0f : 1f);
            float targetAlpha = show ? 1f : 0f;

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (animationDuration <= 0f)
            {
                ApplyAnimationState(targetScale, targetAlpha, show);
                yield break;
            }

            float time = 0f;
            while (time < animationDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / animationDuration);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                if (_canvasGroup != null)
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                time += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyAnimationState(targetScale, targetAlpha, show);
        }

        private void ApplyAnimationState(Vector3 scale, float alpha, bool visible)
        {
            transform.localScale = scale;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }

            gameObject.SetActive(visible);
            _animationRoutine = null;
        }
    }
}
