using System;
using System.Collections;
using Gameplay.Card;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public class PlayerDrawCard : BaseCard
    {
        [SerializeField, Min(0.05f)] private float moveDuration = 0.5f;
        [SerializeField, Min(0f)] private float faceUpHoldDuration = 0.3f;
        [SerializeField] private Image frontImage;

        private Animator _animator;
        private GameObject _front;
        private GameObject _back;
        private Vector2 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;

        public Action<PlayerDrawCard> OnAnimationFinished;

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponent<Animator>();
            _front = transform.Find("Front")?.gameObject;
            _back = transform.Find("Back")?.gameObject;
            frontImage ??= _front != null ? _front.GetComponent<Image>() : null;
        }

        public override void Initialize(CardData cardData)
        {
            base.Initialize(cardData);

            if (cardData.artworks != null && frontImage != null)
                frontImage.sprite = cardData.Artwork;

            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            _originalScale = transform.localScale;
        }

        public void PlayAnimation(Vector2 startPosition, Vector2 handPosition)
        {
            StopAllCoroutines();
            SetFaceUp();
            RectTransform.anchoredPosition = startPosition;
            RectTransform.localScale = new Vector3(0.72f, 0.72f, 1f);
            if (_animator != null)
                _animator.enabled = false;
            StartCoroutine(PlayFastFaceUpAnimation(handPosition));
        }

        public void OnCardClicked()
        {
            // Draw animation is automatic; clicks no longer control flip or collect.
        }

        public void AnimationFinished()
        {
            OnAnimationFinished?.Invoke(this);
        }

        public void Reset()
        {
            StopAllCoroutines();
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
            transform.localScale = _originalScale;
            SetFaceUp();

            if (_animator != null)
            {
                _animator.enabled = false;
                _animator.Rebind();
            }
        }

        public void SetPosition(Vector2 pos)
        {
            RectTransform.anchoredPosition = pos;
        }

        public void SetScale(Vector3 scale)
        {
            RectTransform.localScale = scale;
        }

        private IEnumerator PlayFastFaceUpAnimation(Vector2 handPosition)
        {
            moveDuration = Mathf.Max(moveDuration, 0.5f);
            faceUpHoldDuration = Mathf.Max(faceUpHoldDuration, 0.3f);
            var startPosition = RectTransform.anchoredPosition;
            var startRotation = RectTransform.localRotation;
            var elapsed = 0f;

            while (elapsed < moveDuration)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                RectTransform.anchoredPosition = Vector2.Lerp(startPosition, Vector2.zero, t);
                RectTransform.localRotation = Quaternion.Lerp(startRotation, Quaternion.identity, t);
                RectTransform.localScale = Vector3.Lerp(new Vector3(0.72f, 0.72f, 1f), Vector3.one, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            RectTransform.anchoredPosition = Vector2.zero;
            RectTransform.localRotation = Quaternion.identity;
            RectTransform.localScale = Vector3.one;

            if (faceUpHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(faceUpHoldDuration);

            elapsed = 0f;
            var centerPosition = RectTransform.anchoredPosition;
            while (elapsed < moveDuration)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                RectTransform.anchoredPosition = Vector2.Lerp(centerPosition, handPosition, t);
                RectTransform.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.55f, 0.55f, 1f), t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            OnAnimationFinished?.Invoke(this);
        }

        private void SetFaceUp()
        {
            if (_front != null)
            {
                _front.SetActive(true);
                _front.transform.localRotation = Quaternion.identity;
            }

            if (_back != null)
                _back.SetActive(false);
        }
    }
}
