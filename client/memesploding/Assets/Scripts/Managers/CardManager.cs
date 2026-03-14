using System;
using Events;
using Events.GameEvents;
using Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
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

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
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

        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            //TODO: Handle when a card is play
            Debug.Log("Card played event receive");
        }
    }
}