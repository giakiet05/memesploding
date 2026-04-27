using Events;
using Models;
using Events.GameEvents;
using Gameplay;
using UnityEngine;
using EventType = Events.EventType;

namespace Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        public Player Player { get; set; }

        private GameSession _session;

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            EventBus.Subscribe<WsConnectedEventPayload>(EventType.WsConnected, OnWsConnected);
            EventBus.Subscribe<WsAckEventPayload>(EventType.WsAck, OnWsAck);
            EventBus.Subscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnWsGameplayEvent);
            EventBus.Subscribe<WsStateSnapshotEventPayload>(EventType.WsStateSnapshot, OnWsStateSnapshot);
            EventBus.Subscribe<WsErrorEventPayload>(EventType.WsError, OnWsError);
            EventBus.Subscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            EventBus.Unsubscribe<WsConnectedEventPayload>(EventType.WsConnected, OnWsConnected);
            EventBus.Unsubscribe<WsAckEventPayload>(EventType.WsAck, OnWsAck);
            EventBus.Unsubscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnWsGameplayEvent);
            EventBus.Unsubscribe<WsStateSnapshotEventPayload>(EventType.WsStateSnapshot, OnWsStateSnapshot);
            EventBus.Unsubscribe<WsErrorEventPayload>(EventType.WsError, OnWsError);
            EventBus.Unsubscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
        }

        private void OnWsConnected(WsConnectedEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            _session = new GameSession(payload.Data.matchId, payload.Data.roomCode);
        }

        private void OnWsAck(WsAckEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            Debug.Log($"[GameManager] WS ACK stateVersion={payload.Data.stateVersion}");
        }

        private void OnWsGameplayEvent(WsGameplayEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            if (_session == null)
            {
                Debug.LogWarning("Game session is null");
                return;
            }

            _session.HandleGameplayEvent(payload);
        }

        private void OnWsStateSnapshot(WsStateSnapshotEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            if (_session == null)
            {
                Debug.LogWarning("Game session is null");
                return;
            }

            _session.ApplySnapshot(payload.Data);
        }

        private void OnWsError(WsErrorEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            Debug.LogError($"[GameManager] WS error: {payload.Data.message}");
        }

        private void OnWsStatusChanged(WsStatusChangedEventPayload payload)
        {
            Debug.Log($"[GameManager] WS status={payload?.Status}");
        }

        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            if (payload.PlayerID == Player.ID)
            {
                NetworkManager.Instance.SendPlayCardCommand(payload);
            }
        }

        public void DrawCard()
        {
            NetworkManager.Instance.SendDrawCardCommand();
        }
    }
}
