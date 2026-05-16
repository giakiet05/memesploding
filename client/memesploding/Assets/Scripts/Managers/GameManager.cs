using Events;
using Events.GameEvents;
using Gameplay;
using Models;
using System.Collections.Generic;
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

            Player = new Player
            {
                ID = "P0",
                Username = "HelloKitty"
            };
        }

        public Player Player { get; set; }

        private GameSession _session;

        private void Start()
        {
            EventBus.Subscribe<WsConnectedEventPayload>(EventType.WsConnected, OnWsConnected);
            EventBus.Subscribe<WsAckEventPayload>(EventType.WsAck, OnWsAck);
            EventBus.Subscribe<WsGameplayEventPayload>(EventType.WsGameplayEvent, OnWsGameplayEvent);
            EventBus.Subscribe<WsStateSnapshotEventPayload>(EventType.WsStateSnapshot, OnWsStateSnapshot);
            EventBus.Subscribe<WsErrorEventPayload>(EventType.WsError, OnWsError);
            EventBus.Subscribe<WsStatusChangedEventPayload>(EventType.WsStatusChanged, OnWsStatusChanged);
        }

        private void OnDestroy()
        {
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
            GameplayUIManager.Instance.InitOpponentUI(_session.GameState.players);
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


        public void PlayCard(
            string targetUserId = null,
            int comboSize = 0,
            List<string> cardCodes = null,
            string requestedCardCode = null,
            string discardCardCode = null)
        {
            NetworkManager.Instance.SendPlayCardCommand(
                cardCode: Player.ID,
                targetUserId: targetUserId,
                comboSize: comboSize,
                cardCodes: cardCodes,
                requestedCardCode: requestedCardCode,
                discardCardCode: discardCardCode
            );
        }

        public void DrawCard()
        {
            if (_session?.GameState == null || !_session.GameState.IsPlayerTurn)
                return;

            //UIManager.Instance.DrawCard();
            NetworkManager.Instance.SendDrawCardCommand();
            //TODO: Using loading screen
        }
        public int GetDrawPileCount() => _session?.GameState?.drawPileCount ?? 0;

        public void ChooseBombInsertPosition(int position)
        {
            NetworkManager.Instance.SendChooseBombInsertPositionCommand(position);
        }
       
    }
}
