using System.Text.Json.Serialization;

namespace Memesploding.Shared.Enums;

/// <summary>
/// Error codes dùng cho API response.
/// Enum name dùng trong C# (PascalCase), JSON output là SCREAMING_SNAKE_CASE.
/// </summary>
public enum ErrorCode
{
    // Hệ thống
    [JsonStringEnumMemberName("INTERNAL_ERROR")]
    InternalError,

    [JsonStringEnumMemberName("VALIDATION_FAILED")]
    ValidationFailed,

    // Xác thực (Auth)
    [JsonStringEnumMemberName("UNAUTHORIZED")]
    Unauthorized,

    [JsonStringEnumMemberName("TOKEN_EXPIRED")]
    TokenExpired,

    // Người dùng (User)
    [JsonStringEnumMemberName("USER_NOT_FOUND")]
    UserNotFound,

    // Phòng chơi (Room)
    [JsonStringEnumMemberName("ROOM_NOT_FOUND")]
    RoomNotFound,

    [JsonStringEnumMemberName("ROOM_IS_FULL")]
    RoomIsFull,

    [JsonStringEnumMemberName("NOT_ROOM_HOST")]
    NotRoomHost,

    [JsonStringEnumMemberName("MATCH_ALREADY_STARTED")]
    MatchAlreadyStarted,

    [JsonStringEnumMemberName("PLAYER_ALREADY_IN_ROOM")]
    PlayerAlreadyInRoom,

    // Bạn bè (Friendship)
    [JsonStringEnumMemberName("FRIENDSHIP_NOT_FOUND")]
    FriendshipNotFound,

    [JsonStringEnumMemberName("ALREADY_FRIENDS")]
    AlreadyFriends,
}
