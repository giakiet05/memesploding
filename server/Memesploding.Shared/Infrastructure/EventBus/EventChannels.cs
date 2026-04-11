namespace Memesploding.Shared.Infrastructure.EventBus;

public static class EventChannels
{
    public const string FriendRequestSent = "events:friendship:req_sent";
    public const string FriendRequestResponded = "events:friendship:req_responded";

    public const string RoomInvitationSent = "events:room:inv_sent";
    public const string RoomInvitationResponded = "events:room:inv_responded";
    public const string RoomJoinRequestSent = "events:room:join_req_sent";
    public const string RoomJoinRequestResponded = "events:room:join_req_responded";
    
    public const string RoomUpdates = "room:updates";
    
    public const string UserConnected = "events:presence:connected";
    public const string UserDisconnected = "events:presence:disconnected";
    public const string FriendStatusChanged = "events:presence:friend_status_changed"; // Thêm mới
}
