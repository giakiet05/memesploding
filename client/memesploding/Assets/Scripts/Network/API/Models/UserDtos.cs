using System;

namespace Network.API.Models
{
    [Serializable]
    public class UpdateUserRequestDto
    {
        public string Username;
        public string Bio;
        public string AvatarUrl;
    }

    [Serializable]
    public class UserQueryDto
    {
        public string SearchQuery;
        public PaginationQueryDto Pagination = new PaginationQueryDto();
    }

    [Serializable]
    public class UserStatsDto
    {
        public long Xp;
        public long NextLevelXp;
        public int Score;
        public int Level;
        public int HighestScore;
        public int TotalMatches;
        public int TotalWins;
        public double WinRate;
        public int GlobalRank;
    }

    [Serializable]
    public class MeDto
    {
        public string Id;
        public string Username;
        public string Email;
        public string Provider;
        public string AvatarUrl;
        public string Bio;
        public int Level;
        public int Score;
        public DateTime CreatedAt;
        public DateTime UpdatedAt;
    }

    [Serializable]
    public class UserProfileDto
    {
        public string Id;
        public string Username;
        public string AvatarUrl;
        public string Bio;
        public int Level;
        public int Score;
        public string Relationship;
    }
}
