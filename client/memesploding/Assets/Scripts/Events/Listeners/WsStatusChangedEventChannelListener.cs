using Events.Channels;
using UnityEngine;

namespace Events.Listeners
{
    public class WsStatusChangedEventChannelListener : MonoBehaviour
    {
        [SerializeField] private WsStatusChangedEventChannelSO channel;
        [SerializeField] private WsStatusChangedUnityEvent onRaised;

        private void OnEnable() => channel?.Register(HandleEvent);
        private void OnDisable() => channel?.Unregister(HandleEvent);
        private void HandleEvent(WsStatusChangedEventPayload payload) => onRaised?.Invoke(payload);
    }
}
