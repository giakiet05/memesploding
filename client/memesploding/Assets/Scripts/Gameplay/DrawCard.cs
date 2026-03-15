using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public class DrawCard : MonoBehaviour
    {
        public CardData Data { get; private set; }

        private Animator _animator;
        private RectTransform _rect;

        [SerializeField] private Image frontImage;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _rect = GetComponent<RectTransform>();
        }

        public void Initialize(CardData cardData)
        {
            Data = cardData;

            if (cardData.artwork != null)
                frontImage.sprite = cardData.artwork;
        }

        public void PlayAnimation()
        {
            _animator.Play("DrawCard");
        }

        public void SetPosition(Vector2 pos)
        {
            _rect.anchoredPosition = pos;
        }

        public void SetScale(Vector3 scale)
        {
            _rect.localScale = scale;
        }
    }
}