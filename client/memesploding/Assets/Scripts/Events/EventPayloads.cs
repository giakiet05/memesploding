using UnityEngine;

namespace Events
{
    public abstract class BaseEventPayload { }

    public class CardPlayedEventPayload : BaseEventPayload
    {
        public CardPlayedEventPayload(string cardId, string cardName)
        {
            CardId = cardId;
            CardName = cardName;
        }

        public string CardId { get; }
        public string CardName { get; }
    }
}