using UnityEngine;

namespace Events.Channels
{
    [CreateAssetMenu(menuName = "Events/Channels/Card Played", fileName = "CardPlayedChannel")]
    public class CardPlayedEventChannelSO : BaseEventChannelSO<CardPlayedEventPayload> { }
}
