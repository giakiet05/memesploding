namespace Memesploding.Api.Messaging.Channels;

public static class EventChannels
{
    public const string FriendRequestSent = "events:friendship:req_sent";
    public const string FriendRequestResponded = "events:friendship:req_responded";

    public const string RoomInvitationSent = "events:room:inv_sent";
    public const string RoomInvitationResponded = "events:room:inv_responded";
    public const string RoomJoinRequestSent = "events:room:join_req_sent";
    public const string RoomJoinRequestResponded = "events:room:join_req_responded";
    public const string RoomMemberJoined = "events:room:member_joined";
    public const string RoomMemberLeft = "events:room:member_left";
    public const string RoomMemberKicked = "events:room:member_kicked";
    public const string RoomReadyStatusChanged = "events:room:ready_changed";
    public const string RoomMatchStarting = "events:room:match_starting";
    public const string RoomHostChanged = "events:room:host_changed";
    public const string RoomDissolved = "events:room:dissolved";
    
    public const string RoomUpdates = "room:updates";
    
    public const string UserConnected = "events:presence:connected";
    public const string UserDisconnected = "events:presence:disconnected";
    public const string FriendStatusChanged = "events:presence:friend_status_changed"; // Thêm mới
}
