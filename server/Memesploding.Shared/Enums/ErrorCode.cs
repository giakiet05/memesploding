namespace Memesploding.Shared.Enums;

public static class ErrorCode
{
    // Hệ thống
    public const string InternalError = "INTERNAL_ERROR";
    public const string ValidationFailed = "VALIDATION_FAILED";

    // Người dùng và Xác thực (Auth/User)
    public const string Unauthorized = "UNAUTHORIZED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string PlayerAlreadyInRoom = "PLAYER_ALREADY_IN_ROOM";

    // Phòng chơi (Room/Match)
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomIsFull = "ROOM_IS_FULL";
    public const string NotRoomHost = "NOT_ROOM_HOST";
    public const string MatchAlreadyStarted = "MATCH_ALREADY_STARTED";

    // Bạn bè (Friendship)
    public const string FriendshipNotFound = "FRIENDSHIP_NOT_FOUND";
    public const string AlreadyFriends = "ALREADY_FRIENDS";
}
