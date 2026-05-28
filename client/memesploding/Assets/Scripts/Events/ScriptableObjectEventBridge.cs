using Events.Channels;
using Events.GameEvents;
using UnityEngine;
using EventType = Events.EventType;

namespace Events
{
    /// <summary>
    /// Bridges the current EventBus events into ScriptableObject channels.
    /// This keeps the existing runtime behavior while enabling incremental migration.
    /// </summary>
    public class ScriptableObjectEventBridge : MonoBehaviour
    {
        [Header("Optional Event Channels")]
        [SerializeField] private CardPlayedEventChannelSO cardPlayedChannel;
        [SerializeField] private TurnStartEventChannelSO turnStartChannel;
        [SerializeField] private SceneChangedEventChannelSO sceneChangedChannel;
        [SerializeField] private WsStatusChangedEventChannelSO wsStatusChangedChannel;

        private void OnEnable()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Subscribe<SceneChangedEventPayload>(EventType.SceneChanged, OnSceneChanged);
            EventBus.Subscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Unsubscribe<SceneChangedEventPayload>(EventType.SceneChanged, OnSceneChanged);
            EventBus.Unsubscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
        }

        private void OnCardPlayed(CardPlayedEventPayload payload) => cardPlayedChannel?.Raise(payload);
        private void OnTurnStart(TurnStartEventPayload payload) => turnStartChannel?.Raise(payload);
        private void OnSceneChanged(SceneChangedEventPayload payload) => sceneChangedChannel?.Raise(payload);
        private void OnWsStatusChanged(WsStatusChangedEventPayload payload) => wsStatusChangedChannel?.Raise(payload);
    }
}
