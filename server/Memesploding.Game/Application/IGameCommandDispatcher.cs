using Memesploding.Game.Auth;
using Memesploding.Game.DTOs;

namespace Memesploding.Game.Application;

public interface IGameCommandDispatcher
{
    Task<long?> DispatchAsync(GameConnectionContext context, WsClientCommand command, CancellationToken cancellationToken = default);
}
