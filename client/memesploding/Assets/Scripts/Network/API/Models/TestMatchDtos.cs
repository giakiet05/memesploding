using System;
using System.Collections.Generic;

namespace Network.API.Models
{
    [Serializable]
    public class BotTestMatchDto
    {
        public string MatchId;
        public string RoomCode;
        public RoomConnectionDto Connection;
        public List<RoomParticipantDto> Participants;
    }
}
