using System.Collections.Concurrent;
using System.Text.Json;
using Memesploding.Game.Domain.MatchRuntime;
using Memesploding.Game.Domain.StateMachine;
using Memesploding.Game.Infrastructure.Runtime;

namespace Memesploding.Game.Workers;

public class BotRuntimeWorker(
    IMatchRuntimeManager runtimeManager,
    ILogger<BotRuntimeWorker> logger
) : BackgroundService
{
    private const double BotVsHumanNopeChance = 0.15;
    private const double BotVsBotNopeChance = 0.20;
    private readonly ConcurrentDictionary<string, long> _handledVersions = new();
    private readonly ConcurrentDictionary<string, byte> _nopeWindowParticipants = new();
    private readonly ConcurrentDictionary<string, int> _turnPlayCounts = new();
    private readonly ConcurrentDictionary<string, int> _turnPlayLimits = new();
    private readonly ConcurrentDictionary<string, PendingBotAction> _pendingActions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BotRuntimeWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var runtime in runtimeManager.GetAllRuntimes())
            {
                stoppingToken.ThrowIfCancellationRequested();
                await TickRuntimeBotsAsync(runtime, stoppingToken);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TickRuntimeBotsAsync(MatchRuntime runtime, CancellationToken cancellationToken)
    {
        var state = runtime.State;
        if (!state.IsTestMatch || state.Phase != MatchPhase.Playing)
        {
            return;
        }

        foreach (var bot in state.Players.Where(IsAliveBot))
        {
            var botKey = $"{state.MatchId}:{bot.UserId}";
            if (!_pendingActions.TryGetValue(botKey, out var pendingAction) ||
                pendingAction.StateVersion != state.StateVersion)
            {
                if (!TryBuildCommand(runtime, bot, out var command))
                {
                    _pendingActions.TryRemove(botKey, out _);
                    continue;
                }

                pendingAction = new PendingBotAction(
                    command,
                    state.StateVersion,
                    DateTime.UtcNow.AddMilliseconds(state.BotActionDelayMs)
                );
                _pendingActions[botKey] = pendingAction;
                continue;
            }

            if (pendingAction.ReadyAtUtc > DateTime.UtcNow)
            {
                continue;
            }

            if (_handledVersions.TryGetValue(botKey, out var version) && version == state.StateVersion)
            {
                continue;
            }

            _handledVersions[botKey] = state.StateVersion;
            _pendingActions.TryRemove(botKey, out _);
            if (pendingAction.Command.Name.Equals("PlayCard", StringComparison.OrdinalIgnoreCase))
            {
                _turnPlayCounts.AddOrUpdate(GetTurnKey(state, bot.UserId), 1, (_, count) => count + 1);
            }
            await runtimeManager.EnqueueCommandAsync(state.MatchId, pendingAction.Command, cancellationToken);
            return;
        }
    }

    private bool TryBuildCommand(MatchRuntime runtime, MatchRuntimePlayerState bot, out RuntimeCommand command)
    {
        command = default!;
        var state = runtime.State;

        if (state.PendingFavorTargetId == bot.UserId)
        {
            command = new RuntimeCommand("ChooseFavorCard", bot.UserId, PickCardPayload(bot));
            return true;
        }

        if (state.PendingDefuseUserId == bot.UserId)
        {
            if (HasCard(bot, "Defuse"))
            {
                command = new RuntimeCommand("UseDefuse", bot.UserId, "{}");
                return true;
            }

            return false;
        }

        if (state.PendingBombOwnerUserId == bot.UserId &&
            state.PendingBombCardCode != null &&
            state.BombReinsertWindowEndsAt.HasValue)
        {
            command = new RuntimeCommand(
                "ChooseBombInsertPosition",
                bot.UserId,
                JsonSerializer.Serialize(new { position = Random.Shared.Next(state.DrawPile.Count + 1) })
            );
            return true;
        }

        if (state.PendingReactionAction != null &&
            state.ReactionWindowEndsAt.HasValue &&
            state.ReactionWindowEndsAt.Value > DateTime.UtcNow &&
            state.PendingReactionUserId != bot.UserId &&
            HasCard(bot, "Nope") &&
            HasNotNopedInCurrentWindow(state, bot.UserId) &&
            Random.Shared.NextDouble() < GetNopeChance(state) &&
            TryReserveBotNopeForCurrentWindow(state))
        {
            command = new RuntimeCommand("Nope", bot.UserId, "{}");
            return true;
        }

        if (state.PendingReactionAction != null ||
            state.ReactionResolveAt.HasValue ||
            state.TurnAdvanceAt.HasValue ||
            state.PendingDefuseUserId.HasValue ||
            state.PendingBombCardCode != null ||
            state.PendingFavorTargetId.HasValue)
        {
            return false;
        }

        if (runtime.GetCurrentTurnUserId() != bot.UserId)
        {
            return false;
        }

        if (state.DrawPile.Count == 0)
        {
            return false;
        }

        var turnKey = GetTurnKey(state, bot.UserId);
        var playCount = _turnPlayCounts.GetOrAdd(turnKey, 0);
        var playLimit = _turnPlayLimits.GetOrAdd(turnKey, _ => Random.Shared.Next(1, 4));
        if (playCount >= playLimit)
        {
            command = new RuntimeCommand("DrawCard", bot.UserId, "{}");
            return true;
        }

        var playableCard = PickPlayableCard(bot);
        if (playableCard != null)
        {
            command = new RuntimeCommand("PlayCard", bot.UserId, BuildPlayCardPayload(state, bot.UserId, playableCard));
            return true;
        }

        command = new RuntimeCommand("DrawCard", bot.UserId, "{}");
        return true;
    }

    private static string? PickPlayableCard(MatchRuntimePlayerState bot)
    {
        var hand = bot.Hand ?? [];
        var candidates = hand
            .Where(card => card is "Skip" or "Attack" or "Shuffle" or "SeeTheFuture" or "Favor")
            .Distinct()
            .ToList();

        return candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];
    }

    private static string BuildPlayCardPayload(MatchRuntimeState state, Guid botUserId, string cardCode)
    {
        if (cardCode == "Favor")
        {
            var target = state.Players
                .Where(player => player.UserId != botUserId &&
                                 player.LifeState == PlayerLifeState.Alive)
                .OrderBy(player => player.Role.Equals("bot", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .Select(player => (Guid?)player.UserId)
                .FirstOrDefault();

            if (target.HasValue)
            {
                return JsonSerializer.Serialize(new { cardCode, targetUserId = target.Value });
            }
        }

        return JsonSerializer.Serialize(new { cardCode });
    }

    private static string PickCardPayload(MatchRuntimePlayerState bot)
    {
        var hand = bot.Hand ?? [];
        if (hand.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(new { cardCode = hand[Random.Shared.Next(hand.Count)] });
    }

    private static bool HasCard(MatchRuntimePlayerState player, string cardCode)
    {
        return (player.Hand ?? []).Contains(cardCode);
    }

    private static bool IsAliveBot(MatchRuntimePlayerState player)
    {
        return player.Role.Equals("bot", StringComparison.OrdinalIgnoreCase) &&
               player.LifeState == PlayerLifeState.Alive;
    }

    private static double GetNopeChance(MatchRuntimeState state)
    {
        return IsBotUser(state, state.PendingReactionUserId)
            ? BotVsBotNopeChance
            : BotVsHumanNopeChance;
    }

    private static bool IsBotUser(MatchRuntimeState state, Guid? userId)
    {
        return userId.HasValue &&
               state.Players.Any(player => player.UserId == userId.Value &&
                                           player.Role.Equals("bot", StringComparison.OrdinalIgnoreCase));
    }

    private bool TryReserveBotNopeForCurrentWindow(MatchRuntimeState state)
    {
        return _nopeWindowParticipants.TryAdd(GetNopeWindowKey(state), 0);
    }

    private bool HasNotNopedInCurrentWindow(MatchRuntimeState state, Guid userId)
    {
        return !_nopeWindowParticipants.ContainsKey(GetNopeWindowKey(state, userId));
    }

    private static string GetNopeWindowKey(MatchRuntimeState state)
    {
        return $"{state.MatchId}:{state.PendingReactionUserId}:{state.PendingReactionAction}:{state.ReactionWindowEndsAt:O}:bot-nope";
    }

    private static string GetNopeWindowKey(MatchRuntimeState state, Guid userId)
    {
        return $"{state.MatchId}:{state.PendingReactionUserId}:{state.PendingReactionAction}:{state.ReactionWindowEndsAt:O}:{userId}";
    }

    private static string GetTurnKey(MatchRuntimeState state, Guid userId)
    {
        return $"{state.MatchId}:{state.TurnCounter}:{userId}";
    }

    private sealed record PendingBotAction(RuntimeCommand Command, long StateVersion, DateTime ReadyAtUtc);
}
