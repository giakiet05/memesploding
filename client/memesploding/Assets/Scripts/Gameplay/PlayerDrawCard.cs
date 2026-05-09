using System;
using Gameplay.Card;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public class PlayerDrawCard : BaseCard
    {
        private Animator _animator;

        private Vector2 _originalPosition;
        private Quaternion _originalRotation;

        public Action<PlayerDrawCard> OnAnimationFinished;

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponent<Animator>();
        }

        public override void Initialize(CardData cardData)
        {
            base.Initialize(cardData);

            if (cardData.artwork != null)
                cardImage.sprite = cardData.artwork;

            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
        }

        public void PlayAnimation()
        {
            _animator.Play("DrawCard");
        }

        public void OnCardClicked()
        {
            var isFlip = _animator.GetBool("isFlip");
            var isCollect = _animator.GetBool("isCollect");

            if (!isFlip) {
                _animator.SetBool("isFlip", true);
                return;
            }
            else if (!isCollect) {
                _animator.SetBool("isCollect", true);
            }
        }

        public void AnimationFinished()
        {
            OnAnimationFinished?.Invoke(this);
        }

        public void Reset()
        {
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;

            _animator.Rebind();
            _animator.Update(0f);
        }

        public void SetPosition(Vector2 pos)
        {
            RectTransform.anchoredPosition = pos;
        }

        public void SetScale(Vector3 scale)
        {
            RectTransform.localScale = scale;
        }
    }
}