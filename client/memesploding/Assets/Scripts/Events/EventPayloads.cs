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

        public Card PlayedCard;
        public string PlayerName { get; }
    }
}