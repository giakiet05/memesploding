using UnityEngine;


public class Player : MonoBehaviour
{
    public Player(string id, string username)
    {
        ID = id;
        Username = username;
    }

    public string ID { get; set; }
    public string Username { get; set; }
}
