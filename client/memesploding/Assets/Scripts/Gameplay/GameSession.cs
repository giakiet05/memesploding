using Events;
using Network.Websocket;
using UnityEngine;

namespace Gameplay
{
    public class GameSession
    {
        private readonly string _matchId;
        private readonly string _roomCode;
        private GameState _gameState;

        public GameSession(string matchId, string roomCode)
        {
            _matchId = matchId;
            _roomCode = roomCode;
        }

        public void ApplySnapshot(WsStateSnapshotDto snapshot)
        {
            if (snapshot == null) return;

            if (snapshot.matchId != _matchId || snapshot.roomCode != _roomCode)
                return;

            if (_gameState != null && snapshot.stateVersion <= _gameState.stateVersion)
                return;

            _gameState ??= new GameState();
            _gameState.UpdateState(snapshot);
        }

        public void HandleGameplayEvent(WsGameplayEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            WsGameplayPayloadBase parsedPayload = payload.ParsedPayload ?? payload.Data.ParsedPayload;

            switch (payload.EventType)
            {
                case WsGameplayEventType.MatchStarted:
                    var matchStarted = parsedPayload as WsEmptyGameplayPayload ?? new WsEmptyGameplayPayload();
                    // TODO: Start local match flow (timer/UI/input unlock) from MatchStarted.
                    break;

                case WsGameplayEventType.TurnStarted:
                case WsGameplayEventType.TurnChanged:
                    var turnPayload = parsedPayload as WsTurnIndexPayload ?? new WsTurnIndexPayload();
                    // TODO: Update active player indicator and timers with turnPayload.turnIndex.
                    UpdateTurn(turnPayload.turnIndex);
                    break;

                case WsGameplayEventType.TurnContinues:
                    var turnContinues = parsedPayload as WsTurnContinuesPayload ?? new WsTurnContinuesPayload();
                    // TODO: Reflect pending draw chain state using turnContinues.pendingDrawCount.
                    break;

                case WsGameplayEventType.CardDrawn:
                case WsGameplayEventType.CardPlayed:
                    var cardAction = parsedPayload as WsCardActionPayload ?? new WsCardActionPayload();
                    // TODO: Animate draw/play and sync hand/discard from cardAction.userId + cardAction.cardCode.
                    break;

                case WsGameplayEventType.ComboPlayed:
                    var comboPlayed = parsedPayload as WsComboPlayedPayload ?? new WsComboPlayedPayload();
                    // TODO: Show combo indicator using comboPlayed.comboSize/cardCode/comboCode.
                    break;

                case WsGameplayEventType.ExplosionTriggered:
                case WsGameplayEventType.DefuseUsed:
                case WsGameplayEventType.SkipApplied:
                    var userPayload = parsedPayload as WsUserPayload ?? new WsUserPayload();
                    // TODO: Play VFX/SFX for user-targeted effect with userPayload.userId.
                    break;

                case WsGameplayEventType.BombReinserted:
                    var bombReinserted = parsedPayload as WsBombReinsertedPayload ?? new WsBombReinsertedPayload();
                    // TODO: Update deck preview / bomb marker by bombReinserted.position.
                    break;

                case WsGameplayEventType.BombReinsertAuto:
                case WsGameplayEventType.ShuffleApplied:
                    var emptyPayload = parsedPayload as WsEmptyGameplayPayload ?? new WsEmptyGameplayPayload();
                    // TODO: Sync deck ordering state after server-side automatic update.
                    break;

                case WsGameplayEventType.PlayerEliminated:
                    var eliminated = parsedPayload as WsPlayerEliminatedPayload ?? new WsPlayerEliminatedPayload();
                    // TODO: Mark player KO and update ranking with eliminated.reason.
                    break;

                case WsGameplayEventType.MatchFinished:
                    var finished = parsedPayload as WsMatchFinishedPayload ?? new WsMatchFinishedPayload();
                    // TODO: Transition to result screen with finished.winnerUserId.
                    break;

                case WsGameplayEventType.AttackApplied:
                    var attack = parsedPayload as WsAttackAppliedPayload ?? new WsAttackAppliedPayload();
                    // TODO: Show attack transfer from attack.fromUserId to attack.toUserId (added draws: attack.added).
                    break;

                case WsGameplayEventType.FuturePeeked:
                    var peek = parsedPayload as WsFuturePeekedPayload ?? new WsFuturePeekedPayload();
                    // TODO: Render peeked top cards UI using peek.cards.
                    break;

                case WsGameplayEventType.FavorWindowOpened:
                    var favorWindow = parsedPayload as WsFavorWindowOpenedPayload ?? new WsFavorWindowOpenedPayload();
                    // TODO: Open favor selection UI for requester/target IDs.
                    break;

                case WsGameplayEventType.FavorResolved:
                case WsGameplayEventType.CatComboTwoResolved:
                    var transfer = parsedPayload as WsTransferPayload ?? new WsTransferPayload();
                    // TODO: Apply card transfer animation with transfer.fromUserId/toUserId/cardCode.
                    break;

                case WsGameplayEventType.FavorTargetEmpty:
                    var favorEmpty = parsedPayload as WsFavorTargetEmptyPayload ?? new WsFavorTargetEmptyPayload();
                    // TODO: Show no-card feedback for favor target.
                    break;

                case WsGameplayEventType.ReactionWindowOpened:
                    var reactionOpen = parsedPayload as WsCardActionPayload ?? new WsCardActionPayload();
                    // TODO: Open reaction/nope prompt for reactionOpen.cardCode.
                    break;

                case WsGameplayEventType.ReactionWindowClosed:
                    var reactionClosed = parsedPayload as WsReactionWindowClosedPayload ?? new WsReactionWindowClosedPayload();
                    // TODO: Close reaction UI and display final nope count (reactionClosed.nopeCount).
                    break;

                case WsGameplayEventType.CatComboThreeResolved:
                case WsGameplayEventType.CatComboThreeMiss:
                    var comboThree = parsedPayload as WsCatComboThreePayload ?? new WsCatComboThreePayload();
                    // TODO: Show combo-3 result using comboThree.requestedCardCode.
                    break;

                case WsGameplayEventType.CatComboFiveResolved:
                    var comboFive = parsedPayload as WsCatComboFiveResolvedPayload ?? new WsCatComboFiveResolvedPayload();
                    // TODO: Apply discard retrieval for combo-5 with comboFive.discardCardCode.
                    break;

                default:
                    Debug.Log($"[GameSession] Unsupported gameplay event '{payload.EventType}'");
                    break;
            }
        }

        public void UpdateTurn(int newTurnIndex)
        {
            if (_gameState == null)
            {
                Debug.LogWarning("Game state is null");
                return;
            }

            _gameState.turnIndex = newTurnIndex;
        }
    }
}
