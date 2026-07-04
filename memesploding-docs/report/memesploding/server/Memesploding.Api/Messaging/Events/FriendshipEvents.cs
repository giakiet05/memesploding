namespace Memesploding.Api.Messaging.Events;

public record FriendRequestSentEvent(
    Guid SenderId, 
    string SenderUsername, 
    string SenderAvatarUrl, 
    Guid ReceiverId
);

public record FriendRequestRespondedEvent(
    Guid RequesterId, 
    Guid ResponderId, 
    string ResponderUsername, 
    bool Accepted
);
