using System;

namespace Network.Websocket
{
    public enum AppRoomConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Faulted
    }

    [Serializable]
    public class AppRoomSessionInfo
    {
        public string wsUrl;
        public string accessToken;
        public string roomCode;
        public DateTime? connectedAtUtc;
        public DateTime? lastMessageAtUtc;
        public string lastError;
    }

    [Serializable]
    public class AppRoomMemberJoinedDto
    {
        public string roomCode;
        public string userId;
        public string nickname;
        public string avatarUrl;
        public string role;
        public bool isReady;
    }

    [Serializable]
    public class AppRoomMemberLeftDto
    {
        public string roomCode;
        public string userId;
    }

    [Serializable]
    public class AppRoomMemberKickedDto
    {
        public string roomCode;
        public string targetUserId;
        public string kickedByUserId;
    }

    [Serializable]
    public class AppRoomReadyStatusChangedDto
    {
        public string roomCode;
        public string userId;
        public bool isReady;
    }

    [Serializable]
    public class AppRoomConnectionDto
    {
        public string wsUrl;
        public string wsAccessToken;
    }

    [Serializable]
    public class AppRoomMatchStartingDto
    {
        public string roomCode;
        public string startedByUserId;
        public AppRoomConnectionDto connection;
    }

    [Serializable]
    public class AppRoomHostChangedDto
    {
        public string roomCode;
        public string previousHostUserId;
        public string newHostUserId;
    }

    [Serializable]
    public class AppRoomDissolvedDto
    {
        public string roomCode;
        public string dissolvedByUserId;
    }

    [Serializable]
    public class AppRoomErrorDto
    {
        public string code;
        public string message;
    }
}
