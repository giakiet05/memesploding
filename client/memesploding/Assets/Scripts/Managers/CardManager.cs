using Events;
using Events.GameEvents;
using Gameplay;
using Gameplay.Card;
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

        [SerializeField] private CardFactory factory;
        [SerializeField] private HandLayout handLayout;
        [SerializeField] private RectTransform dragLayer;

        public HandLayout HandLayout => handLayout;
        public RectTransform DragLayer => dragLayer;

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);

            ValidateReferences();

            InitStartingHand(30, "DEFUSE");
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        //For testing
        private void InitStartingHand(int amount, string cardName)
        {
            for (int i = 0; i < amount; i++)
            {
                PlayableCard card = factory.CreatePlayable(cardName, HandLayout.transform);
                handLayout.AddCard(card);
            }
        }

        public PlayableCard CreatePlayableCard(string cardName, Transform parent)
        {
            return factory.CreatePlayable(cardName, parent);
        }

        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            //TODO: Handle when a card is play
            Debug.Log("Card played event receive");
        }

        private void ValidateReferences()
        {
            if (!handLayout)
                Debug.LogError("HandLayout not assigned in CardManager", this);

            if (!dragLayer)
                Debug.LogError("DragLayer not assigned in CardManager", this);
        }
    }
}