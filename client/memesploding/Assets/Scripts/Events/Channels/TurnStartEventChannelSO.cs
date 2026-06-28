using UnityEngine;

namespace Events.Channels
{
    [CreateAssetMenu(menuName = "Events/Channels/Turn Start", fileName = "TurnStartChannel")]
    public class TurnStartEventChannelSO : BaseEventChannelSO<TurnStartEventPayload> { }
}
