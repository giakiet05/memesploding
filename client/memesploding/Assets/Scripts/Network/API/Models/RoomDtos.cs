using System;
using System.Collections.Generic;

namespace Network.API.Models
{
    [Serializable]
    public class CreateRoomDto
    {
        public int MaxPlayers;
        public bool IsPublic;
        public List<string> CardSetIds;
    }

    [Serializable]
    public class UpdateRoomDto
    {
        public int? MaxPlayers;
        public bool? IsPublic;
        public List<string> CardSetIds;
    }

    [Serializable]
    public class RoomQueryDto : PaginationQueryDto
    {
        public List<string> CardSetIds;
        public List<int> MaxPlayers;
    }

    [Serializable]
    public class RoomSummaryDto
    {
        public string Code;
        public string HostId;
        public string HostNickname;
        public int MaxPlayers;
        public int CurrentPlayers;
        public string Status;
        public List<CardSetInfoDto> CardSets;
        public bool IsPublic;
        public DateTime CreatedAt;
    }

    [Serializable]
    public class RoomDetailDto
    {
        public string Code;
        public string HostId;
        public string Status;
        public bool IsPublic;
        public RoomSettingsDto Settings;
        public List<CardSetInfoDto> CardSets;
        public List<RoomParticipantDto> CurrentParticipants;
        public RoomConnectionDto Connection;
    }

    [Serializable]
    public class RoomSettingsDto
    {
        public int MaxPlayers;
    }

    [Serializable]
    public class RoomParticipantDto
    {
        public string UserId;
        public string Nickname;
        public string AvatarUrl;
        public string Role;
        public bool IsReady;
    }

    [Serializable]
    public class CardSetInfoDto
    {
        public string Id;
        public string Name;
    }

    [Serializable]
    public class RoomConnectionDto
    {
        public string WsUrl;
        public string WsAccessToken;
    }
}
