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
            GameManager.Instance?.NotifyLocalDrawResolved();
            CardManager.Instance?.SyncHand(GameState.selfHand);
            foreach (var player in GameState.players.Where(player =>
                         string.Equals(player.lifeState, "Eliminated", StringComparison.OrdinalIgnoreCase)))
            {
                GameplayUIManager.Instance?.ShowPlayerEliminated(player.userId, "Đã bị loại");
            }

            if (!string.IsNullOrWhiteSpace(GameState.pendingReactionAction) &&
                GameState.reactionWindowEndsAt.HasValue)
            {
                GameplayUIManager.Instance?.ShowReactionWindow(
                    GameState.pendingReactionUserId,
                    GameState.pendingReactionAction,
                    GameState.pendingNopeCount,
                    GameState.pendingReactionTargetUserIds,
                    GameState.pendingReactionEffectScope,
                    isNewWindow: true);
            }

            if (IsSelf(GameState.pendingDefuseUserId))
                GameplayUIManager.Instance?.ShowDefuseWindow(GameState.defuseWindowEndsAt);
            else if (IsSelf(GameState.pendingBombOwnerUserId) && !string.IsNullOrWhiteSpace(GameState.pendingBombCardCode))
                GameplayUIManager.Instance?.OpenBombReinsertWindow(GameState.bombReinsertWindowEndsAt);
            else if (IsSelf(GameState.pendingFavorTargetId))
                GameplayUIManager.Instance?.ShowFavorWindow(GameState.pendingFavorRequesterId, GameState.pendingFavorTargetId);

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

            if (IsResolvedReactionEffect(payload.EventType) &&
                !string.IsNullOrWhiteSpace(GameState.pendingReactionAction) &&
                !GameState.reactionWindowEndsAt.HasValue)
            {
                GameState.pendingReactionUserId = null;
                GameState.pendingReactionAction = null;
                GameState.pendingReactionTargetUserIds = null;
                GameState.pendingReactionEffectScope = null;
                GameplayUIManager.Instance.ShowReactionResult(activated: true);
            }

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
                    if (IsSelf(turnContinues.userId))
                        GameManager.Instance?.NotifyLocalDrawResolved();
                    EventBus.Publish(EventType.TurnStart, new TurnStartEventPayload(turnContinues.userId));
                    break;

                case WsGameplayEventType.TurnTimeoutAutoDraw:
                    if (parsedPayload is not WsUserPayload timeoutAutoDraw)
                        return;

                    Debug.Log($"[GameSession] Turn timed out. Server auto-drew for userId={timeoutAutoDraw.userId}");
                    GameplayUIManager.Instance?.ShowActionToast(
                        "HẾT GIỜ",
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(timeoutAutoDraw.userId)} tự động rút bài",
                        status: "AUTO DRAW",
                        danger: true);
                    break;

                case WsGameplayEventType.CardDrawn:
                    if (parsedPayload is not WsCardActionPayload cardDrawn)
                        return;
                    EventBus.Publish(EventType.TurnEnd, new TurnEndEventPayload(cardDrawn.userId));
                    var addedToHand = cardDrawn.addedToHand ??
                                      cardDrawn.cardCode is not ("ExplodingKitten" or "ImplodingKitten");
                    GameState.drawPileCount = cardDrawn.drawPileCount >= 0
                        ? cardDrawn.drawPileCount
                        : Math.Max(0, GameState.drawPileCount - 1);
                    if (addedToHand)
                        ChangePlayerHandCount(cardDrawn.userId, +1);

                    //Handle player draw event
                    if (IsSelf(cardDrawn.userId) && !string.IsNullOrWhiteSpace(cardDrawn.cardCode))
                    {
                        if (!string.Equals(cardDrawn.cardCode, "Hidden", StringComparison.OrdinalIgnoreCase))
                            GameplayUIManager.Instance.DisplayDrawnCard(cardDrawn.cardCode);
                        if (addedToHand)
                        {
                            CardManager.Instance?.AddCardToHand(cardDrawn.cardCode);
                            GameState.selfHand.Add(cardDrawn.cardCode);
                        }
                    }
                    else if (!IsSelf(cardDrawn.userId))
                    {
                        GameplayUIManager.Instance.DisplayOpponentDraw(cardDrawn.userId);
                    }
                    GameplayUIManager.Instance?.ShowActionToast(
                        "RÚT BÀI",
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(cardDrawn.userId)} đã rút một lá",
                        $"Còn {GameState.drawPileCount} lá trong chồng rút",
                        "DRAW");
                    break;

                case WsGameplayEventType.StreakingHoldsBomb:
                    // CardDrawn.addedToHand already applied the authoritative hand delta.
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
                        if (CardManager.Instance == null || !CardManager.Instance.ConfirmPendingPlay())
                            CardManager.Instance?.RemoveCardFromHand(cardPlayed.cardCode);
                    }
                    else
                    {
                        //Make opponent play a card
                        GameplayUIManager.Instance.PlayOpponentCard(cardPlayed.userId, cardPlayed.cardCode);
                    }
                    GameplayUIManager.Instance?.ShowActionToast(
                        cardPlayed.cardCode,
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(cardPlayed.userId)} đã đánh {cardPlayed.cardCode}",
                        status: "PLAY");
                    break;

                case WsGameplayEventType.ComboPlayed:
                    if (parsedPayload is not WsComboPlayedPayload comboPlayed)
                        return;

                    if (comboPlayed.comboSize > 0)
                        ChangePlayerHandCount(comboPlayed.userId, -comboPlayed.comboSize);

                    var playedComboCards = comboPlayed.cardCodes;
                    if ((playedComboCards == null || playedComboCards.Length == 0) &&
                        comboPlayed.comboSize is 2 or 3 &&
                        !string.IsNullOrWhiteSpace(comboPlayed.cardCode))
                    {
                        playedComboCards = Enumerable.Repeat(comboPlayed.cardCode, comboPlayed.comboSize).ToArray();
                    }
                    playedComboCards ??= Array.Empty<string>();
                    foreach (var playedComboCard in playedComboCards)
                        GameState.discardPile.Add(playedComboCard);

                    if (IsSelf(comboPlayed.userId))
                    {
                        foreach (var consumedCard in playedComboCards)
                            RemoveOneCardFromSelfHand(consumedCard);

                        if (CardManager.Instance == null || !CardManager.Instance.ConfirmPendingPlay())
                        {
                            foreach (var consumedCard in playedComboCards)
                                CardManager.Instance?.RemoveCardFromHand(consumedCard);
                        }
                    }
                    GameplayUIManager.Instance?.ShowActionToast(
                        $"COMBO {comboPlayed.comboSize}",
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(comboPlayed.userId)} đã đánh combo",
                        status: "COMBO");
                    break;

                case WsGameplayEventType.ExplosionTriggered:
                    if (parsedPayload is not WsUserPayload explosion)
                        return;

                    GameState.pendingDefuseUserId = explosion.userId;
                    GameState.pendingBombOwnerUserId = explosion.userId;
                    GameState.pendingBombCardCode = "ExplodingKitten";
                    GameState.defuseWindowEndsAt = explosion.defuseWindowEndsAt;
                    if (IsSelf(explosion.userId))
                        GameplayUIManager.Instance?.ShowDefuseWindow(explosion.defuseWindowEndsAt);
                    GameplayUIManager.Instance?.ShowActionToast(
                        "BOOM!",
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(explosion.userId)} đã rút trúng Boom",
                        status: "NGUY HIỂM",
                        danger: true);
                    break;

                case WsGameplayEventType.DefuseUsed:
                    if (parsedPayload is not WsUserPayload defuseUsed)
                        return;

                    ChangePlayerHandCount(defuseUsed.userId, -1);
                    if (IsSelf(defuseUsed.userId))
                    {
                        RemoveOneCardFromSelfHand("Defuse");
                        CardManager.Instance?.RemoveCardFromHand("Defuse");
                    }
                    GameState.discardPile.Add("Defuse");
                    if (IsSelf(defuseUsed.userId))
                        GameplayUIManager.Instance?.PlayLocalCard("Defuse");
                    else
                        GameplayUIManager.Instance?.PlayOpponentCard(defuseUsed.userId, "Defuse");
                    GameplayUIManager.Instance?.ShowActionToast(
                        "DEFUSE",
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(defuseUsed.userId)} đã dùng Defuse",
                        "Bomb đã được vô hiệu hóa",
                        "AN TOÀN");

                    GameState.pendingDefuseUserId = null;
                    GameState.bombReinsertWindowEndsAt = defuseUsed.bombReinsertWindowEndsAt;
                    if (string.Equals(GameState.pendingBombOwnerUserId, defuseUsed.userId, StringComparison.OrdinalIgnoreCase))
                    {
                        GameState.pendingBombCardCode = "ExplodingKitten";
                    }
                    if (IsSelf(defuseUsed.userId))
                        GameplayUIManager.Instance?.OpenBombReinsertWindow(defuseUsed.bombReinsertWindowEndsAt);

                    break;

                case WsGameplayEventType.SkipApplied:
                    if (parsedPayload is not WsUserPayload skipApplied)
                        return;

                    SetPlayerPendingDraw(skipApplied.userId, Math.Max(0, GetPlayerPendingDraw(skipApplied.userId) - 1));
                    break;

                case WsGameplayEventType.BombReinserted:
                    if (parsedPayload is not WsBombReinsertedPayload bombReinserted)
                        return;

                    GameState.drawPileCount = bombReinserted.drawPileCount;
                    GameState.pendingBombOwnerUserId = null;
                    GameState.pendingBombCardCode = null;
                    GameState.pendingDefuseUserId = null;
                    GameState.bombReinsertWindowEndsAt = null;
                    GameState.defuseWindowEndsAt = null;
                    GameplayUIManager.Instance?.HideInteractionModal();
                    break;

                case WsGameplayEventType.BombReinsertAuto:
                    if (parsedPayload is WsBombReinsertedPayload autoReinsert)
                        GameState.drawPileCount = autoReinsert.drawPileCount;
                    else
                        GameState.drawPileCount++;
                    GameState.pendingBombOwnerUserId = null;
                    GameState.pendingBombCardCode = null;
                    GameState.bombReinsertWindowEndsAt = null;
                    GameplayUIManager.Instance?.HideInteractionModal();
                    break;

                case WsGameplayEventType.ShuffleApplied:
                    // No deterministic local mutation besides visual feedback.
                    // TODO: Add deck shuffle animation hook.
                    break;

                case WsGameplayEventType.PlayerEliminated:
                    if (parsedPayload is not WsPlayerEliminatedPayload eliminated)
                        return;

                    SetPlayerLifeState(eliminated.userId, "Eliminated");
                    GameplayUIManager.Instance?.ShowPlayerEliminated(eliminated.userId, eliminated.reason);
                    if (IsSelf(eliminated.userId))
                        GameplayUIManager.Instance?.HideInteractionModal();

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
                    GameState.favorWindowEndsAt = favorWindow.favorWindowEndsAt;
                    GameplayUIManager.Instance.ShowFavorWindow(favorWindow.requesterId, favorWindow.targetId);
                    break;

                case WsGameplayEventType.FavorResolved:
                    if (parsedPayload is not WsTransferPayload favorResolved)
                        return;

                    if (IsSelf(favorResolved.fromUserId))
                        GameplayUIManager.Instance?.AnimateFavorTransfer(favorResolved.cardCode, favorResolved.toUserId);
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

                case WsGameplayEventType.DrawPileEmpty:
                    GameState.drawPileCount = 0;
                    Debug.LogWarning("[GameSession] Draw pile is empty.");
                    break;

                case WsGameplayEventType.ImplodingReinsertRequired:
                    if (parsedPayload is not WsUserPayload implodingReinsert)
                        return;

                    GameState.pendingBombOwnerUserId = implodingReinsert.userId;
                    GameState.pendingBombCardCode = "ImplodingKitten";
                    break;

                case WsGameplayEventType.UnknownCommand:
                    if (parsedPayload is WsUnknownCommandPayload unknownCommand)
                        Debug.LogWarning($"[GameSession] Server rejected unknown command '{unknownCommand.name ?? "unknown"}'.");
                    break;

                case WsGameplayEventType.ReconnectAck:
                    Debug.Log("[GameSession] Reconnect acknowledged by game server.");
                    break;

                case WsGameplayEventType.CardEffectUnhandled:
                    if (parsedPayload is WsCardActionPayload unhandledEffect)
                    {
                        Debug.LogError(
                            $"[GameSession] Server has no effect implementation for card '{unhandledEffect.cardCode ?? "unknown"}'.");
                    }
                    break;

                case WsGameplayEventType.ReactionWindowOpened:
                    if (parsedPayload is not WsCardActionPayload reactionOpen)
                        return;

                    GameState.pendingReactionUserId = reactionOpen.userId;
                    GameState.pendingReactionAction = reactionOpen.cardCode;
                    GameState.pendingNopeCount = Math.Max(0, reactionOpen.nopeCount);
                    GameState.pendingReactionTargetUserIds = reactionOpen.targetUserIds;
                    GameState.pendingReactionEffectScope = reactionOpen.effectScope;
                    GameState.lastNopeUserId = null;
                    GameState.lastNopedAction = null;
                    if (reactionOpen.reactionWindowEndsAt.HasValue)
                        GameState.reactionWindowEndsAt = reactionOpen.reactionWindowEndsAt;
                    GameState.turnEndsAt = null;
                    Clock.PauseTurn();
                    GameplayUIManager.Instance.ShowReactionWindow(
                        reactionOpen.userId,
                        reactionOpen.cardCode,
                        GameState.pendingNopeCount,
                        reactionOpen.targetUserIds,
                        reactionOpen.effectScope,
                        isNewWindow: true);
                    break;

                case WsGameplayEventType.NopePlayed:
                    if (parsedPayload is not WsNopeActionPayload nopePlayed)
                        return;

                    GameState.pendingNopeCount = Math.Max(0, nopePlayed.nopeCount);
                    GameState.lastNopeUserId = nopePlayed.userId;
                    if (nopePlayed.targetUserIds != null)
                        GameState.pendingReactionTargetUserIds = nopePlayed.targetUserIds;
                    if (!string.IsNullOrWhiteSpace(nopePlayed.effectScope))
                        GameState.pendingReactionEffectScope = nopePlayed.effectScope;
                    if (nopePlayed.reactionWindowEndsAt.HasValue)
                        GameState.reactionWindowEndsAt = nopePlayed.reactionWindowEndsAt;
                    ChangePlayerHandCount(nopePlayed.userId, -1);

                    if (IsSelf(nopePlayed.userId))
                    {
                        RemoveOneCardFromSelfHand("Nope");
                        CardManager.Instance?.RemoveCardFromHand("Nope");
                        GameplayUIManager.Instance.PlayLocalCard("Nope");
                    }
                    else
                    {
                        GameplayUIManager.Instance.PlayOpponentCard(nopePlayed.userId, "Nope");
                    }

                    GameState.discardPile.Add("Nope");
                    GameplayUIManager.Instance?.ShowActionToast(
                        "NOPE!",
                        $"{GameplayUIManager.Instance.GetPlayerDisplayName(nopePlayed.userId)} đã đánh NOPE",
                        status: "NOPE",
                        danger: true);
                    GameplayUIManager.Instance.ShowReactionWindow(
                        GameState.pendingReactionUserId,
                        GameState.pendingReactionAction,
                        GameState.pendingNopeCount,
                        GameState.pendingReactionTargetUserIds,
                        GameState.pendingReactionEffectScope,
                        isNewWindow: false);
                    break;

                case WsGameplayEventType.ReactionWindowClosed:
                    if (parsedPayload is not WsReactionWindowClosedPayload reactionClosed)
                        return;

                    GameState.pendingNopeCount = Math.Max(0, reactionClosed.nopeCount);
                    GameState.reactionWindowEndsAt = null;
                    GameplayUIManager.Instance.HoldReactionWindowForResolution();
                    break;

                case WsGameplayEventType.ActionNoped:
                    if (parsedPayload is not WsNopeActionPayload actionNoped)
                        return;

                    GameState.pendingNopeCount = Math.Max(0, actionNoped.nopeCount);
                    GameState.lastNopedAction = actionNoped.cardCode;
                    GameState.pendingReactionUserId = null;
                    GameState.pendingReactionAction = null;
                    GameState.reactionWindowEndsAt = null;
                    GameState.pendingReactionTargetUserIds = null;
                    GameState.pendingReactionEffectScope = null;
                    GameplayUIManager.Instance.ShowReactionResult(activated: false);
                    break;

                case WsGameplayEventType.ActionRejected:
                    if (parsedPayload is not WsActionRejectedPayload rejected)
                        return;

                    GameplayUIManager.Instance.ClearTargetSelection();
                    CardManager.Instance?.RejectPendingPlay();
                    GameManager.Instance?.NotifyLocalDrawResolved();
                    GameplayUIManager.Instance?.HideInteractionModal();
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

        private static bool IsResolvedReactionEffect(WsGameplayEventType eventType)
        {
            return eventType is
                WsGameplayEventType.SkipApplied or
                WsGameplayEventType.ShuffleApplied or
                WsGameplayEventType.AttackApplied or
                WsGameplayEventType.FuturePeeked or
                WsGameplayEventType.FavorWindowOpened or
                WsGameplayEventType.CatComboTwoResolved or
                WsGameplayEventType.CatComboThreeResolved or
                WsGameplayEventType.CatComboThreeMiss or
                WsGameplayEventType.CatComboFiveResolved or
                WsGameplayEventType.BuryResolved or
                WsGameplayEventType.IllTakeThatMarked or
                WsGameplayEventType.TowerMaskUpdated or
                WsGameplayEventType.MarkApplied or
                WsGameplayEventType.CatButtCurseApplied or
                WsGameplayEventType.CatomicBombResolved or
                WsGameplayEventType.FavorTargetEmpty or
                WsGameplayEventType.SwapTopBottom or
                WsGameplayEventType.GarbageCollectionResolved or
                WsGameplayEventType.BarkingKittenResolved or
                WsGameplayEventType.CardDrawn or
                WsGameplayEventType.CardEffectUnhandled or
                WsGameplayEventType.TurnChanged;
        }

        public void UpdateTurn(int newTurnIndex, DateTime? turnEndsAt, int turnTimerSeconds, DateTime serverTimeUtc)
        {
            GameManager.Instance?.NotifyLocalDrawResolved();
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
            var isLocalTurn = string.Equals(curUserID, GameManager.Instance?.Player?.ID, StringComparison.OrdinalIgnoreCase);
            GameplayUIManager.Instance?.ShowActionToast(
                "ĐẾN LƯỢT",
                isLocalTurn ? "Đến lượt của bạn" : $"Đến lượt của {GameplayUIManager.Instance.GetPlayerDisplayName(curUserID)}",
                status: "TURN");
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
                {
                    RemoveOneCardFromSelfHand(cardCode);
                    CardManager.Instance?.RemoveCardFromHand(cardCode);
                }
                if (IsSelf(toUserId))
                {
                    GameState.selfHand.Add(cardCode);
                    CardManager.Instance?.AddCardToHand(cardCode);
                }
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
