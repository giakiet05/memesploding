using Events;
using Events.GameEvents;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Events.EventType;

namespace Managers
{
    public class NavigationManager : MonoBehaviour
    {
        public static NavigationManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Scene name is empty", this);
                return;
            }

            var fromScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(sceneName);
            EventBus.Publish(EventType.SceneChanged, new SceneChangedEventPayload(fromScene, sceneName));
        }

        public void LoadWelcome()
        {
            LoadScene("Welcome");
        }

        public void LoadMainMenu()
        {
            LoadScene("MainMenu");
        }

        public void LoadCreateRoom()
        {
            LoadScene("CreateRoom");
        }

        public void LoadJoinRoom()
        {
            LoadScene("JoinRoom");
        }

        public void LoadWaitRoom()
        {
            LoadScene("WaitRoom");
        }

        public void LoadProfile()
        {
            LoadScene("Profile");
        }

        public void LoadMatchHistory()
        {
            LoadScene("MatchHistory");
        }

        public void LoadGameplay()
        {
            LoadScene("Gameplay");
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
