using Events.Channels;
using UnityEngine;

namespace Events.Listeners
{
    public class CardPlayedEventChannelListener : MonoBehaviour
    {
        [SerializeField] private CardPlayedEventChannelSO channel;
        [SerializeField] private CardPlayedUnityEvent onRaised;

        private void OnEnable() => channel?.Register(HandleEvent);
        private void OnDisable() => channel?.Unregister(HandleEvent);
        private void HandleEvent(CardPlayedEventPayload payload) => onRaised?.Invoke(payload);
    }
}
