using Card;
using Gameplay;
using UnityEngine;

namespace Events
{
    public abstract class BaseEventPayload { }

    public class CardPlayedEventPayload : BaseEventPayload
    {
        public CardPlayedEventPayload(BaseCard card, string playerName)
        {
            PlayedCard = card;
            PlayerName = playerName;
        }

        public BaseCard PlayedCard { get; }
        public string PlayerName { get; }
    }

    public class CardDrawEventPayload : BaseEventPayload
    {
        public CardDrawEventPayload(string cardName)
        {
            CardName = cardName;
        }

        public string CardName { get; }
    }
}