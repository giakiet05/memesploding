namespace Memesploding.Game.Infrastructure.Time;

public interface IGameClock
{
    DateTime UtcNow { get; }
}
