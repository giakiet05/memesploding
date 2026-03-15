using Events;
using Events.GameEvents;
using Gameplay;
using ScriptableObjects;
using UnityEngine;
using EventType = Events.EventType;

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

        [SerializeField] private Canvas canvas;
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private Card cardPrefab;
        [SerializeField] private HandLayout handLayout;
        [SerializeField] private RectTransform dragLayer;

        public HandLayout HandLayout => handLayout;
        public RectTransform DragLayer => dragLayer;

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        public void SetCardDatabase(CardDatabase database)
        {
            cardDatabase = database;
        }

        public Card CreateCard(string cardName, Transform parent = null)
        {
            CardData data = cardDatabase.Get(cardName);

            if (data == null)
            {
                Debug.LogError($"Card not found: {cardName}");
                return null;
            }

            if (parent == null)
                parent = canvas.transform;

            Card card = Instantiate(cardPrefab, parent, false);
            card.Initialize(data);

            return card;
        }

        public void DealCard(string cardName)
        {
            Card card = CreateCard(cardName);
            if (card == null) return;

            handLayout.AddCard(card);
        }

        public void RemoveCard(Card card)
        {
            handLayout.RemoveCard(card);
            Destroy(card.gameObject);
        }

        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            //TODO: Handle when a card is play
            Debug.Log("Card played event receive");
        }
    }
}