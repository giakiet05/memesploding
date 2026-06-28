using UnityEngine;

namespace Events.Channels
{
    [CreateAssetMenu(menuName = "Events/Channels/Ws Status Changed", fileName = "WsStatusChangedChannel")]
    public class WsStatusChangedEventChannelSO : BaseEventChannelSO<WsStatusChangedEventPayload> { }
}
