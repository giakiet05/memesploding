using Gameplay;
using UnityEngine;

namespace Events
{
    public abstract class BaseEventPayload { }

    public class CardPlayedEventPayload : BaseEventPayload
    {
        public CardPlayedEventPayload(Card card, string playerName)
        {
            PlayedCard = card;
            PlayerName = playerName;
        }

        public Card PlayedCard { get; }
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