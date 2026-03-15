using UnityEngine;


public class PlayerModel : MonoBehaviour
{
    public PlayerModel(string id, string username)
    {
        ID = id;
        Username = username;
    }

    public string ID { get; set; }
    public string Username { get; set; }
}
