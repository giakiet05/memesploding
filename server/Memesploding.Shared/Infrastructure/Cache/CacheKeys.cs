namespace Memesploding.Shared.Infrastructure.Cache;

public static class CacheKeys
{
    // 1. Presence & Connections
    public static string UserPresence(Guid userId) => $"presence:{userId}";
    public static string UserConnections(Guid userId) => $"user_connections:{userId}";
    public static string UserLastActivity(Guid userId) => $"user_activity:{userId}";

    // 2. Rooms
    public static string RoomInfo(string roomCode) => $"room:{roomCode.ToUpper()}:info";
    public static string RoomParticipants(string roomCode) => $"room:{roomCode.ToUpper()}:participants";
    public static string RoomCardSets(string roomCode) => $"room:{roomCode.ToUpper()}:card_sets";
    public static string PublicRooms() => "public_rooms";
    public static string UserInRoom(Guid userId) => $"user:{userId}:room";

    // 3. Invitations (Mời vào phòng)
    public static string Invitation(string invitationId) => $"invitation:{invitationId}";
    public static string UserInvitationIndex(Guid userId) => $"invitation_index:{userId}";
    public static string InviteRateLimit(Guid inviterId, Guid friendId) => $"invite_ratelimit:{inviterId}:{friendId}";

    // 4. Join Requests (Xin vào phòng riêng)
    public static string JoinRequest(string requestId) => $"join_request:{requestId}";
    public static string JoinRequestRateLimit(Guid requesterId, string roomCode) => $"join_request_ratelimit:{requesterId}:{roomCode.ToUpper()}";
}
