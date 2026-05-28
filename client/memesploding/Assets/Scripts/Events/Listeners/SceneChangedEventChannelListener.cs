using Events.Channels;
using UnityEngine;

namespace Events.Listeners
{
    public class SceneChangedEventChannelListener : MonoBehaviour
    {
        [SerializeField] private SceneChangedEventChannelSO channel;
        [SerializeField] private SceneChangedUnityEvent onRaised;

        private void OnEnable() => channel?.Register(HandleEvent);
        private void OnDisable() => channel?.Unregister(HandleEvent);
        private void HandleEvent(SceneChangedEventPayload payload) => onRaised?.Invoke(payload);
    }
}
