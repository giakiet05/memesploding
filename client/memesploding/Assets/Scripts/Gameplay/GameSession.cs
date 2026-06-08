using Events;
using Managers;
using Network.Websocket;
using System;
using System.Linq;
using Events.GameEvents;
using Managers.UIManager;
using UnityEngine;
using EventType = Events.EventType;

namespace Gameplay
{
    public class GameSession
    {
        private readonly string _matchId;
        private readonly string _roomCode;
        public GameState GameState { get; private set; }
        public GameClock Clock { get; } = new();

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

            if (GameState != null && snapshot.stateVersion <= GameState.stateVersion)
                return;

            GameState ??= new GameState();
            GameState.UpdateState(snapshot);
            Clock.ApplySnapshot(snapshot);
            CardManager.Instance?.SyncHand(GameState.selfHand);

            var currentUserId = TryGetUserIdAtIndex(GameState.players, GameState.turnIndex);
            Debug.Log(
                $"[TurnTrace] Snapshot stateVersion={GameState.stateVersion} turnIndex={GameState.turnIndex} " +
                $"currentUserId={currentUserId ?? "null"} localUserId={GameManager.Instance?.Player?.ID ?? "null"} " +
                $"players={GameState.players?.Count ?? 0}");
        }

        public void HandleGameplayEvent(WsGameplayEventPayload payload)
        {
            if (payload?.Data == null)
                return;

            if (GameState == null)
            {
                Debug.LogWarning("Game state is null");
                return;
            }

            if (payload.Data.stateVersion <= GameState.stateVersion)
                return;

            WsGameplayPayloadBase parsedPayload = payload.ParsedPayload ?? payload.Data.ParsedPayload;
            GameState.stateVersion = payload.Data.stateVersion;

            switch (payload.EventType)
            {
                case WsGameplayEventType.MatchStarted:
                    GameState.phase = "Playing";
                    //TODO: Start local match flow (timer/UI/input unlock) from MatchStarted.
                    break;

                case WsGameplayEventType.TurnStarted:
                case WsGameplayEventType.TurnChanged:
                    if (parsedPayload is not WsTurnIndexPayload turnPayload)
                        return;
                    UpdateTurn(turnPayload.turnIndex, turnPayload.turnEndsAt, turnPayload.turnTimerSeconds, turnPayload.serverTimeUtc);
                    break;

                case WsGameplayEventType.TurnContinues:
                    if (parsedPayload is not WsTurnContinuesPayload turnContinues)
                        return;

                    SetPlayerPendingDraw(turnContinues.userId, turnContinues.pendingDrawCount);
                    UpdateTurnClock(turnContinues.turnEndsAt, turnContinues.turnTimerSeconds, turnContinues.serverTimeUtc);
                    //TODO: Display that a player turn is still continue
                    break;

                case WsGameplayEventType.TurnTimeoutAutoDraw:
                    if (parsedPayload is not WsUserPayload timeoutAutoDraw)
                        return;

                    Debug.Log($"[GameSession] Turn timed out. Server auto-drew for userId={timeoutAutoDraw.userId}");
                    break;

                case WsGameplayEventType.CardDrawn:
                    if (parsedPayload is not WsCardActionPayload cardDrawn)
                        return;
                    GameState.drawPileCount = Math.Max(0, GameState.drawPileCount - 1);
                    ChangePlayerHandCount(cardDrawn.userId, +1);

                    //Handle player draw event
                    if (IsSelf(cardDrawn.userId) && !string.IsNullOrWhiteSpace(cardDrawn.cardCode))
                    {
                        HandleDrawnCard(cardDrawn.cardCode);
                        GameState.selfHand.Add(cardDrawn.cardCode);
                    }
                    else if (!IsSelf(cardDrawn.userId))
                    {
                        GameplayUIManager.Instance.DisplayOpponentDraw(cardDrawn.userId);
                    }
                    break;

                case WsGameplayEventType.CardPlayed:
                    if (parsedPayload is not WsCardActionPayload cardPlayed)
                        return;

                    if (string.IsNullOrWhiteSpace(cardPlayed.cardCode))
                    {
                        Debug.LogError("Card code is null or empty");
                        break;
                    }

                    ChangePlayerHandCount(cardPlayed.userId, -1);
                    GameState.discardPile.Add(cardPlayed.cardCode);

                    if (IsSelf(cardPlayed.userId))
                    {
                        RemoveOneCardFromSelfHand(cardPlayed.cardCode);
                        CardManager.Instance?.RemoveCardFromHand(cardPlayed.cardCode);
                    }
                    else
                    {
                        //Make opponent play a card
                        GameplayUIManager.Instance.PlayOpponentCard(cardPlayed.userId, cardPlayed.cardCode);
                    }
                    break;

                case WsGameplayEventType.ComboPlayed:
                    if (parsedPayload is not WsComboPlayedPayload comboPlayed)
                        return;

                    if (comboPlayed.comboSize > 0)
                        ChangePlayerHandCount(comboPlayed.userId, -comboPlayed.comboSize);

                    if (!string.IsNullOrWhiteSpace(comboPlayed.comboCode))
                        GameState.discardPile.Add(comboPlayed.comboCode);
                    else if (!string.IsNullOrWhiteSpace(comboPlayed.cardCode))
                        GameState.discardPile.Add(comboPlayed.cardCode);
                    break;

                case WsGameplayEventType.ExplosionTriggered:
                    if (parsedPayload is not WsUserPayload explosion)
                        return;

                    GameState.pendingDefuseUserId = explosion.userId;
                    GameState.pendingBombOwnerUserId = explosion.userId;
                    GameState.pendingBombCardCode = "ExplodingKitten";
                    // TODO: Server should include window timestamps in event payload for precise countdown.
                    break;

                case WsGameplayEventType.DefuseUsed:
                    if (parsedPayload is not WsUserPayload defuseUsed)
                        return;

                    GameState.pendingDefuseUserId = null;
                    if (string.Equals(GameState.pendingBombOwnerUserId, defuseUsed.userId, StringComparison.OrdinalIgnoreCase))
                    {
                        GameState.pendingBombCardCode = "ExplodingKitten";
                    }
                    //TODO: Display that a defuse card has been used

                    break;

                case WsGameplayEventType.SkipApplied:
                    if (parsedPayload is not WsUserPayload skipApplied)
                        return;

                    SetPlayerPendingDraw(skipApplied.userId, Math.Max(0, GetPlayerPendingDraw(skipApplied.userId) - 1));
                    break;

                case WsGameplayEventType.BombReinserted:
                    if (parsedPayload is not WsBombReinsertedPayload bombReinserted)
                        return;

                    GameState.pendingBombOwnerUserId = null;
                    GameState.pendingBombCardCode = null;
                    GameState.pendingDefuseUserId = null;
                    GameState.bombReinsertWindowEndsAt = null;
                    GameState.defuseWindowEndsAt = null;
                    break;

                case WsGameplayEventType.BombReinsertAuto:
                    GameState.pendingBombOwnerUserId = null;
                    GameState.pendingBombCardCode = null;
                    GameState.bombReinsertWindowEndsAt = null;
                    break;

                case WsGameplayEventType.ShuffleApplied:
                    // No deterministic local mutation besides visual feedback.
                    // TODO: Add deck shuffle animation hook.
                    break;

                case WsGameplayEventType.PlayerEliminated:
                    if (parsedPayload is not WsPlayerEliminatedPayload eliminated)
                        return;

                    SetPlayerLifeState(eliminated.userId, "Eliminated");

                    //TODO: set a player is eliminated in the UI
                    break;

                case WsGameplayEventType.MatchFinished:
                    GameState.phase = "Finished";
                    // TODO: Persist and present winner/ranking panel.
                    break;

                case WsGameplayEventType.AttackApplied:
                    if (parsedPayload is not WsAttackAppliedPayload attack)
                        return;

                    SetPlayerPendingDraw(attack.toUserId, Math.Max(0, GetPlayerPendingDraw(attack.toUserId) + attack.added));

                    //TODO: Display attack effect
                    break;

                case WsGameplayEventType.FuturePeeked:
                    if (parsedPayload is not WsFuturePeekedPayload peek)
                        return;

                    // TODO: Store peek.cards in dedicated UI state instead of GameState when UI model is introduced.
                    if (IsSelf(peek.userId) && peek.cards != null)
                        GameplayUIManager.Instance.DisplayCards(peek.cards.ToList());
                    break;

                case WsGameplayEventType.FavorWindowOpened:
                    if (parsedPayload is not WsFavorWindowOpenedPayload favorWindow)
                        return;

                    GameState.pendingFavorRequesterId = favorWindow.requesterId;
                    GameState.pendingFavorTargetId = favorWindow.targetId;
                    // TODO: Server should provide favorWindowEndsAt in payload for accurate local timer.
                    GameplayUIManager.Instance.ShowFavorWindow(favorWindow.requesterId, favorWindow.targetId);
                    break;

                case WsGameplayEventType.FavorResolved:
                    if (parsedPayload is not WsTransferPayload favorResolved)
                        return;

                    ApplyCardTransfer(favorResolved.fromUserId, favorResolved.toUserId, favorResolved.cardCode);
                    GameState.pendingFavorRequesterId = null;
                    GameState.pendingFavorTargetId = null;
                    GameState.favorWindowEndsAt = null;
                    GameplayUIManager.Instance.HideFavorWindow();
                    break;

                case WsGameplayEventType.CatComboTwoResolved:
                    if (parsedPayload is not WsTransferPayload comboTwoResolved)
                        return;

                    ApplyCardTransfer(comboTwoResolved.fromUserId, comboTwoResolved.toUserId, comboTwoResolved.cardCode);
                    break;

                case WsGameplayEventType.FavorTargetEmpty:
                    GameState.pendingFavorRequesterId = null;
                    GameState.pendingFavorTargetId = null;
                    GameState.favorWindowEndsAt = null;
                    GameplayUIManager.Instance.HideFavorWindow();
                    break;

                case WsGameplayEventType.ReactionWindowOpened:
                    if (parsedPayload is not WsCardActionPayload reactionOpen)
                        return;

                    GameState.pendingReactionUserId = reactionOpen.userId;
                    GameState.pendingReactionAction = reactionOpen.cardCode;
                    GameState.pendingNopeCount = 0;
                    GameState.lastNopeUserId = null;
                    GameState.lastNopedAction = null;
                    // TODO: Server should include reactionWindowEndsAt in payload for accurate local timer.
                    GameplayUIManager.Instance.ShowReactionWindow(reactionOpen.userId, reactionOpen.cardCode, GameState.pendingNopeCount);
                    break;

                case WsGameplayEventType.NopePlayed:
                    if (parsedPayload is not WsNopeActionPayload nopePlayed)
                        return;

                    GameState.pendingNopeCount = Math.Max(0, nopePlayed.nopeCount);
                    GameState.lastNopeUserId = nopePlayed.userId;
                    ChangePlayerHandCount(nopePlayed.userId, -1);

                    if (IsSelf(nopePlayed.userId))
                    {
                        RemoveOneCardFromSelfHand("Nope");
                        CardManager.Instance?.RemoveCardFromHand("Nope");
                    }
                    else
                    {
                        GameplayUIManager.Instance.PlayOpponentCard(nopePlayed.userId, "Nope");
                    }

                    GameplayUIManager.Instance.ShowReactionWindow(
                        GameState.pendingReactionUserId,
                        GameState.pendingReactionAction,
                        GameState.pendingNopeCount);
                    break;

                case WsGameplayEventType.ReactionWindowClosed:
                    if (parsedPayload is not WsReactionWindowClosedPayload reactionClosed)
                        return;

                    GameState.pendingNopeCount = Math.Max(0, reactionClosed.nopeCount);
                    GameState.pendingReactionUserId = null;
                    GameState.pendingReactionAction = null;
                    GameState.reactionWindowEndsAt = null;
                    GameplayUIManager.Instance.HideReactionWindow();
                    break;

                case WsGameplayEventType.ActionNoped:
                    if (parsedPayload is not WsNopeActionPayload actionNoped)
                        return;

                    GameState.pendingNopeCount = Math.Max(0, actionNoped.nopeCount);
                    GameState.lastNopedAction = actionNoped.cardCode;
                    GameState.pendingReactionUserId = null;
                    GameState.pendingReactionAction = null;
                    GameState.reactionWindowEndsAt = null;
                    GameplayUIManager.Instance.ShowActionNoped(actionNoped.cardCode, actionNoped.nopeCount);
                    break;

                case WsGameplayEventType.ActionRejected:
                    if (parsedPayload is not WsActionRejectedPayload rejected)
                        return;

                    GameplayUIManager.Instance.ClearTargetSelection();
                    Debug.LogWarning(
                        $"[GameSession] Action rejected reason={rejected.reason ?? "unknown"} userId={rejected.userId ?? "null"} " +
                        $"cardCode={rejected.cardCode ?? "null"} required={rejected.required}");
                    break;

                case WsGameplayEventType.CatComboThreeResolved:
                    if (parsedPayload is not WsCatComboThreePayload comboThreeResolved)
                        return;

                    if (!string.IsNullOrWhiteSpace(comboThreeResolved.requestedCardCode))
                    {
                        ApplyCardTransfer(comboThreeResolved.fromUserId, comboThreeResolved.toUserId, comboThreeResolved.requestedCardCode);
                    }
                    break;

                case WsGameplayEventType.CatComboThreeMiss:
                    // No card transfer to apply.
                    break;

                case WsGameplayEventType.CatComboFiveResolved:
                    if (parsedPayload is not WsCatComboFiveResolvedPayload comboFive)
                        return;

                    ChangePlayerHandCount(comboFive.userId, +1);
                    if (IsSelf(comboFive.userId) && !string.IsNullOrWhiteSpace(comboFive.discardCardCode))
                        GameState.selfHand.Add(comboFive.discardCardCode);

                    RemoveOneCardFromDiscard(comboFive.discardCardCode);
                    break;

                default:
                    Debug.LogWarning(
                        $"[GameSession] Unsupported gameplay event parsedType='{payload.EventType}' rawType='{payload.RawType}' stateVersion={payload.StateVersion} rawPayload={payload.RawPayload}");
                    break;
            }
        }

