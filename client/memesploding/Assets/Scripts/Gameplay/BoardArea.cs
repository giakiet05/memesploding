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

        //Handle when player play a card
        public void OnDrop(PointerEventData eventData)
        {
            Card card = eventData.pointerDrag?.GetComponent<Card>();
            if (card == null) 
                return;

            Debug.Log("Card is play");
            CardPlayedEventPayload payload = new CardPlayedEventPayload(card.Id, card.name, "Vak0506");
            EventBus.Publish(EventType.CardPlayedEvent, payload);

            if (_newestCard != null)
                _newestCard.SetNewest(false);
            _newestCard = card;

            card.RectTransform.SetParent(transform, false);
            card.DisableDrag();
        }

        //TODO: Handle when opponent play a card
    }
}
