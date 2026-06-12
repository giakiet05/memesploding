using System.Text.Json;
using Memesploding.Game.Domain.MatchRuntime;
using Memesploding.Game.Domain.StateMachine;
using Xunit;

namespace Memesploding.Game.Tests;

public class MatchRuntimeInteractionTests
{
    [Fact]
    public void ComboFive_DoesNotMutateHand_WhenOneCardIsNotOwned()
    {
        var (runtime, actor, _) = CreateRuntime(
            ["Attack", "Skip", "Favor", "Shuffle", "SeeTheFuture"],
            ["Nope"]);
        var originalHand = runtime.State.Players[0].Hand!.ToList();
        var originalDiscard = runtime.State.DiscardPile.ToList();

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new
        {
            cardCode = "Attack",
            comboSize = 5,
            cardCodes = new[] { "Attack", "Skip", "Favor", "Shuffle", "Cat1" },
            discardCardCode = "Nope"
        }));

        Assert.Equal(originalHand, runtime.State.Players[0].Hand);
        Assert.Equal(originalDiscard, runtime.State.DiscardPile);
        Assert.Equal("ActionRejected", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public async Task InvalidFavorChoice_KeepsFavorWindowOpen()
    {
        var (runtime, actor, target) = CreateRuntime(["Favor"], ["Attack"]);
        runtime.State.PendingFavorRequesterId = actor;
        runtime.State.PendingFavorTargetId = target;
        runtime.State.FavorWindowEndsAt = DateTime.UtcNow.AddSeconds(10);

        await runtime.EnqueueAsync(new RuntimeCommand(
            "ChooseFavorCard",
            target,
            JsonSerializer.Serialize(new { cardCode = "Nope" })));
        await runtime.TickAsync();

        Assert.Equal(actor, runtime.State.PendingFavorRequesterId);
        Assert.Equal(target, runtime.State.PendingFavorTargetId);
        Assert.Equal(["Attack"], runtime.State.Players[1].Hand);
        Assert.Equal("ActionRejected", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public async Task InvalidBombPosition_DoesNotReinsertBomb()
    {
        var (runtime, actor, _) = CreateRuntime([], [], ["Attack", "Skip"]);
        runtime.State.PendingBombOwnerUserId = actor;
        runtime.State.PendingBombCardCode = "ExplodingKitten";
        runtime.State.BombReinsertWindowEndsAt = DateTime.UtcNow.AddSeconds(10);

        await runtime.EnqueueAsync(new RuntimeCommand(
            "ChooseBombInsertPosition",
            actor,
            JsonSerializer.Serialize(new { position = 3 })));
        await runtime.TickAsync();

        Assert.Equal(["Attack", "Skip"], runtime.State.DrawPile);
        Assert.Equal("ExplodingKitten", runtime.State.PendingBombCardCode);
        Assert.Equal("ActionRejected", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public async Task UsedDefuse_IsAddedToDiscardPile_ForComboFiveRecovery()
    {
        var (runtime, actor, _) = CreateRuntime(["Defuse"], []);
        runtime.State.PendingDefuseUserId = actor;
        runtime.State.PendingBombOwnerUserId = actor;
        runtime.State.PendingBombCardCode = "ExplodingKitten";
        runtime.State.DefuseWindowEndsAt = DateTime.UtcNow.AddSeconds(10);

        await runtime.EnqueueAsync(new RuntimeCommand("UseDefuse", actor, "{}"));
        await runtime.TickAsync();

        Assert.Empty(runtime.State.Players[0].Hand!);
        Assert.Equal(["Defuse"], runtime.State.DiscardPile);
        Assert.Equal("DefuseUsed", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public void CatCard_CannotBePlayedOutsideCombo()
    {
        var (runtime, actor, _) = CreateRuntime(["Cat1"], []);

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new { cardCode = "Cat1" }));

        Assert.Equal(["Cat1"], runtime.State.Players[0].Hand);
        Assert.Empty(runtime.State.DiscardPile);
        Assert.Equal("ActionRejected", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public void ComboThree_RemovesAllPlayedCards_AndReportsExactDiscardCards()
    {
        var (runtime, actor, target) = CreateRuntime(["Cat1", "Cat1", "Cat1", "Attack"], []);

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new
        {
            cardCode = "Cat1",
            comboSize = 3,
            targetUserId = target,
            requestedCardCode = "Nope"
        }));

        Assert.Equal(["Attack"], runtime.State.Players[0].Hand);
        Assert.Equal(["Cat1", "Cat1", "Cat1"], runtime.State.DiscardPile);
        var comboEvent = runtime.State.EventLog.First(x => x.EventType == "ComboPlayed");
        using var payload = JsonDocument.Parse(comboEvent.Payload);
        Assert.Equal(3, payload.RootElement.GetProperty("cardCodes").GetArrayLength());
    }

    [Fact]
    public async Task NopedAction_ClearsReactionState_AndRefreshesActorsTurn()
    {
        var (runtime, actor, target) = CreateRuntime(["Attack"], ["Nope"]);
        var oldTurnEnd = DateTime.UtcNow.AddSeconds(1);
        runtime.State.TurnEndsAt = oldTurnEnd;

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new { cardCode = "Attack" }));
        runtime.HandlePlayCardCommand(target, JsonSerializer.Serialize(new { cardCode = "Nope" }));
        runtime.State.ReactionWindowEndsAt = DateTime.UtcNow.AddMilliseconds(-1);
        await runtime.TickAsync();
        runtime.State.ReactionResolveAt = DateTime.UtcNow.AddMilliseconds(-1);
        await runtime.TickAsync();

        Assert.Null(runtime.State.PendingReactionAction);
        Assert.Null(runtime.State.ReactionWindowEndsAt);
        Assert.True(runtime.State.TurnEndsAt > oldTurnEnd);
        Assert.Contains(runtime.State.EventLog, x => x.EventType == "action_noped");
        Assert.Equal("TurnContinues", runtime.State.EventLog[^1].EventType);
        Assert.Equal(["Attack", "Nope"], runtime.State.DiscardPile);
    }

    [Fact]
    public async Task BeginInteraction_RefreshesTurnOnlyOncePerTurn()
    {
        var (runtime, actor, _) = CreateRuntime(["Favor"], []);
        runtime.State.TurnEndsAt = DateTime.UtcNow.AddSeconds(1);

        await runtime.EnqueueAsync(new RuntimeCommand("BeginInteraction", actor, "{}"));
        await runtime.TickAsync();
        var firstReset = runtime.State.TurnEndsAt;

        runtime.State.TurnEndsAt = DateTime.UtcNow.AddSeconds(2);
        await runtime.EnqueueAsync(new RuntimeCommand("BeginInteraction", actor, "{}"));
        await runtime.TickAsync();

        Assert.Equal(runtime.State.TurnCounter, runtime.State.InteractionResetTurnCounter);
        Assert.NotNull(firstReset);
        Assert.True(firstReset > DateTime.UtcNow.AddSeconds(5));
        Assert.True(runtime.State.TurnEndsAt < firstReset);
    }

    [Fact]
    public void AcceptedPlay_RefreshesTurn_ButRejectedPlayDoesNot()
    {
        var (runtime, actor, _) = CreateRuntime(["StreakingKitten"], []);
        runtime.State.TurnEndsAt = DateTime.UtcNow.AddSeconds(1);

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new { cardCode = "StreakingKitten" }));
        var refreshedTurnEnd = runtime.State.TurnEndsAt;

        runtime.State.TurnEndsAt = DateTime.UtcNow.AddSeconds(2);
        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new { cardCode = "Attack" }));

        Assert.NotNull(refreshedTurnEnd);
        Assert.True(refreshedTurnEnd > DateTime.UtcNow.AddSeconds(5));
        Assert.True(runtime.State.TurnEndsAt < refreshedTurnEnd);
        Assert.Equal("ActionRejected", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public async Task ReactionWindow_PausesTurnUntilReactionResolves()
    {
        var (runtime, actor, _) = CreateRuntime(["Shuffle"], []);

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new { cardCode = "Shuffle" }));

        Assert.Null(runtime.State.TurnEndsAt);
        runtime.State.ReactionWindowEndsAt = DateTime.UtcNow.AddMilliseconds(-1);
        await runtime.TickAsync();
        Assert.Null(runtime.State.TurnEndsAt);

        runtime.State.ReactionResolveAt = DateTime.UtcNow.AddMilliseconds(-1);
        await runtime.TickAsync();

        Assert.NotNull(runtime.State.TurnEndsAt);
        Assert.True(runtime.State.TurnEndsAt > DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task EmptyDrawPile_DoesNotConsumePendingDrawOrAdvanceTurn()
    {
        var (runtime, actor, _) = CreateRuntime([], [], []);

        await runtime.EnqueueAsync(new RuntimeCommand("DrawCard", actor, "{}"));
        await runtime.TickAsync();

        Assert.Equal(1, runtime.State.Players[0].PendingDrawCount);
        Assert.Null(runtime.State.TurnAdvanceAt);
        Assert.Null(runtime.State.TurnEndsAt);
        Assert.Equal("DrawPileEmpty", runtime.State.EventLog[^1].EventType);
    }

    [Fact]
    public async Task CardDrawn_ReportsAuthoritativeRemainingDrawPileCount()
    {
        var (runtime, actor, _) = CreateRuntime([], [], ["Attack", "Skip"]);

        await runtime.EnqueueAsync(new RuntimeCommand("DrawCard", actor, "{}"));
        await runtime.TickAsync();

        var cardDrawn = runtime.State.EventLog.First(x => x.EventType == "CardDrawn");
        using var payload = JsonDocument.Parse(cardDrawn.Payload);
        Assert.Equal(1, payload.RootElement.GetProperty("drawPileCount").GetInt32());
        Assert.Single(runtime.State.DrawPile);
    }

    [Fact]
    public async Task SeeTheFuture_ReportsExactTopCardsInOrder()
    {
        var expected = new[] { "Attack", "Skip", "Defuse" };
        var (runtime, actor, _) = CreateRuntime(["SeeTheFuture"], [], expected.ToList());

        runtime.HandlePlayCardCommand(actor, JsonSerializer.Serialize(new { cardCode = "SeeTheFuture" }));
        runtime.State.ReactionWindowEndsAt = DateTime.UtcNow.AddMilliseconds(-1);
        await runtime.TickAsync();
        runtime.State.ReactionResolveAt = DateTime.UtcNow.AddMilliseconds(-1);
        await runtime.TickAsync();

        var peekEvent = runtime.State.EventLog.First(x => x.EventType == "FuturePeeked");
        using var payload = JsonDocument.Parse(peekEvent.Payload);
        Assert.Equal(expected, payload.RootElement.GetProperty("cards").EnumerateArray().Select(x => x.GetString()));
    }

    private static (MatchRuntime Runtime, Guid Actor, Guid Target) CreateRuntime(
        List<string> actorHand,
        List<string> targetHand,
        List<string>? drawPile = null)
    {
        var actor = Guid.NewGuid();
        var target = Guid.NewGuid();
        var state = new MatchRuntimeState
        {
            MatchId = Guid.NewGuid(),
            RoomCode = "TEST",
            Phase = MatchPhase.Playing,
            TurnIndex = 0,
            TurnEndsAt = DateTime.UtcNow.AddMinutes(1),
            DrawPile = drawPile ?? [],
            Players =
            [
                Player(actor, actorHand),
                Player(target, targetHand)
            ]
        };
        return (new MatchRuntime(state), actor, target);
    }

    private static MatchRuntimePlayerState Player(Guid userId, List<string> hand)
    {
        return new MatchRuntimePlayerState(
            userId,
            "Player",
            string.Empty,
            "player",
            true,
            PlayerLifeState.Alive,
            1,
            false,
            false,
            false,
            null,
            hand);
    }
}