        public void UpdateTurn(int newTurnIndex, DateTime? turnEndsAt, int turnTimerSeconds, DateTime serverTimeUtc)
        {
            GameState.turnIndex = newTurnIndex;
            GameState.turnCounter = Math.Max(0, GameState.turnCounter + 1);
            GameState.turnEndsAt = turnEndsAt;
            if (turnTimerSeconds > 0)
                GameState.turnTimerSeconds = turnTimerSeconds;

            UpdateTurnClock(turnEndsAt, turnTimerSeconds, serverTimeUtc);

            //Set current active player
            var curUserID = TryGetUserIdAtIndex(GameState.players, newTurnIndex);
            Debug.Log(
                $"[TurnTrace] Event turnIndex={newTurnIndex} currentUserId={curUserID ?? "null"} " +
                $"localUserId={GameManager.Instance?.Player?.ID ?? "null"} players={GameState.players?.Count ?? 0}");

            if (string.IsNullOrWhiteSpace(curUserID))
                return;

            EventBus.Publish(EventType.TurnStart, new TurnStartEventPayload(curUserID));
        }

        private void UpdateTurnClock(DateTime? turnEndsAt, int turnTimerSeconds, DateTime serverTimeUtc)
        {
            if (turnEndsAt.HasValue)
                GameState.turnEndsAt = turnEndsAt;

            if (turnTimerSeconds > 0)
                GameState.turnTimerSeconds = turnTimerSeconds;

            if (serverTimeUtc != default)
                GameState.serverTimeUtc = serverTimeUtc;

            Clock.UpdateTurn(GameState.turnEndsAt, GameState.turnTimerSeconds, GameState.serverTimeUtc);
        }

