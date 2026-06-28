using Events.Channels;
using UnityEngine;

namespace Events.Listeners
{
    public class TurnStartEventChannelListener : MonoBehaviour
    {
        [SerializeField] private TurnStartEventChannelSO channel;
        [SerializeField] private TurnStartUnityEvent onRaised;

        private void OnEnable() => channel?.Register(HandleEvent);
        private void OnDisable() => channel?.Unregister(HandleEvent);
        private void HandleEvent(TurnStartEventPayload payload) => onRaised?.Invoke(payload);
    }
}
