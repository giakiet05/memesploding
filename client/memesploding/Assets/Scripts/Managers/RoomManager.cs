using UnityEngine;

namespace Managers
{
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }
    }
}