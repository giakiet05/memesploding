using System.Text.Json.Serialization;

namespace Memesploding.Shared.Enums;

/// <summary>
/// Error codes dùng cho API response.
/// Enum name dùng trong C# (PascalCase), JSON output là SCREAMING_SNAKE_CASE.
/// Generic codes only - message field provides specific details.
/// </summary>
public enum ErrorCode
{
    // Hệ thống
    [JsonStringEnumMemberName("INTERNAL_ERROR")]
    InternalError,

    [JsonStringEnumMemberName("VALIDATION_FAILED")]
    ValidationFailed,

    // Xác thực & Phân quyền
    /// <summary>
    /// 401 - Authentication issue: chưa đăng nhập, token missing/invalid/expired
    /// </summary>
    [JsonStringEnumMemberName("UNAUTHORIZED")]
    Unauthorized,

    [JsonStringEnumMemberName("TOKEN_EXPIRED")]
    TokenExpired,

    /// <summary>
    /// 403 - Authorization issue: đã đăng nhập nhưng không có quyền thực hiện action
    /// </summary>
    [JsonStringEnumMemberName("FORBIDDEN")]
    Forbidden,

    // Generic resource errors
    /// <summary>
    /// 404 - Resource not found (user, room, notification, etc.)
    /// </summary>
    [JsonStringEnumMemberName("NOT_FOUND")]
    NotFound,

    // Business logic errors (giữ lại những cái cần thiết cho game logic)
    [JsonStringEnumMemberName("ROOM_IS_FULL")]
    RoomIsFull,

    [JsonStringEnumMemberName("NOT_ROOM_HOST")]
    NotRoomHost,

    [JsonStringEnumMemberName("MATCH_ALREADY_STARTED")]
    MatchAlreadyStarted,

    [JsonStringEnumMemberName("PLAYER_ALREADY_IN_ROOM")]
    PlayerAlreadyInRoom,

    [JsonStringEnumMemberName("ALREADY_FRIENDS")]
    AlreadyFriends,

    [JsonStringEnumMemberName("NOT_IN_ROOM")]
    NotInRoom,

    [JsonStringEnumMemberName("NOT_FRIENDS")]
    NotFriends,

    [JsonStringEnumMemberName("RATE_LIMITED")]
    RateLimited,

    [JsonStringEnumMemberName("INVITATION_NOT_FOUND")]
    InvitationNotFound,

    [JsonStringEnumMemberName("ROOM_PUBLIC")]
    RoomPublic,

    [JsonStringEnumMemberName("REQUEST_NOT_FOUND")]
    RequestNotFound,
}
