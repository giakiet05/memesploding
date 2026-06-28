using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Network.API.Models
{
    [Serializable]
    public class MatchSummaryDto
    {
        public string MatchId;
        public string RoomCode;
        public DateTime StartedAt;
        public DateTime? EndedAt;
        public int TotalPlayers;
        public int? FinalRank;
        public int XpEarned;
        public int ScoreChange;
        public List<CardSetInfoDto> CardSets;
        public List<MatchPlayerSummaryDto> Players;
    }

    [Serializable]
    public class MatchPlayerSummaryDto
    {
        public string UserId;
        public string Nickname;
        public string AvatarUrl;
        public int FinalRank;
    }

    [Serializable]
    public class MatchDetailDto
    {
        public string MatchId;
        public string RoomCode;
        public DateTime StartedAt;
        public DateTime? EndedAt;
        public string WinnerId;
        public List<CardSetInfoDto> CardSets;
        public List<MatchParticipantDetailDto> Participants;
        public MatchStatsDto Stats;
    }

    [Serializable]
    public class MatchParticipantDetailDto
    {
        public string UserId;
        public string Nickname;
        public string AvatarUrl;
        public int FinalRank;
        public int XpEarned;
        public int ScoreChange;
    }

    [Serializable]
    public class MatchStatsDto
    {
        public int TotalTurns;
        public int TotalCards;
        public JToken Events;
    }
}
