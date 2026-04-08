using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

/// <summary>
/// Các loại sự kiện bắn qua WebSocket (SignalR)
/// </summary>
public enum WsEventType
{
    // Social
    FriendRequestReceived,
    FriendRequestAccepted,
    FriendStatusChanged,
    
    // Game/Room
    RoomInvitationReceived,
    RoomInvitationResponse,
    JoinRoomRequested,
    JoinRoomResponse,
    
    // System
    Error,
    SystemNotice
}

/// <summary>
/// Định dạng chuẩn cho mọi tin nhắn bắn qua WebSocket
/// </summary>
public record WsMessage<T>(
    WsEventType Event,
    T Data,
    DateTime Timestamp
)
{
    public static WsMessage<T> Create(WsEventType eventType, T data) 
        => new(eventType, data, DateTime.UtcNow);
}

// --- Chi tiết các DTO cho từng loại Event ---

public record WsFriendRequestDto(
    Guid SenderId,
    string SenderUsername,
    string SenderAvatarUrl,
    string Message = ""
);

public record WsFriendStatusDto(
    Guid UserId,
    string Username,
    string AvatarUrl,
    bool Online,
    DateTime? LastSeen,
    WsUserActivityDto Activity
);

public record WsUserActivityDto(
    string Type, // "idle", "in_room", "in_match"
    WsRoomBriefDto? Room = null
);

public record WsRoomBriefDto(
    string Code,
    string Status,
    bool IsPublic = true,
    int CurrentPlayers = 0,
    int MaxPlayers = 0
);

public record WsRoomInvitationDto(
    string InvitationId,
    string RoomCode,
    Guid InviterId,
    string InviterUsername,
    string InviterAvatarUrl,
    bool IsPublic,
    int CurrentPlayers,
    int MaxPlayers,
    DateTime ExpiresAt
);

public record WsRoomInvitationResponseDto(
    string InvitationId,
    bool Accepted,
    Guid InviteeId,
    string InviteeUsername
);

public record WsJoinRoomRequestDto(
    string RequestId,
    string RoomCode,
    Guid RequesterId,
    string RequesterUsername,
    string RequesterAvatarUrl,
    DateTime ExpiresAt
);

public record WsJoinRoomResponseDto(
    string RequestId,
    string RoomCode,
    bool Accepted,
    Guid? ResponderId,
    string? ResponderUsername
);

public record WsErrorDto(
    ErrorCode Code,
    string Message
);
