using Memesploding.Game.Auth;
using Memesploding.Game.Domain.MatchRuntime;
using Memesploding.Game.DTOs;
using Memesploding.Game.Infrastructure.Runtime;

namespace Memesploding.Game.Application;

public class GameCommandDispatcher(IMatchRuntimeManager runtimeManager) : IGameCommandDispatcher
{
    public async Task<long?> DispatchAsync(GameConnectionContext context, WsClientCommand command, CancellationToken cancellationToken = default)
    {
        var matchId = context.MatchId;
        if (matchId == null)
        {
            if (!runtimeManager.TryGetByRoomCode(context.RoomCode, out var runtimeByRoom) || runtimeByRoom == null)
            {
                return null;
            }

            matchId = runtimeByRoom.State.MatchId;
        }

        var eventName = command.Event.Trim();
        if (eventName.Length == 0)
        {
            return null;
        }

        var payload = command.Data.ValueKind == System.Text.Json.JsonValueKind.Undefined
            ? "{}"
            : command.Data.GetRawText();

        await runtimeManager.EnqueueCommandAsync(
            matchId.Value,
            new RuntimeCommand(eventName, context.UserId, payload),
            cancellationToken
        );

        if (!runtimeManager.TryGetByMatchId(matchId.Value, out var runtime) || runtime == null)
        {
            return null;
        }

        return runtime.State.StateVersion;
    }
}
