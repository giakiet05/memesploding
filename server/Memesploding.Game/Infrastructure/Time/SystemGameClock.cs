namespace Memesploding.Game.Infrastructure.Time;

public class SystemGameClock : IGameClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
