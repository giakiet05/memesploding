namespace Memesploding.Shared.Events;

public record UserConnectedEvent(Guid UserId, string ConnectionId);
public record UserDisconnectedEvent(Guid UserId, string ConnectionId);
