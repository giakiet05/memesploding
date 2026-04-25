namespace Memesploding.Game.DTOs;

public record WsServerEvent<T>(
    string Event,
    T Data,
    DateTime Timestamp
)
{
    public static WsServerEvent<T> Create(string eventName, T data) => new(eventName, data, DateTime.UtcNow);
}
