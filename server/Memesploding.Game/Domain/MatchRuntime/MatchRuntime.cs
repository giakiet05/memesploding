using System.Threading.Channels;
using Memesploding.Game.Domain.StateMachine;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memesploding.Game.Domain.MatchRuntime;

public class MatchRuntime
{
    private readonly Channel<RuntimeCommand> _commandQueue = Channel.CreateUnbounded<RuntimeCommand>();
    private readonly Random _random = new();

    public MatchRuntime(MatchRuntimeState state)
    {
        State = state;
    }

    public MatchRuntimeState State { get; }

    public void ProcessReconnectTimeouts()
    {
        if (State.Phase != MatchPhase.Playing)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var player in State.Players.Where(p => p.LifeState == PlayerLifeState.Alive && !p.Connected && p.PendingReconnectUntil.HasValue && p.PendingReconnectUntil.Value <= now).ToList())
        {
            EliminatePlayer(player.UserId, "reconnect_timeout");
        }
    }

    public ValueTask EnqueueAsync(RuntimeCommand command, CancellationToken cancellationToken = default)
        => _commandQueue.Writer.WriteAsync(command, cancellationToken);

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        while (_commandQueue.Reader.TryRead(out var command))
        {
            HandleCommand(command);
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        if (State.Phase == MatchPhase.Playing &&
            State.TurnAdvanceAt.HasValue &&
            State.TurnAdvanceAt.Value <= DateTime.UtcNow)
        {
            AdvanceTurnNow();
        }

        if (State.Phase == MatchPhase.Playing && State.TurnEndsAt.HasValue && State.TurnEndsAt.Value <= DateTime.UtcNow)
        {
            var currentTurnUserId = GetCurrentTurnUserId();
            if (currentTurnUserId.HasValue && State.PendingReactionAction == null && !State.PendingDefuseUserId.HasValue && State.PendingBombCardCode == null)
            {
                if (DrawCardForPlayer(currentTurnUserId.Value, fromBottom: false))
                {
                    IncrementVersion("TurnTimeoutAutoDraw", $"{{\"userId\":\"{currentTurnUserId.Value}\"}}");
                    ConsumePendingDraw(currentTurnUserId.Value);
                    if (!IsBombResolutionPendingFor(currentTurnUserId.Value))
                    {
                        CompleteCurrentTurnAfterDrawResolution(currentTurnUserId.Value);
                    }
                }
                else
                    State.TurnEndsAt = null;
            }
        }

        if (State.ReactionWindowEndsAt.HasValue &&
            State.ReactionWindowEndsAt.Value <= DateTime.UtcNow &&
            State.PendingReactionAction != null &&
            !State.ReactionResolveAt.HasValue)
        {
            State.ReactionWindowEndsAt = null;
            State.ReactionResolveAt = DateTime.UtcNow.AddMilliseconds(State.EffectResolutionDelayMs);
            IncrementVersion(
                "ReactionWindowClosed",
                $"{{\"cardCode\":\"{State.PendingReactionAction}\",\"nopeCount\":{State.PendingNopeCount}}}"
            );
        }

        if (State.ReactionResolveAt.HasValue &&
            State.ReactionResolveAt.Value <= DateTime.UtcNow &&
            State.PendingReactionAction != null &&
            State.PendingReactionUserId.HasValue)
        {
            ResolvePendingReactionAction();
        }

        if (State.DefuseWindowEndsAt.HasValue && State.DefuseWindowEndsAt.Value <= DateTime.UtcNow && State.PendingDefuseUserId.HasValue)
        {
            var userId = State.PendingDefuseUserId.Value;
            EliminatePlayer(userId, "defuse_timeout");
            State.PendingDefuseUserId = null;
            State.DefuseWindowEndsAt = null;
            State.PendingBombOwnerUserId = null;
            State.PendingBombCardCode = null;
        }

        if (State.BombReinsertWindowEndsAt.HasValue && State.BombReinsertWindowEndsAt.Value <= DateTime.UtcNow && State.PendingBombCardCode != null)
        {
            var ownerUserId = State.PendingBombOwnerUserId;
            InsertBombAtRandom(State.PendingBombCardCode);
            State.BombReinsertWindowEndsAt = null;
            State.PendingBombOwnerUserId = null;
            State.PendingBombCardCode = null;
            IncrementVersion("BombReinsertAuto", "{}");
            if (ownerUserId.HasValue && !IsBombResolutionPendingFor(ownerUserId.Value))
            {
                CompleteCurrentTurnAfterDrawResolution(ownerUserId.Value);
            }
        }

        if (State.FavorWindowEndsAt.HasValue && State.FavorWindowEndsAt.Value <= DateTime.UtcNow &&
            State.PendingFavorRequesterId.HasValue && State.PendingFavorTargetId.HasValue)
        {
            // Timeout: auto-pick random card from Bob
            ExecuteFavorTransfer(State.PendingFavorRequesterId.Value, State.PendingFavorTargetId.Value, null);
        }

        ProcessReconnectTimeouts();
        CheckMatchFinished();
    }

    public void MarkStarted()
    {
        State.Phase = MatchPhase.Playing;
        State.StartedAt = DateTime.UtcNow;
        StartTurnLoop();
        IncrementVersion("MatchStarted", "{}");
    }

    public void StartTurnLoop()
    {
        if (State.Players.Count == 0)
        {
            return;
        }

        State.TurnIndex = ((State.TurnIndex % State.Players.Count) + State.Players.Count) % State.Players.Count;
        State.TurnCounter++;
        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        IncrementVersion("TurnStarted", CreateTurnTimingPayload(State.TurnIndex));
    }

    public Guid? GetCurrentTurnUserId()
    {
        if (State.Players.Count == 0)
        {
            return null;
        }

        return State.Players[State.TurnIndex].UserId;
    }

    private void IncrementVersion(string eventType, string payload)
    {
        State.StateVersion++;
        State.EventLog.Add(new MatchRuntimeEvent(eventType, payload, State.StateVersion, DateTime.UtcNow));
    }

    private void HandleCommand(RuntimeCommand command)
    {
        var eventName = command.Name.Trim().ToLowerInvariant();
        switch (eventName)
        {
            case "drawcard":
                HandleDrawCardCommand(command.UserId, false);
                break;
            case "drawfrombottom":
                HandleDrawCardCommand(command.UserId, true);
                break;
            case "playcard":
                HandlePlayCardCommand(command.UserId, command.Payload);
                break;
            case "begininteraction":
                HandleBeginInteraction(command.UserId);
                break;
            case "reconnectmatch":
                MarkReconnect(command.UserId);
                break;
            case "nope":
                HandleNopeCommand(command.UserId);
                break;
            case "usedefuse":
                HandleUseDefuse(command.UserId);
                break;
            case "choosebombinsertposition":
                HandleChooseBombInsertPosition(command.UserId, command.Payload);
                break;
            case "choosefavorcard":
                HandleChooseFavorCard(command.UserId, command.Payload);
                break;
            default:
                IncrementVersion("UnknownCommand", $"{{\"name\":\"{command.Name}\"}}");
                break;
        }
    }

    private void HandleDrawCardCommand(Guid userId, bool fromBottom)
    {
        if (State.Phase != MatchPhase.Playing)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"INVALID_PHASE\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!IsAlivePlayer(userId))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"PLAYER_ELIMINATED\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.TurnAdvanceAt.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"TURN_TRANSITION_PENDING\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingReactionAction != null || State.ReactionResolveAt.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"REACTION_IN_PROGRESS\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingDefuseUserId.HasValue || State.PendingBombCardCode != null)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"BOMB_RESOLUTION_PENDING\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingFavorTargetId.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"FAVOR_IN_PROGRESS\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (GetCurrentTurnUserId() != userId)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NOT_TURN\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!DrawCardForPlayer(userId, fromBottom))
        {
            State.TurnEndsAt = null;
            return;
        }
        ConsumePendingDraw(userId);
        if (!IsBombResolutionPendingFor(userId))
        {
            CompleteCurrentTurnAfterDrawResolution(userId);
        }
    }

    public void HandlePlayCardCommand(Guid userId, string payload)
    {
        if (State.Phase != MatchPhase.Playing)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"INVALID_PHASE\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!IsAlivePlayer(userId))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"PLAYER_ELIMINATED\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryGetCardCode(payload, out var cardCode))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"INVALID_PAYLOAD\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.TurnAdvanceAt.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"TURN_TRANSITION_PENDING\",\"userId\":\"{userId}\"}}");
            return;
        }

        // Nope can be played regardless of turn if window is open
        if (cardCode == "Nope")
        {
            HandleNopeCommand(userId);
            return;
        }

        if (State.PendingReactionAction != null || State.ReactionWindowEndsAt.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"REACTION_IN_PROGRESS\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingDefuseUserId.HasValue || State.PendingBombCardCode != null)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"BOMB_RESOLUTION_PENDING\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingFavorTargetId.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"FAVOR_IN_PROGRESS\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (GetCurrentTurnUserId() != userId)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NOT_TURN\",\"userId\":\"{userId}\"}}");
            return;
        }

        var playerIndex = State.Players.FindIndex(p => p.UserId == userId);
        if (playerIndex < 0) return;
        var hand = State.Players[playerIndex].Hand ?? [];

        // Universal Combo Support
        if (TryGetComboSize(payload, out var comboSize) && comboSize >= 2)
        {
            if (!ValidateComboInteraction(userId, comboSize, payload))
            {
                return;
            }

            if (TryApplyUniversalCombo(userId, playerIndex, hand, cardCode, comboSize, payload))
            {
                return;
            }
            return;
        }

        if (!ValidateCardInteraction(userId, cardCode, payload))
        {
            return;
        }

        if (!hand.Remove(cardCode))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"CARD_NOT_OWNED\",\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\"}}");
            return;
        }
        State.Players[playerIndex] = State.Players[playerIndex] with { Hand = hand };
        State.DiscardPile.Add(cardCode);
        IncrementVersion("CardPlayed", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\"}}");
        if (!RequiresReactionWindow(cardCode))
            RefreshCurrentTurn(userId);

        ApplyCardEffect(userId, cardCode, payload);
    }

    private void HandleNopeCommand(Guid userId)
    {
        if (!State.ReactionWindowEndsAt.HasValue || State.ReactionWindowEndsAt.Value < DateTime.UtcNow || State.PendingReactionAction == null)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NO_REACTION_WINDOW\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryRemoveCardFromHand(userId, "Nope"))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"CARD_NOT_OWNED\",\"userId\":\"{userId}\",\"cardCode\":\"Nope\"}}");
            return;
        }

        State.PendingNopeCount++;
        State.ReactionResolveAt = null;
        State.ReactionWindowEndsAt = DateTime.UtcNow.AddSeconds(State.NopeWindowSeconds);
        IncrementVersion(
            "nope_played",
            CreateReactionPayload(userId, State.PendingReactionAction, State.PendingNopeCount, State.ReactionWindowEndsAt.Value)
        );
    }

    private void ApplyCardEffect(Guid userId, string cardCode, string payload)
    {
        if (RequiresReactionWindow(cardCode))
        {
            OpenReactionWindow(userId, cardCode, payload);
            return;
        }

        ResolveCardEffect(userId, cardCode, payload);
    }

    private void ResolveCardEffect(Guid userId, string cardCode, string payload)
    {
        switch (cardCode)
        {
            case "Skip":
                ApplySkip(userId, superSkip: false);
                break;
            case "SuperSkip":
                ApplySkip(userId, superSkip: true);
                break;
            case "Attack":
            case "TargetedAttack":
            case "PersonalAttack":
                ApplyAttack(userId, cardCode, payload);
                ScheduleTurnAdvance();
                break;
            case "Reverse":
                State.TurnDirection *= -1;
                ScheduleTurnAdvance();
                break;
            case "Shuffle":
                Shuffle(State.DrawPile);
                IncrementVersion("ShuffleApplied", "{}");
                break;
            case "SeeTheFuture":
            case "AlterTheFuture":
            case "AlterTheFutureNow":
            case "AlterTheFuture5":
                EmitTopCardsPeek(userId, cardCode);
                break;
            case "DrawFromBottom":
                HandleDrawCardCommand(userId, true);
                break;
            case "Favor":
                ApplyFavor(userId, payload);
                break;
            case "Combo2":
                ResolveTwoOfKindCombo(userId, payload);
                break;
            case "Combo3":
                ResolveThreeOfKindCombo(userId, payload);
                break;
            case "Combo5":
                ResolveFiveOfKindCombo(userId, payload);
                break;
            case "Bury":
                ApplyBury(userId);
                break;
            case "IllTakeThat":
                ApplyIllTakeThat(userId, payload);
                break;
            case "TowerMask":
                SetTowerMask(userId, true);
                break;
            case "Mark":
                ApplyMark(userId, payload);
                break;
            case "CurseOfTheCatButt":
                ApplyCurseOfCatButt(userId, payload);
                break;
            case "SwapTopAndBottom":
                SwapTopAndBottom();
                break;
            case "GarbageCollection":
                ApplyGarbageCollection();
                break;
            case "CatomicBomb":
                ApplyCatomicBomb();
                break;
            case "BarkingKitten":
                ApplyBarkingKitten(userId, payload);
                break;
            case "StreakingKitten":
                IncrementVersion("StreakingKittenActive", $"{{\"userId\":\"{userId}\"}}");
                break;
            case "FeralCat":
                IncrementVersion("FeralCatPlayed", $"{{\"userId\":\"{userId}\"}}");
                break;
            default:
                IncrementVersion("CardEffectUnhandled", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\"}}");
                break;
        }
    }

    private void OpenReactionWindow(Guid userId, string actionCardCode, string payload)
    {
        if (State.PendingReactionAction != null)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"REACTION_IN_PROGRESS\",\"userId\":\"{userId}\"}}");
            return;
        }

        State.PendingReactionUserId = userId;
        State.PendingReactionAction = actionCardCode;
        State.PendingReactionPayload = payload;
        State.PendingNopeCount = 0;
        State.ReactionResolveAt = null;
        State.TurnEndsAt = null;
        State.ReactionWindowEndsAt = DateTime.UtcNow.AddSeconds(State.NopeWindowSeconds);
        (State.PendingReactionTargetUserIds, State.PendingReactionEffectScope) =
            ResolveReactionTargets(userId, actionCardCode, payload);
        State.PendingReactionPayload = AddResolvedReactionTarget(payload);
        IncrementVersion("ReactionWindowOpened", CreateReactionPayload(userId, actionCardCode, 0, State.ReactionWindowEndsAt.Value));
    }

    private void ResolvePendingReactionAction()
    {
        var actionCardCode = State.PendingReactionAction;
        var actorUserId = State.PendingReactionUserId;
        var actionPayload = State.PendingReactionPayload ?? "{}";
        var nopeCount = State.PendingNopeCount;

        State.PendingReactionAction = null;
        State.PendingReactionUserId = null;
        State.PendingReactionPayload = null;
        State.ReactionWindowEndsAt = null;
        State.ReactionResolveAt = null;
        State.PendingNopeCount = 0;
        State.PendingReactionTargetUserIds = [];
        State.PendingReactionEffectScope = "none";

        if (actionCardCode == null || !actorUserId.HasValue)
        {
            return;
        }

        if (nopeCount % 2 == 1)
        {
            IncrementVersion(
                "action_noped",
                $"{{\"userId\":\"{actorUserId.Value}\",\"cardCode\":\"{actionCardCode}\",\"nopeCount\":{nopeCount}}}"
            );
            if (GetCurrentTurnUserId() == actorUserId.Value && IsAlivePlayer(actorUserId.Value))
            {
                var player = State.Players.First(x => x.UserId == actorUserId.Value);
                State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
                IncrementVersion("TurnContinues", CreateTurnContinuesPayload(actorUserId.Value, Math.Max(1, player.PendingDrawCount)));
            }
            return;
        }

        ResolveCardEffect(actorUserId.Value, actionCardCode, actionPayload);
        RefreshCurrentTurn(actorUserId.Value);
    }

    private static bool RequiresReactionWindow(string cardCode)
    {
        return cardCode is
            "Attack" or
            "TargetedAttack" or
            "PersonalAttack" or
            "Favor" or
            "Shuffle" or
            "SeeTheFuture" or
            "AlterTheFuture" or
            "AlterTheFutureNow" or
            "AlterTheFuture5" or
            "Bury" or
            "IllTakeThat" or
            "TowerMask" or
            "Mark" or
            "CurseOfTheCatButt" or
            "SwapTopAndBottom" or
            "GarbageCollection" or
            "CatomicBomb" or
            "BarkingKitten" or
            "Reverse" or
            "Skip" or
            "SuperSkip" or
            "DrawFromBottom" or
            "Combo2" or
            "Combo3" or
            "Combo5";
    }

    private void ApplyAttack(Guid userId, string cardCode, string payload)
    {
        var targetUserId = cardCode == "Attack"
            ? GetNextAliveTurnUserId()
            : ResolveTargetUser(userId, payload);
        if (!targetUserId.HasValue)
        {
            return;
        }

        var targetIndex = State.Players.FindIndex(player => player.UserId == targetUserId.Value && player.LifeState == PlayerLifeState.Alive);
        if (targetIndex < 0)
        {
            return;
        }

        var actorIndex = State.Players.FindIndex(player => player.UserId == userId);
        var actorPending = actorIndex >= 0
            ? Math.Max(1, State.Players[actorIndex].PendingDrawCount)
            : 1;
        if (actorIndex >= 0)
        {
            State.Players[actorIndex] = State.Players[actorIndex] with { PendingDrawCount = 1 };
        }

        var currentPending = Math.Max(1, State.Players[targetIndex].PendingDrawCount);
        var added = cardCode == "PersonalAttack" ? 3 : 2;
        var attackLoad = actorPending + added;
        var nextPending = currentPending <= 1 ? attackLoad : currentPending + attackLoad;
        State.Players[targetIndex] = State.Players[targetIndex] with { PendingDrawCount = nextPending };
        IncrementVersion("AttackApplied", $"{{\"from\":\"{userId}\",\"to\":\"{targetUserId.Value}\",\"added\":{added}}}");
    }

    private Guid? GetNextAliveTurnUserId()
    {
        var nextIndex = GetNextAliveTurnIndex();
        return nextIndex < 0 ? null : State.Players[nextIndex].UserId;
    }

    private void ApplySkip(Guid userId, bool superSkip)
    {
        var playerIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (playerIndex < 0)
        {
            return;
        }

        var pending = Math.Max(1, State.Players[playerIndex].PendingDrawCount);
        pending = superSkip ? 0 : pending - 1;
        State.Players[playerIndex] = State.Players[playerIndex] with { PendingDrawCount = pending };
        IncrementVersion("SkipApplied", $"{{\"userId\":\"{userId}\"}}");

        if (pending <= 0)
        {
            State.Players[playerIndex] = State.Players[playerIndex] with { PendingDrawCount = 1 };
            ScheduleTurnAdvance();
            return;
        }

        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        IncrementVersion("TurnContinues", CreateTurnContinuesPayload(userId, pending));
    }

    private void ConsumePendingDraw(Guid userId)
    {
        if (!IsAlivePlayer(userId))
        {
            return;
        }

        var playerIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (playerIndex < 0)
        {
            return;
        }

        var pending = Math.Max(1, State.Players[playerIndex].PendingDrawCount);
        pending--;
        State.Players[playerIndex] = State.Players[playerIndex] with { PendingDrawCount = pending };
    }

    private void CompleteCurrentTurnAfterDrawResolution(Guid userId)
    {
        if (!IsAlivePlayer(userId))
        {
            return;
        }

        var playerIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (playerIndex < 0)
        {
            return;
        }

        var pending = State.Players[playerIndex].PendingDrawCount;
        if (pending <= 0)
        {
            State.Players[playerIndex] = State.Players[playerIndex] with { PendingDrawCount = 1 };
            ScheduleTurnAdvance();
            return;
        }

        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        IncrementVersion("TurnContinues", CreateTurnContinuesPayload(userId, pending));
    }

    private bool IsBombResolutionPendingFor(Guid userId)
    {
        return State.PendingDefuseUserId == userId ||
               (State.PendingBombCardCode != null && State.PendingBombOwnerUserId == userId);
    }

    private bool IsAlivePlayer(Guid userId)
    {
        return State.Players.Any(player => player.UserId == userId && player.LifeState == PlayerLifeState.Alive);
    }

    private bool DrawCardForPlayer(Guid userId, bool fromBottom)
    {
        if (State.DrawPile.Count == 0)
        {
            IncrementVersion("DrawPileEmpty", "{}");
            return false;
        }

        var card = fromBottom ? DrawFromBottomInternal() : DrawFromTopInternal();

        var playerIndex = State.Players.FindIndex(p => p.UserId == userId);
        if (playerIndex < 0)
        {
            return false;
        }

        var hand = State.Players[playerIndex].Hand ?? [];
        var addedToHand = card switch
        {
            "ExplodingKitten" => HasCardInHand(userId, "StreakingKitten") || !HasCardInHand(userId, "Defuse"),
            "ImplodingKitten" => false,
            _ => true
        };
        IncrementVersion("CardDrawn", JsonSerializer.Serialize(new { userId, cardCode = card, addedToHand }));

        if (card == "ExplodingKitten")
        {
            if (HasCardInHand(userId, "StreakingKitten"))
            {
                hand.Add(card);
                IncrementVersion("StreakingHoldsBomb", $"{{\"userId\":\"{userId}\"}}");
            }
            else
            {
                if (!HasCardInHand(userId, "Defuse"))
                {
                    hand.Add(card);
                    State.Players[playerIndex] = State.Players[playerIndex] with { Hand = hand };
                    EliminatePlayer(userId, "missing_defuse");
                    return true;
                }

                State.PendingDefuseUserId = userId;
                State.DefuseWindowEndsAt = DateTime.UtcNow.AddSeconds(State.DefuseDecisionSeconds);
                State.PendingBombOwnerUserId = userId;
                State.PendingBombCardCode = "ExplodingKitten";
                IncrementVersion("ExplosionTriggered", JsonSerializer.Serialize(new
                {
                    userId,
                    defuseWindowEndsAt = State.DefuseWindowEndsAt
                }));
            }
        }
        else if (card == "ImplodingKitten")
        {
            if (!State.ImplodingKittenFaceUpInDeck)
            {
                State.ImplodingKittenFaceUpInDeck = true;
                State.PendingBombOwnerUserId = userId;
                State.PendingBombCardCode = "ImplodingKitten";
                State.BombReinsertWindowEndsAt = DateTime.UtcNow.AddSeconds(State.BombReinsertSeconds);
                IncrementVersion("ImplodingReinsertRequired", $"{{\"userId\":\"{userId}\"}}");
            }
            else
            {
                EliminatePlayer(userId, "imploding_second_draw");
            }
        }
        else
        {
            hand.Add(card);
        }

        State.Players[playerIndex] = State.Players[playerIndex] with { Hand = hand };
        return true;
    }

    private string DrawFromTopInternal()
    {
        var card = State.DrawPile[0];
        State.DrawPile.RemoveAt(0);
        return card;
    }

    private string DrawFromBottomInternal()
    {
        var lastIndex = State.DrawPile.Count - 1;
        var card = State.DrawPile[lastIndex];
        State.DrawPile.RemoveAt(lastIndex);
        return card;
    }

    private void ScheduleTurnAdvance()
    {
        if (State.Phase != MatchPhase.Playing || State.TurnAdvanceAt.HasValue)
        {
            return;
        }

        State.TurnEndsAt = null;
        State.TurnAdvanceAt = DateTime.UtcNow.AddMilliseconds(State.TurnTransitionDelayMs);
    }

    private void AdvanceTurnNow()
    {
        State.TurnAdvanceAt = null;
        if (State.Players.Count == 0)
        {
            return;
        }

        State.TurnIndex = GetNextAliveTurnIndex();
        if (State.TurnIndex < 0)
        {
            State.Phase = MatchPhase.Finished;
            IncrementVersion("MatchFinished", "{}");
            return;
        }

        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        State.TurnCounter++;
        IncrementVersion("TurnChanged", CreateTurnTimingPayload(State.TurnIndex));
    }

    private string CreateTurnTimingPayload(int turnIndex)
    {
        return JsonSerializer.Serialize(new
        {
            turnIndex,
            turnEndsAt = State.TurnEndsAt,
            turnTimerSeconds = State.TurnTimerSeconds,
            serverTimeUtc = DateTime.UtcNow
        });
    }

    private string CreateTurnContinuesPayload(Guid userId, int pendingDrawCount)
    {
        return JsonSerializer.Serialize(new
        {
            userId,
            pendingDrawCount,
            turnEndsAt = State.TurnEndsAt,
            turnTimerSeconds = State.TurnTimerSeconds,
            serverTimeUtc = DateTime.UtcNow
        });
    }

    private int GetNextAliveTurnIndex()
    {
        if (State.Players.Count == 0)
        {
            return -1;
        }

        var idx = State.TurnIndex;
        for (var i = 0; i < State.Players.Count; i++)
        {
            idx = (idx + State.TurnDirection + State.Players.Count) % State.Players.Count;
            if (State.Players[idx].LifeState == PlayerLifeState.Alive)
            {
                return idx;
            }
        }

        return -1;
    }

    private static bool TryGetCardCode(string payload, out string cardCode)
    {
        cardCode = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (!root.TryGetProperty("cardCode", out var value))
            {
                return false;
            }

            cardCode = value.GetString() ?? string.Empty;
            return cardCode.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetComboSize(string payload, out int comboSize)
    {
        comboSize = 0;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty("comboSize", out var comboElement))
            {
                return false;
            }

            comboSize = comboElement.GetInt32();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetGuidProperty(string payload, string propertyName, out Guid guid)
    {
        guid = Guid.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty(propertyName, out var element))
            {
                return false;
            }

            var raw = element.GetString();
            return raw != null && Guid.TryParse(raw, out guid);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetStringProperty(string payload, string propertyName, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty(propertyName, out var element))
            {
                return false;
            }

            value = element.GetString() ?? string.Empty;
            return value.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Shuffle(List<string> cards)
    {
        var random = new Random();
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    private Guid? ResolveTargetUser(Guid actorUserId, string payload)
    {
        if (TryGetGuidProperty(payload, "targetUserId", out var targetFromPayload))
        {
            var isValid = State.Players.Any(
                player => player.UserId == targetFromPayload &&
                          player.UserId != actorUserId &&
                          player.LifeState == PlayerLifeState.Alive
            );
            if (isValid)
            {
                return targetFromPayload;
            }
        }

        return GetRandomAliveTarget(actorUserId);
    }

    private bool TryResolveExplicitTarget(Guid actorUserId, string payload, out Guid targetUserId)
    {
        targetUserId = Guid.Empty;
        if (!TryGetGuidProperty(payload, "targetUserId", out var requestedTarget) ||
            requestedTarget == actorUserId ||
            !IsAlivePlayer(requestedTarget))
        {
            return false;
        }

        targetUserId = requestedTarget;
        return true;
    }

    private bool ValidateCardInteraction(Guid userId, string cardCode, string payload)
    {
        if (IsCatCard(cardCode) || cardCode is "Defuse" or "ExplodingKitten" or "ImplodingKitten")
        {
            IncrementVersion("ActionRejected", JsonSerializer.Serialize(new
            {
                reason = "INVALID_CARD_PLAY",
                userId,
                cardCode
            }));
            return false;
        }

        if (cardCode is not ("Favor" or "TargetedAttack" or "IllTakeThat" or "Mark" or
            "CurseOfTheCatButt" or "BarkingKitten"))
        {
            return true;
        }

        if (TryResolveExplicitTarget(userId, payload, out _))
        {
            return true;
        }

        IncrementVersion("ActionRejected", JsonSerializer.Serialize(new
        {
            reason = "INVALID_TARGET",
            userId,
            cardCode
        }));
        return false;
    }

    private bool ValidateComboInteraction(Guid userId, int comboSize, string payload)
    {
        if (comboSize is 2 or 3 && !TryResolveExplicitTarget(userId, payload, out _))
        {
            IncrementVersion("ActionRejected", JsonSerializer.Serialize(new
            {
                reason = "INVALID_TARGET",
                userId,
                comboSize
            }));
            return false;
        }

        if (comboSize == 3 && !TryGetStringProperty(payload, "requestedCardCode", out _))
        {
            IncrementVersion("ActionRejected", JsonSerializer.Serialize(new
            {
                reason = "MISSING_REQUESTED_CARD",
                userId
            }));
            return false;
        }

        if (comboSize == 5 &&
            (!TryGetStringProperty(payload, "discardCardCode", out var discardCardCode) ||
             !State.DiscardPile.Contains(discardCardCode)))
        {
            IncrementVersion("ActionRejected", JsonSerializer.Serialize(new
            {
                reason = "INVALID_DISCARD_CARD",
                userId
            }));
            return false;
        }

        return true;
    }

    private (List<Guid> TargetUserIds, string EffectScope) ResolveReactionTargets(
        Guid actorUserId,
        string cardCode,
        string payload)
    {
        if (cardCode is "Skip" or "SuperSkip" or "SeeTheFuture" or "AlterTheFuture" or
            "AlterTheFutureNow" or "AlterTheFuture5" or "DrawFromBottom" or "Reverse" or "TowerMask")
        {
            return ([actorUserId], "self");
        }

        if (cardCode is "Shuffle" or "SwapTopAndBottom" or "CatomicBomb" or "Bury")
        {
            return ([], "draw_pile");
        }

        if (cardCode is "GarbageCollection")
        {
            return (
                State.Players
                    .Where(player => player.LifeState == PlayerLifeState.Alive)
                    .Select(player => player.UserId)
                    .ToList(),
                "all_players"
            );
        }

        if (cardCode is "Combo5")
        {
            return ([], "discard_pile");
        }

        Guid? target = cardCode == "Attack"
            ? GetNextAliveTurnUserId()
            : ResolveTargetUser(actorUserId, payload);

        return target.HasValue ? ([target.Value], "target_players") : ([], "none");
    }

    private string AddResolvedReactionTarget(string payload)
    {
        if (State.PendingReactionTargetUserIds.Count != 1 ||
            State.PendingReactionEffectScope != "target_players")
        {
            return payload;
        }

        try
        {
            var root = JsonNode.Parse(payload) as JsonObject ?? new JsonObject();
            root["targetUserId"] = State.PendingReactionTargetUserIds[0];
            return root.ToJsonString();
        }
        catch
        {
            return JsonSerializer.Serialize(new { targetUserId = State.PendingReactionTargetUserIds[0] });
        }
    }

    private string CreateReactionPayload(Guid userId, string cardCode, int nopeCount, DateTime reactionWindowEndsAt)
    {
        return JsonSerializer.Serialize(new
        {
            userId,
            cardCode,
            reactionWindowEndsAt,
            nopeCount,
            targetUserIds = State.PendingReactionTargetUserIds,
            effectScope = State.PendingReactionEffectScope
        });
    }

    private void HandleUseDefuse(Guid userId)
    {
        if (State.PendingDefuseUserId != userId || State.PendingBombCardCode == null)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NO_PENDING_DEFUSE\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryRemoveCardFromHand(userId, "Defuse"))
        {
            EliminatePlayer(userId, "missing_defuse");
            return;
        }

        State.DefuseWindowEndsAt = null;
        State.PendingDefuseUserId = null;
        State.PendingBombOwnerUserId = userId;
        State.BombReinsertWindowEndsAt = DateTime.UtcNow.AddSeconds(State.BombReinsertSeconds);
        IncrementVersion("DefuseUsed", JsonSerializer.Serialize(new
        {
            userId,
            bombReinsertWindowEndsAt = State.BombReinsertWindowEndsAt
        }));
    }

    private void HandleChooseBombInsertPosition(Guid userId, string payload)
    {
        if (State.PendingBombCardCode == null || !State.BombReinsertWindowEndsAt.HasValue)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NO_PENDING_BOMB\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingBombOwnerUserId.HasValue && State.PendingBombOwnerUserId.Value != userId)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NOT_BOMB_OWNER\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryGetInsertPosition(payload, out var pos))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"INVALID_INSERT_POSITION\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (pos < 0 || pos > State.DrawPile.Count)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"INVALID_INSERT_POSITION\",\"userId\":\"{userId}\"}}");
            return;
        }

        State.DrawPile.Insert(pos, State.PendingBombCardCode);
        IncrementVersion("BombReinserted", $"{{\"userId\":\"{userId}\",\"position\":{pos}}}");
        State.PendingBombCardCode = null;
        State.BombReinsertWindowEndsAt = null;
        State.PendingBombOwnerUserId = null;
        if (!IsBombResolutionPendingFor(userId))
        {
            CompleteCurrentTurnAfterDrawResolution(userId);
        }
    }

    private void MarkReconnect(Guid userId)
    {
        var idx = State.Players.FindIndex(p => p.UserId == userId);
        if (idx < 0)
        {
            return;
        }

        State.Players[idx] = State.Players[idx] with
        {
            Connected = true,
            PendingReconnectUntil = null
        };
        IncrementVersion("ReconnectAck", $"{{\"userId\":\"{userId}\"}}");
    }

    private void EliminatePlayer(Guid userId, string reason)
    {
        var idx = State.Players.FindIndex(p => p.UserId == userId);
        if (idx < 0)
        {
            return;
        }

        State.Players[idx] = State.Players[idx] with
        {
            LifeState = PlayerLifeState.Eliminated,
            PendingDrawCount = 0
        };
        if (!State.EliminationOrder.Contains(userId))
        {
            State.EliminationOrder.Add(userId);
        }

        if (State.PendingDefuseUserId == userId)
        {
            State.PendingDefuseUserId = null;
            State.DefuseWindowEndsAt = null;
        }

        if (State.PendingBombOwnerUserId == userId)
        {
            State.PendingBombOwnerUserId = null;
            State.BombReinsertWindowEndsAt = null;
            State.PendingBombCardCode = null;
        }

        IncrementVersion("PlayerEliminated", $"{{\"userId\":\"{userId}\",\"reason\":\"{reason}\"}}");
        CheckMatchFinished();

        if (State.Phase == MatchPhase.Playing && GetCurrentTurnUserId() == userId)
        {
            ScheduleTurnAdvance();
        }
    }

    private void CheckMatchFinished()
    {
        if (State.Phase == MatchPhase.Finished)
        {
            return;
        }

        var alive = State.Players.Where(p => p.LifeState == PlayerLifeState.Alive).ToList();
        if (alive.Count != 1)
        {
            return;
        }

        State.Phase = MatchPhase.Finished;
        State.WinnerUserId = alive[0].UserId;
        IncrementVersion("MatchFinished", $"{{\"winnerUserId\":\"{alive[0].UserId}\"}}");
    }

    private void InsertBombAtRandom(string bombCode)
    {
        var position = _random.Next(State.DrawPile.Count + 1);
        State.DrawPile.Insert(position, bombCode);
    }

    private bool HasCardInHand(Guid userId, string cardCode)
    {
        var idx = State.Players.FindIndex(p => p.UserId == userId);
        return idx >= 0 && (State.Players[idx].Hand ?? []).Contains(cardCode);
    }

    private bool TryRemoveCardFromHand(Guid userId, string cardCode)
    {
        var idx = State.Players.FindIndex(p => p.UserId == userId);
        if (idx < 0)
        {
            return false;
        }

        var hand = State.Players[idx].Hand ?? [];
        if (!hand.Remove(cardCode))
        {
            return false;
        }

        State.Players[idx] = State.Players[idx] with { Hand = hand };
        State.DiscardPile.Add(cardCode);
        return true;
    }

    private static bool TryGetInsertPosition(string payload, out int position)
    {
        position = -1;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty("position", out var pos))
            {
                return false;
            }

            position = pos.GetInt32();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void EmitTopCardsPeek(Guid userId, string cardCode)
    {
        var count = cardCode == "AlterTheFuture5" ? 5 : 3;
        var peek = State.DrawPile.Take(count).ToList();
        var payload = JsonSerializer.Serialize(new { userId, cardCode, cards = peek });
        IncrementVersion("FuturePeeked", payload);
    }

    private bool TryApplyUniversalCombo(Guid userId, int playerIndex, List<string> hand, string cardCode, int comboSize, string payload)
    {
        if (comboSize is not (2 or 3 or 5)) return false;

        var updatedHand = new List<string>(hand);
        var consumedCards = new List<string>(comboSize);
        if (comboSize == 5)
        {
            List<string> cardCodes;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (!doc.RootElement.TryGetProperty("cardCodes", out var codesElem) || codesElem.ValueKind != JsonValueKind.Array)
                {
                    IncrementVersion("ActionRejected", $"{{\"reason\":\"COMBO5_REQUIRES_CARDCODES_ARRAY\",\"userId\":\"{userId}\"}}");
                    return false;
                }
                cardCodes = codesElem.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => x != null)
                    .Cast<string>()
                    .ToList();
            }
            catch
            {
                IncrementVersion("ActionRejected", $"{{\"reason\":\"INVALID_JSON_PAYLOAD\",\"userId\":\"{userId}\"}}");
                return false;
            }

            if (cardCodes.Count != 5 || cardCodes.Distinct().Count() != 5)
            {
                IncrementVersion("ActionRejected", $"{{\"reason\":\"COMBO5_REQUIRES_5_DISTINCT_CARDS\",\"userId\":\"{userId}\"}}");
                return false;
            }

            foreach (var code in cardCodes)
            {
                if (!updatedHand.Remove(code))
                {
                    IncrementVersion("ActionRejected", $"{{\"reason\":\"CARD_NOT_OWNED\",\"userId\":\"{userId}\",\"cardCode\":\"{code}\"}}");
                    return false;
                }
                consumedCards.Add(code);
            }
        }
        else // Combo 2 or 3
        {
            var count = updatedHand.Count(c => c == cardCode);
            if (count < comboSize)
            {
                IncrementVersion("ActionRejected", $"{{\"reason\":\"INSUFFICIENT_COMBO_CARDS\",\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\",\"required\":{comboSize}}}");
                return false;
            }

            for (var i = 0; i < comboSize; i++)
            {
                updatedHand.Remove(cardCode);
                consumedCards.Add(cardCode);
            }
        }

        State.DiscardPile.AddRange(consumedCards);
        State.Players[playerIndex] = State.Players[playerIndex] with { Hand = updatedHand };
        var comboCode = $"Combo{comboSize}";
        IncrementVersion("ComboPlayed", JsonSerializer.Serialize(new
        {
            userId,
            comboSize,
            cardCode,
            comboCode,
            cardCodes = consumedCards
        }));
        ApplyCardEffect(userId, comboCode, payload);
        return true;
    }

    private void HandleBeginInteraction(Guid userId)
    {
        if (State.Phase != MatchPhase.Playing ||
            GetCurrentTurnUserId() != userId ||
            !IsAlivePlayer(userId) ||
            State.TurnAdvanceAt.HasValue ||
            State.PendingReactionAction != null ||
            State.PendingDefuseUserId.HasValue ||
            State.PendingBombCardCode != null ||
            State.PendingFavorTargetId.HasValue ||
            State.InteractionResetTurnCounter == State.TurnCounter)
        {
            return;
        }

        State.InteractionResetTurnCounter = State.TurnCounter;
        RefreshCurrentTurn(userId);
    }

    private void RefreshCurrentTurn(Guid userId)
    {
        if (State.Phase != MatchPhase.Playing ||
            State.TurnAdvanceAt.HasValue ||
            GetCurrentTurnUserId() != userId ||
            !IsAlivePlayer(userId))
        {
            return;
        }

        var player = State.Players.First(x => x.UserId == userId);
        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        IncrementVersion("TurnContinues", CreateTurnContinuesPayload(userId, Math.Max(1, player.PendingDrawCount)));
    }

    private static bool IsCatCard(string cardCode)
    {
        return cardCode is "Cat1" or "Cat2" or "Cat3" or "Cat4" or "Cat5" or "FeralCat";
    }

    private void ResolveTwoOfKindCombo(Guid userId, string payload)
    {
        var targetUserId = ResolveTargetUser(userId, payload);
        if (!targetUserId.HasValue)
        {
            return;
        }

        var targetIndex = State.Players.FindIndex(player => player.UserId == targetUserId.Value);
        var sourceIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (targetIndex < 0 || sourceIndex < 0)
        {
            return;
        }

        var targetHand = State.Players[targetIndex].Hand ?? [];
        if (targetHand.Count == 0)
        {
            return;
        }

        var stealIndex = _random.Next(targetHand.Count);
        var stolen = targetHand[stealIndex];
        targetHand.RemoveAt(stealIndex);
        State.Players[targetIndex] = State.Players[targetIndex] with { Hand = targetHand };

        var sourceHand = State.Players[sourceIndex].Hand ?? [];
        sourceHand.Add(stolen);
        State.Players[sourceIndex] = State.Players[sourceIndex] with { Hand = sourceHand };
        IncrementVersion("CatComboTwoResolved", $"{{\"from\":\"{targetUserId.Value}\",\"to\":\"{userId}\",\"cardCode\":\"{stolen}\"}}");
    }

    private void ResolveThreeOfKindCombo(Guid userId, string payload)
    {
        var targetUserId = ResolveTargetUser(userId, payload);
        if (!targetUserId.HasValue)
        {
            return;
        }

        if (!TryGetStringProperty(payload, "requestedCardCode", out var requestedCardCode))
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"MISSING_REQUESTED_CARD\",\"userId\":\"{userId}\"}}");
            return;
        }

        var targetIndex = State.Players.FindIndex(player => player.UserId == targetUserId.Value);
        var sourceIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (targetIndex < 0 || sourceIndex < 0)
        {
            return;
        }

        var targetHand = State.Players[targetIndex].Hand ?? [];
        if (!targetHand.Remove(requestedCardCode))
        {
            IncrementVersion("CatComboThreeMiss", $"{{\"from\":\"{targetUserId.Value}\",\"to\":\"{userId}\",\"requestedCardCode\":\"{requestedCardCode}\"}}");
            return;
        }

        State.Players[targetIndex] = State.Players[targetIndex] with { Hand = targetHand };
        var sourceHand = State.Players[sourceIndex].Hand ?? [];
        sourceHand.Add(requestedCardCode);
        State.Players[sourceIndex] = State.Players[sourceIndex] with { Hand = sourceHand };
        IncrementVersion("CatComboThreeResolved", $"{{\"from\":\"{targetUserId.Value}\",\"to\":\"{userId}\",\"requestedCardCode\":\"{requestedCardCode}\"}}");
    }

    private void ResolveFiveOfKindCombo(Guid userId, string payload)
    {
        if (State.DiscardPile.Count == 0)
        {
            return;
        }

        var sourceIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (sourceIndex < 0)
        {
            return;
        }

        var discardCardCode = TryGetStringProperty(payload, "discardCardCode", out var requestedDiscard)
            ? requestedDiscard
            : State.DiscardPile[^1];
        var discardIndex = State.DiscardPile.FindLastIndex(card => card == discardCardCode);
        if (discardIndex < 0)
        {
            return;
        }

        State.DiscardPile.RemoveAt(discardIndex);
        var hand = State.Players[sourceIndex].Hand ?? [];
        hand.Add(discardCardCode);
        State.Players[sourceIndex] = State.Players[sourceIndex] with { Hand = hand };
        IncrementVersion("CatComboFiveResolved", $"{{\"userId\":\"{userId}\",\"discardCardCode\":\"{discardCardCode}\"}}");
    }

    private void ApplyFavor(Guid userId, string payload)
    {
        var target = ResolveTargetUser(userId, payload);
        if (target == null) return;

        var targetIdx = State.Players.FindIndex(p => p.UserId == target.Value);
        if (targetIdx < 0) return;

        var targetHand = State.Players[targetIdx].Hand ?? [];
        if (targetHand.Count == 0)
        {
            IncrementVersion("FavorTargetEmpty", $"{{\"from\":\"{userId}\",\"target\":\"{target.Value}\"}}");
            return;
        }

        // Open favor window: Bob must pick a card to give
        State.PendingFavorRequesterId = userId;
        State.PendingFavorTargetId = target.Value;
        State.FavorWindowEndsAt = DateTime.UtcNow.AddSeconds(State.FavorDecisionSeconds);
        IncrementVersion("FavorWindowOpened", JsonSerializer.Serialize(new
        {
            requesterId = userId,
            targetId = target.Value,
            favorWindowEndsAt = State.FavorWindowEndsAt
        }));
    }

    private void HandleChooseFavorCard(Guid userId, string payload)
    {
        if (!State.PendingFavorTargetId.HasValue || State.PendingFavorTargetId.Value != userId)
        {
            IncrementVersion("ActionRejected", $"{{\"reason\":\"NO_PENDING_FAVOR\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryGetStringProperty(payload, "cardCode", out var cardCode) ||
            !HasCardInHand(userId, cardCode))
        {
            IncrementVersion("ActionRejected", JsonSerializer.Serialize(new
            {
                reason = "INVALID_FAVOR_CARD",
                userId
            }));
            return;
        }

        ExecuteFavorTransfer(State.PendingFavorRequesterId!.Value, userId, cardCode);
    }

    private void ExecuteFavorTransfer(Guid requesterId, Guid targetId, string? requestedCardCode)
    {
        State.PendingFavorRequesterId = null;
        State.PendingFavorTargetId = null;
        State.FavorWindowEndsAt = null;

        var targetIndex = State.Players.FindIndex(p => p.UserId == targetId);
        var sourceIndex = State.Players.FindIndex(p => p.UserId == requesterId);
        if (targetIndex < 0 || sourceIndex < 0) return;

        var targetHand = State.Players[targetIndex].Hand ?? [];
        if (targetHand.Count == 0) return;

        string card;
        if (requestedCardCode != null && targetHand.Contains(requestedCardCode))
        {
            card = requestedCardCode;
        }
        else
        {
            card = targetHand[_random.Next(targetHand.Count)];
        }

        targetHand.Remove(card);
        State.Players[targetIndex] = State.Players[targetIndex] with { Hand = targetHand };

        var sourceHand = State.Players[sourceIndex].Hand ?? [];
        sourceHand.Add(card);
        State.Players[sourceIndex] = State.Players[sourceIndex] with { Hand = sourceHand };
        IncrementVersion("FavorResolved", $"{{\"from\":\"{targetId}\",\"to\":\"{requesterId}\",\"cardCode\":\"{card}\"}}");
        RefreshCurrentTurn(requesterId);
    }

    private void ApplyBury(Guid userId)
    {
        if (State.DiscardPile.Count == 0)
        {
            return;
        }

        var latest = State.DiscardPile[^1];
        if (latest is "ExplodingKitten" or "ImplodingKitten")
        {
            InsertBombAtRandom(latest);
            State.DiscardPile.RemoveAt(State.DiscardPile.Count - 1);
            IncrementVersion("BuryResolved", $"{{\"userId\":\"{userId}\",\"card\":\"{latest}\"}}");
        }
    }

    private void ApplyIllTakeThat(Guid userId, string payload)
    {
        var target = ResolveTargetUser(userId, payload);
        if (target == null)
        {
            return;
        }

        IncrementVersion("IllTakeThatMarked", $"{{\"owner\":\"{userId}\",\"target\":\"{target.Value}\"}}");
    }

    private void SetTowerMask(Guid userId, bool active)
    {
        var idx = State.Players.FindIndex(p => p.UserId == userId);
        if (idx < 0)
        {
            return;
        }

        State.Players[idx] = State.Players[idx] with { HasTowerMask = active };
        IncrementVersion("TowerMaskUpdated", $"{{\"userId\":\"{userId}\",\"active\":{active.ToString().ToLowerInvariant()}}}");
    }

    private void ApplyMark(Guid userId, string payload)
    {
        var target = ResolveTargetUser(userId, payload);
        if (target == null)
        {
            return;
        }

        var idx = State.Players.FindIndex(p => p.UserId == target.Value);
        if (idx < 0)
        {
            return;
        }

        State.Players[idx] = State.Players[idx] with { IsMarked = true };
        IncrementVersion("MarkApplied", $"{{\"from\":\"{userId}\",\"target\":\"{target.Value}\"}}");
    }

    private void ApplyCurseOfCatButt(Guid userId, string payload)
    {
        var target = ResolveTargetUser(userId, payload);
        if (target == null)
        {
            return;
        }

        var idx = State.Players.FindIndex(p => p.UserId == target.Value);
        if (idx < 0)
        {
            return;
        }

        State.Players[idx] = State.Players[idx] with { IsBlind = true };
        IncrementVersion("CatButtCurseApplied", $"{{\"from\":\"{userId}\",\"target\":\"{target.Value}\"}}");
    }

    private void SwapTopAndBottom()
    {
        if (State.DrawPile.Count < 2)
        {
            return;
        }

        var top = State.DrawPile[0];
        var bottomIndex = State.DrawPile.Count - 1;
        State.DrawPile[0] = State.DrawPile[bottomIndex];
        State.DrawPile[bottomIndex] = top;
        IncrementVersion("SwapTopBottom", "{}");
    }

    private void ApplyGarbageCollection()
    {
        foreach (var i in Enumerable.Range(0, State.Players.Count))
        {
            var hand = State.Players[i].Hand ?? [];
            if (hand.Count == 0)
            {
                continue;
            }

            var idx = _random.Next(hand.Count);
            var card = hand[idx];
            hand.RemoveAt(idx);
            State.DrawPile.Add(card);
            State.Players[i] = State.Players[i] with { Hand = hand };
        }

        Shuffle(State.DrawPile);
        IncrementVersion("GarbageCollectionResolved", "{}");
    }

    private void ApplyCatomicBomb()
    {
        var bombs = State.DrawPile.Where(c => c is "ExplodingKitten" or "ImplodingKitten").ToList();
        State.DrawPile = State.DrawPile.Where(c => c is not ("ExplodingKitten" or "ImplodingKitten")).ToList();
        Shuffle(State.DrawPile);
        State.DrawPile.InsertRange(0, bombs);
        IncrementVersion("CatomicBombResolved", $"{{\"bombCount\":{bombs.Count}}}");
    }

    private void ApplyBarkingKitten(Guid userId, string payload)
    {
        var target = ResolveTargetUser(userId, payload);
        if (target == null)
        {
            return;
        }

        var targetIdx = State.Players.FindIndex(p => p.UserId == target.Value);
        var sourceIdx = State.Players.FindIndex(p => p.UserId == userId);
        if (targetIdx < 0 || sourceIdx < 0)
        {
            return;
        }

        var targetHand = State.Players[targetIdx].Hand ?? [];
        var transferCount = targetHand.Count / 2;
        var sourceHand = State.Players[sourceIdx].Hand ?? [];
        for (var i = 0; i < transferCount && targetHand.Count > 0; i++)
        {
            var cardIdx = _random.Next(targetHand.Count);
            var card = targetHand[cardIdx];
            targetHand.RemoveAt(cardIdx);
            sourceHand.Add(card);
        }

        State.Players[targetIdx] = State.Players[targetIdx] with { Hand = targetHand };
        State.Players[sourceIdx] = State.Players[sourceIdx] with { Hand = sourceHand };
        IncrementVersion("BarkingKittenResolved", $"{{\"from\":\"{target.Value}\",\"to\":\"{userId}\"}}");
    }

    private Guid? GetRandomAliveTarget(Guid actor)
    {
        var targets = State.Players
            .Where(p => p.UserId != actor && p.LifeState == PlayerLifeState.Alive)
            .Select(p => p.UserId)
            .ToList();
        if (targets.Count == 0)
        {
            return null;
        }

        return targets[_random.Next(targets.Count)];
    }
}
