using System;

namespace Network.API.Models
{
    [Serializable]
    public class CardSetDto
    {
        public string Id;
        public string Name;
        public string Description;
        public int CardCount;
        public string ImageUrl;
        public bool IsActive;
        public DateTime CreatedAt;
    }

    [Serializable]
    public class CardDto
    {
        public string Id;
        public string Code;
        public string Name;
        public string Description;
        public string Type;
        public string ImageUrl;
        public string IconUrl;
    }
}
