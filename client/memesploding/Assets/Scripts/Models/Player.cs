using UnityEngine;

namespace Models
{
    public class Player
    {
        public string ID { get; set; }
        public string Username { get; set; }
        public string AvatarUrl { get; set; }
        public string Bio { get; set; }
        public int Level { get; set; }
        public int Score { get; set; }
    }
}