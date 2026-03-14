using Events;
using Events.GameEvents;
using UnityEngine;
using UnityEngine.EventSystems;
using EventType = Events.EventType;

namespace Gameplay
{
    public class BoardArea : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            Card card = eventData.pointerDrag?.GetComponent<Card>();
            if (card == null) 
                return;

            Debug.Log("Card is play");
            CardPlayedEventPayload payload = new CardPlayedEventPayload(card.Id, card.name);
            EventBus.Publish(EventType.CardPlayedEvent, payload);
            Destroy(card.gameObject);
        }
    }
}
