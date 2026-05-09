using Gameplay.Card;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public class PlayerDrawCard : BaseCard
    {
        private Animator _animator;

        [SerializeField] private Image frontImage;

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponent<Animator>();
        }

        public override void Initialize(CardData cardData)
        {
            base.Initialize(cardData);

            if (cardData.artwork != null)
                frontImage.sprite = cardData.artwork;
        }

        public void PlayAnimation()
        {
            _animator.Play("DrawCard");
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