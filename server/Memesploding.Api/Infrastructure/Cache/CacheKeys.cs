namespace Memesploding.Api.Infrastructure.Cache;

public static class CacheKeys
{
    // 1. Presence & Connections
    public static string UserPresence(Guid userId) => $"str:presence:{userId}";
    public static string UserConnections(Guid userId) => $"set:user_connections:{userId}";
    public static string UserLastActivity(Guid userId) => $"str:user_activity:{userId}";

    // 2. Rooms
    public static string RoomInfo(string roomCode) => $"hash:room:{roomCode.ToUpper()}:info";
    public static string RoomParticipants(string roomCode) => $"hash:room:{roomCode.ToUpper()}:participants";
    public static string RoomCardSets(string roomCode) => $"set:room:{roomCode.ToUpper()}:card_sets";
    public static string PublicRooms() => "set:public_rooms";
    public static string UserInRoom(Guid userId) => $"str:user:{userId}:room";
    public static string RoomReconnectGrace(Guid userId) => $"str:room_reconnect_grace:{userId}";
    public static string RoomReconnectGraceUsers() => "set:room_reconnect_grace:users";

    // 3. Invitations (Mời vào phòng)
    public static string Invitation(string invitationId) => $"str:invitation:{invitationId}";
    public static string InviteRateLimit(Guid inviterId, Guid friendId) => $"str:invite_ratelimit:{inviterId}:{friendId}";

    // 4. Join Requests (Xin vào phòng riêng)
    public static string JoinRequest(string requestId) => $"str:join_request:{requestId}";
    public static string JoinRequestRateLimit(Guid requesterId, string roomCode) => $"str:join_request_ratelimit:{requesterId}:{roomCode.ToUpper()}";
}
