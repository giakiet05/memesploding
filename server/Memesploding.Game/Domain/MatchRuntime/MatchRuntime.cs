using System.Threading.Channels;
using Memesploding.Game.Domain.StateMachine;
using System.Text.Json;

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

        if (State.Phase == MatchPhase.Playing && State.TurnEndsAt.HasValue && State.TurnEndsAt.Value <= DateTime.UtcNow)
        {
            var currentTurnUserId = GetCurrentTurnUserId();
            if (currentTurnUserId.HasValue && State.PendingReactionAction == null && !State.PendingDefuseUserId.HasValue && State.PendingBombCardCode == null)
            {
                DrawCardForPlayer(currentTurnUserId.Value, fromBottom: false);
                IncrementVersion("turn_timeout_auto_draw", $"{{\"userId\":\"{currentTurnUserId.Value}\"}}");
                ConsumePendingDraw(currentTurnUserId.Value);
                if (!IsBombResolutionPendingFor(currentTurnUserId.Value))
                {
                    CompleteCurrentTurnAfterDrawResolution(currentTurnUserId.Value);
                }
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
                "reaction_window_closed",
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
            IncrementVersion("bomb_reinsert_auto", "{}");
            if (ownerUserId.HasValue && !IsBombResolutionPendingFor(ownerUserId.Value))
            {
                CompleteCurrentTurnAfterDrawResolution(ownerUserId.Value);
            }
        }

        ProcessReconnectTimeouts();
        CheckMatchFinished();
    }

    public void MarkStarted()
    {
        State.Phase = MatchPhase.Playing;
        State.StartedAt = DateTime.UtcNow;
        StartTurnLoop();
        IncrementVersion("match_started", "{}");
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
        IncrementVersion("turn_started", $"{{\"turnIndex\":{State.TurnIndex}}}");
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
            default:
                IncrementVersion("unknown_command", $"{{\"name\":\"{command.Name}\"}}");
                break;
        }
    }

    private void HandleDrawCardCommand(Guid userId, bool fromBottom)
    {
        if (State.Phase != MatchPhase.Playing)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"invalid_phase\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!IsAlivePlayer(userId))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"player_eliminated\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingReactionAction != null || State.ReactionResolveAt.HasValue)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"reaction_in_progress\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingDefuseUserId.HasValue || State.PendingBombCardCode != null)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"bomb_resolution_pending\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (GetCurrentTurnUserId() != userId)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"not_turn\",\"userId\":\"{userId}\"}}");
            return;
        }

        DrawCardForPlayer(userId, fromBottom);
        ConsumePendingDraw(userId);
        if (!IsBombResolutionPendingFor(userId))
        {
            CompleteCurrentTurnAfterDrawResolution(userId);
        }
    }

    private void HandlePlayCardCommand(Guid userId, string payload)
    {
        if (State.Phase != MatchPhase.Playing)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"invalid_phase\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!IsAlivePlayer(userId))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"player_eliminated\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingReactionAction != null || State.ReactionResolveAt.HasValue)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"reaction_in_progress\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingDefuseUserId.HasValue || State.PendingBombCardCode != null)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"bomb_resolution_pending\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (GetCurrentTurnUserId() != userId)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"not_turn\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryGetCardCode(payload, out var cardCode))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"invalid_payload\",\"userId\":\"{userId}\"}}");
            return;
        }

        var playerIndex = State.Players.FindIndex(p => p.UserId == userId);
        if (playerIndex < 0)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"player_not_found\",\"userId\":\"{userId}\"}}");
            return;
        }

        var hand = State.Players[playerIndex].Hand ?? [];
        if (cardCode == "Nope")
        {
            HandleNopeCommand(userId);
            return;
        }

        if (IsCatCard(cardCode) && TryGetComboSize(payload, out var comboSize))
        {
            if (!TryApplyCatCombo(userId, playerIndex, hand, cardCode, comboSize, payload))
            {
                return;
            }

            return;
        }

        if (!hand.Remove(cardCode))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"card_not_owned\",\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\"}}");
            return;
        }

        State.Players[playerIndex] = State.Players[playerIndex] with { Hand = hand };
        State.DiscardPile.Add(cardCode);
        IncrementVersion("card_played", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\"}}");

        ApplyCardEffect(userId, cardCode, payload);
    }

    private void HandleNopeCommand(Guid userId)
    {
        if (!State.ReactionWindowEndsAt.HasValue || State.ReactionWindowEndsAt.Value < DateTime.UtcNow || State.PendingReactionAction == null)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"no_reaction_window\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryRemoveCardFromHand(userId, "Nope"))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"card_not_owned\",\"userId\":\"{userId}\",\"cardCode\":\"Nope\"}}");
            return;
        }

        State.PendingNopeCount++;
        IncrementVersion(
            "nope_played",
            $"{{\"userId\":\"{userId}\",\"cardCode\":\"{State.PendingReactionAction}\",\"nopeCount\":{State.PendingNopeCount}}}"
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
                AdvanceTurn();
                break;
            case "Reverse":
                State.TurnDirection *= -1;
                AdvanceTurn();
                break;
            case "Shuffle":
                Shuffle(State.DrawPile);
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
                IncrementVersion("streaking_kitten_active", $"{{\"userId\":\"{userId}\"}}");
                break;
            case "FeralCat":
                IncrementVersion("feral_cat_played", $"{{\"userId\":\"{userId}\"}}");
                break;
            default:
                IncrementVersion("card_effect_unhandled", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\"}}");
                break;
        }
    }

    private void OpenReactionWindow(Guid userId, string actionCardCode, string payload)
    {
        if (State.PendingReactionAction != null)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"reaction_in_progress\",\"userId\":\"{userId}\"}}");
            return;
        }

        State.PendingReactionUserId = userId;
        State.PendingReactionAction = actionCardCode;
        State.PendingReactionPayload = payload;
        State.PendingNopeCount = 0;
        State.ReactionResolveAt = null;
        State.ReactionWindowEndsAt = DateTime.UtcNow.AddSeconds(State.NopeWindowSeconds);
        IncrementVersion("reaction_window_opened", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{actionCardCode}\"}}");
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
        State.ReactionResolveAt = null;
        State.PendingNopeCount = 0;

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
            return;
        }

        ResolveCardEffect(actorUserId.Value, actionCardCode, actionPayload);
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
            "DrawFromBottom";
    }

    private void ApplyAttack(Guid userId, string cardCode, string payload)
    {
        var targetUserId = ResolveTargetUser(userId, payload);
        if (!targetUserId.HasValue)
        {
            return;
        }

        var targetIndex = State.Players.FindIndex(player => player.UserId == targetUserId.Value && player.LifeState == PlayerLifeState.Alive);
        if (targetIndex < 0)
        {
            return;
        }

        var currentPending = Math.Max(1, State.Players[targetIndex].PendingDrawCount);
        var added = cardCode == "PersonalAttack" ? 3 : 2;
        State.Players[targetIndex] = State.Players[targetIndex] with { PendingDrawCount = currentPending + added };
        IncrementVersion("attack_applied", $"{{\"from\":\"{userId}\",\"to\":\"{targetUserId.Value}\",\"added\":{added}}}");
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

        if (pending <= 0)
        {
            State.Players[playerIndex] = State.Players[playerIndex] with { PendingDrawCount = 1 };
            AdvanceTurn();
            return;
        }

        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        IncrementVersion("turn_continues", $"{{\"userId\":\"{userId}\",\"pendingDrawCount\":{pending}}}");
    }

    private void ConsumePendingDraw(Guid userId)
    {
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
        var playerIndex = State.Players.FindIndex(player => player.UserId == userId);
        if (playerIndex < 0)
        {
            return;
        }

        var pending = State.Players[playerIndex].PendingDrawCount;
        if (pending <= 0)
        {
            State.Players[playerIndex] = State.Players[playerIndex] with { PendingDrawCount = 1 };
            AdvanceTurn();
            return;
        }

        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        IncrementVersion("turn_continues", $"{{\"userId\":\"{userId}\",\"pendingDrawCount\":{pending}}}");
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

    private void DrawCardForPlayer(Guid userId, bool fromBottom)
    {
        if (State.DrawPile.Count == 0)
        {
            IncrementVersion("draw_pile_empty", "{}");
            return;
        }

        var card = fromBottom ? DrawFromBottomInternal() : DrawFromTopInternal();

        var playerIndex = State.Players.FindIndex(p => p.UserId == userId);
        if (playerIndex < 0)
        {
            return;
        }

        var hand = State.Players[playerIndex].Hand ?? [];
        IncrementVersion("card_drawn", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{card}\"}}");

        if (card == "ExplodingKitten")
        {
            if (HasCardInHand(userId, "StreakingKitten"))
            {
                hand.Add(card);
                IncrementVersion("streaking_holds_bomb", $"{{\"userId\":\"{userId}\"}}");
            }
            else
            {
                State.PendingDefuseUserId = userId;
                State.DefuseWindowEndsAt = DateTime.UtcNow.AddSeconds(State.DefuseDecisionSeconds);
                State.PendingBombOwnerUserId = userId;
                State.PendingBombCardCode = "ExplodingKitten";
                IncrementVersion("explosion_triggered", $"{{\"userId\":\"{userId}\"}}");
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
                IncrementVersion("imploding_reinsert_required", $"{{\"userId\":\"{userId}\"}}");
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

    private void AdvanceTurn()
    {
        if (State.Players.Count == 0)
        {
            return;
        }

        State.TurnIndex = GetNextAliveTurnIndex();
        if (State.TurnIndex < 0)
        {
            State.Phase = MatchPhase.Finished;
            IncrementVersion("match_finished", "{}");
            return;
        }

        State.TurnEndsAt = DateTime.UtcNow.AddSeconds(State.TurnTimerSeconds);
        State.TurnCounter++;
        IncrementVersion("turn_changed", $"{{\"turnIndex\":{State.TurnIndex}}}");
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

    private void HandleUseDefuse(Guid userId)
    {
        if (State.PendingDefuseUserId != userId || State.PendingBombCardCode == null)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"no_pending_defuse\",\"userId\":\"{userId}\"}}");
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
        IncrementVersion("defuse_used", $"{{\"userId\":\"{userId}\"}}");
    }

    private void HandleChooseBombInsertPosition(Guid userId, string payload)
    {
        if (State.PendingBombCardCode == null || !State.BombReinsertWindowEndsAt.HasValue)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"no_pending_bomb\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (State.PendingBombOwnerUserId.HasValue && State.PendingBombOwnerUserId.Value != userId)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"not_bomb_owner\",\"userId\":\"{userId}\"}}");
            return;
        }

        if (!TryGetInsertPosition(payload, out var pos))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"invalid_insert_position\",\"userId\":\"{userId}\"}}");
            return;
        }

        pos = Math.Clamp(pos, 0, State.DrawPile.Count);
        State.DrawPile.Insert(pos, State.PendingBombCardCode);
        IncrementVersion("bomb_reinserted", $"{{\"userId\":\"{userId}\",\"position\":{pos}}}");
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
        IncrementVersion("reconnect_ack", $"{{\"userId\":\"{userId}\"}}");
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

        IncrementVersion("player_eliminated", $"{{\"userId\":\"{userId}\",\"reason\":\"{reason}\"}}");
        CheckMatchFinished();

        if (State.Phase == MatchPhase.Playing && GetCurrentTurnUserId() == userId)
        {
            AdvanceTurn();
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
        IncrementVersion("match_finished", $"{{\"winnerUserId\":\"{alive[0].UserId}\"}}");
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
        IncrementVersion("future_peeked", payload);
    }

    private bool TryApplyCatCombo(Guid userId, int playerIndex, List<string> hand, string cardCode, int comboSize, string payload)
    {
        if (comboSize is not (2 or 3 or 5))
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"invalid_combo_size\",\"userId\":\"{userId}\",\"comboSize\":{comboSize}}}");
            return false;
        }

        var sameCards = hand.Count(card => card == cardCode);
        if (sameCards < comboSize)
        {
            IncrementVersion("action_rejected", $"{{\"reason\":\"insufficient_combo_cards\",\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\",\"required\":{comboSize}}}");
            return false;
        }

        var removed = 0;
        for (var i = hand.Count - 1; i >= 0 && removed < comboSize; i--)
        {
            if (hand[i] != cardCode)
            {
                continue;
            }

            hand.RemoveAt(i);
            State.DiscardPile.Add(cardCode);
            removed++;
        }

        State.Players[playerIndex] = State.Players[playerIndex] with { Hand = hand };
        IncrementVersion("cat_combo_played", $"{{\"userId\":\"{userId}\",\"cardCode\":\"{cardCode}\",\"comboSize\":{comboSize}}}");

        switch (comboSize)
        {
            case 2:
                ResolveTwoOfKindCombo(userId, payload);
                break;
            case 3:
                ResolveThreeOfKindCombo(userId, payload);
                break;
            case 5:
                ResolveFiveOfKindCombo(userId, payload);
                break;
        }

        return true;
    }

    private static bool IsCatCard(string cardCode)
    {
        return cardCode is "Cat1" or "Cat2" or "Cat3" or "Cat4" or "Cat5";
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
        IncrementVersion("cat_combo_two_resolved", $"{{\"from\":\"{targetUserId.Value}\",\"to\":\"{userId}\"}}");
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
            IncrementVersion("action_rejected", $"{{\"reason\":\"missing_requested_card\",\"userId\":\"{userId}\"}}");
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
            IncrementVersion("cat_combo_three_miss", $"{{\"from\":\"{targetUserId.Value}\",\"to\":\"{userId}\",\"requestedCardCode\":\"{requestedCardCode}\"}}");
            return;
        }

        State.Players[targetIndex] = State.Players[targetIndex] with { Hand = targetHand };
        var sourceHand = State.Players[sourceIndex].Hand ?? [];
        sourceHand.Add(requestedCardCode);
        State.Players[sourceIndex] = State.Players[sourceIndex] with { Hand = sourceHand };
        IncrementVersion("cat_combo_three_resolved", $"{{\"from\":\"{targetUserId.Value}\",\"to\":\"{userId}\",\"requestedCardCode\":\"{requestedCardCode}\"}}");
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
        IncrementVersion("cat_combo_five_resolved", $"{{\"userId\":\"{userId}\",\"discardCardCode\":\"{discardCardCode}\"}}");
    }

    private void ApplyFavor(Guid userId, string payload)
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
        if (targetHand.Count == 0)
        {
            return;
        }

        var cardIdx = _random.Next(targetHand.Count);
        var card = targetHand[cardIdx];
        targetHand.RemoveAt(cardIdx);
        State.Players[targetIdx] = State.Players[targetIdx] with { Hand = targetHand };

        var sourceHand = State.Players[sourceIdx].Hand ?? [];
        sourceHand.Add(card);
        State.Players[sourceIdx] = State.Players[sourceIdx] with { Hand = sourceHand };
        IncrementVersion("favor_resolved", $"{{\"from\":\"{target.Value}\",\"to\":\"{userId}\"}}");
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
            IncrementVersion("bury_resolved", $"{{\"userId\":\"{userId}\",\"card\":\"{latest}\"}}");
        }
    }

    private void ApplyIllTakeThat(Guid userId, string payload)
    {
        var target = ResolveTargetUser(userId, payload);
        if (target == null)
        {
            return;
        }

        IncrementVersion("ill_take_that_marked", $"{{\"owner\":\"{userId}\",\"target\":\"{target.Value}\"}}");
    }

    private void SetTowerMask(Guid userId, bool active)
    {
        var idx = State.Players.FindIndex(p => p.UserId == userId);
        if (idx < 0)
        {
            return;
        }

        State.Players[idx] = State.Players[idx] with { HasTowerMask = active };
        IncrementVersion("tower_mask_updated", $"{{\"userId\":\"{userId}\",\"active\":{active.ToString().ToLowerInvariant()}}}");
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
        IncrementVersion("mark_applied", $"{{\"from\":\"{userId}\",\"target\":\"{target.Value}\"}}");
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
        IncrementVersion("cat_butt_curse_applied", $"{{\"from\":\"{userId}\",\"target\":\"{target.Value}\"}}");
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
        IncrementVersion("swap_top_bottom", "{}");
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
        IncrementVersion("garbage_collection_resolved", "{}");
    }

    private void ApplyCatomicBomb()
    {
        var bombs = State.DrawPile.Where(c => c is "ExplodingKitten" or "ImplodingKitten").ToList();
        State.DrawPile = State.DrawPile.Where(c => c is not ("ExplodingKitten" or "ImplodingKitten")).ToList();
        Shuffle(State.DrawPile);
        State.DrawPile.InsertRange(0, bombs);
        IncrementVersion("catomic_bomb_resolved", $"{{\"bombCount\":{bombs.Count}}}");
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
        IncrementVersion("barking_kitten_resolved", $"{{\"from\":\"{target.Value}\",\"to\":\"{userId}\"}}");
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