        private void HandleDrawnCard(string cardCode)
        {
            //Displaying the card just drawn
            GameplayUIManager.Instance.DisplayDrawnCard(cardCode);

            //Handle add card to hand
            CardManager.Instance.AddCardToHand(cardCode);
        }

        private static string TryGetUserIdAtIndex(System.Collections.Generic.List<WsPlayerPublicStateDto> players, int index)
        {
            if (players == null || index < 0 || index >= players.Count)
                return null;

            return players[index]?.userId;
        }

        private bool IsSelf(string userId)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null &&
                   !string.IsNullOrWhiteSpace(userId) &&
                   string.Equals(player.ID, userId, StringComparison.OrdinalIgnoreCase);
        }

        private void ChangePlayerHandCount(string userId, int delta)
        {
            if (GameState?.players == null || string.IsNullOrWhiteSpace(userId) || delta == 0)
                return;

            var index = GameState.players.FindIndex(x =>
                string.Equals(x.userId, userId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;

            var player = GameState.players[index];
            var next = Math.Max(0, player.handCount + delta);
            GameState.players[index] = new WsPlayerPublicStateDto
            {
                userId = player.userId,
                nickname = player.nickname,
                connected = player.connected,
                lifeState = player.lifeState,
                handCount = next,
                pendingDrawCount = player.pendingDrawCount,
                pendingReconnectUntil = player.pendingReconnectUntil
            };
        }

        private void SetPlayerPendingDraw(string userId, int pendingDrawCount)
        {
            if (GameState?.players == null || string.IsNullOrWhiteSpace(userId))
                return;

            var index = GameState.players.FindIndex(x =>
                string.Equals(x.userId, userId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;

            var player = GameState.players[index];
            GameState.players[index] = new WsPlayerPublicStateDto
            {
                userId = player.userId,
                nickname = player.nickname,
                connected = player.connected,
                lifeState = player.lifeState,
                handCount = player.handCount,
                pendingDrawCount = Math.Max(0, pendingDrawCount),
                pendingReconnectUntil = player.pendingReconnectUntil
            };
        }

        private int GetPlayerPendingDraw(string userId)
        {
            if (GameState?.players == null || string.IsNullOrWhiteSpace(userId))
                return 0;

            var player = GameState.players.FirstOrDefault(x =>
                string.Equals(x.userId, userId, StringComparison.OrdinalIgnoreCase));
            return player?.pendingDrawCount ?? 0;
        }

        private void SetPlayerLifeState(string userId, string lifeState)
        {
            if (GameState?.players == null || string.IsNullOrWhiteSpace(userId))
                return;

            var index = GameState.players.FindIndex(x =>
                string.Equals(x.userId, userId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;

            var player = GameState.players[index];
            GameState.players[index] = new WsPlayerPublicStateDto
            {
                userId = player.userId,
                nickname = player.nickname,
                connected = player.connected,
                lifeState = lifeState,
                handCount = player.handCount,
                pendingDrawCount = player.pendingDrawCount,
                pendingReconnectUntil = player.pendingReconnectUntil
            };
        }

        private void ApplyCardTransfer(string fromUserId, string toUserId, string cardCode)
        {
            ChangePlayerHandCount(fromUserId, -1);
            ChangePlayerHandCount(toUserId, +1);

            if (!string.IsNullOrWhiteSpace(cardCode))
            {
                if (IsSelf(fromUserId))
                    RemoveOneCardFromSelfHand(cardCode);
                if (IsSelf(toUserId))
                    GameState.selfHand.Add(cardCode);
            }
        }

        private void RemoveOneCardFromSelfHand(string cardCode)
        {
            if (GameState?.selfHand == null || string.IsNullOrWhiteSpace(cardCode))
                return;

            var index = GameState.selfHand.FindIndex(c =>
                string.Equals(c, cardCode, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                GameState.selfHand.RemoveAt(index);
        }

        private void RemoveOneCardFromDiscard(string cardCode)
        {
            if (GameState?.discardPile == null || string.IsNullOrWhiteSpace(cardCode))
                return;

            for (int i = GameState.discardPile.Count - 1; i >= 0; i--)
            {
                if (string.Equals(GameState.discardPile[i], cardCode, StringComparison.OrdinalIgnoreCase))
                {
                    GameState.discardPile.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
