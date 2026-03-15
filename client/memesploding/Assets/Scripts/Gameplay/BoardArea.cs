using System;
using Events;
using Events.GameEvents;
using Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using EventType = Events.EventType;

namespace Gameplay
{
    public class BoardArea : MonoBehaviour, IDropHandler
    {
        private Card _newestCard;

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        //Handle when player play a card
        public void OnDrop(PointerEventData eventData)
        {
            Card card = eventData.pointerDrag?.GetComponent<Card>();
            if (card == null) 
                return;

            Debug.Log("Card is play");
            CardPlayedEventPayload payload = new CardPlayedEventPayload(card, "Vak0506");
            EventBus.Publish(EventType.CardPlayedEvent, payload);

            if (_newestCard != null)
                _newestCard.SetNewest(false);
            _newestCard = card;

            card.RectTransform.SetParent(transform, false);
            card.DisableDrag();
        }

        //Handle when opponent play a card
        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            if (_newestCard != null)
                _newestCard.SetNewest(false);
            _newestCard = payload.PlayedCard;
        }
    }
}
