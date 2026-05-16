using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    public class MenuManager : MonoBehaviour
    {
        public static MenuManager Instance;

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

            SceneManager.LoadScene(sceneName);
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
