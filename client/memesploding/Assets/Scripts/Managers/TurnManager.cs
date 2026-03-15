using Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace Managers
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        [SerializeField] private Timer turnTimer;
    }
}