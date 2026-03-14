using UnityEngine;

namespace Events
{
    public abstract class BaseEventPayload { }

    public class CardPlayedEventPayload : BaseEventPayload
    {
        public CardPlayedEventPayload(string cardId, string cardName, string playerName)
        {
            CardId = cardId;
            CardName = cardName;
            PlayerName = playerName;
        }

        public string CardId { get; }
        public string CardName { get; }
        public string PlayerName { get; }
    }
}