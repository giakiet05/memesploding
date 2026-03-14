using Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace Managers
{
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        [SerializeField] public HandLayout handLayout;
        [SerializeField] public Card cardPrefab;
        [SerializeField] public RectTransform dragLayer;

        public void DealCard(CardData data)
        {
            Card card = Instantiate(cardPrefab);
            card.Initialize(data);
            handLayout.AddCard(card);
        }

        public void RemoveCard(Card card)
        {
            handLayout.RemoveCard(card);
            Destroy(card.gameObject);
        }
    }
}