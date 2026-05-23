using Events;
using Events.GameEvents;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Events.EventType;

namespace Managers
{
    public class NavigationManager : MonoBehaviour
    {
        private const string LoadingSceneName = "Loading";
        private static readonly HashSet<string> ScenesUsingLoading = new HashSet<string>
        {
            "Gameplay"
        };

        public static NavigationManager Instance;
        public static string PendingSceneName { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        public void LoadScene(string sceneName)
        {
            LoadSceneInternal(sceneName, false);
        }

        public void LoadSceneWithLoading(string sceneName)
        {
            LoadSceneInternal(sceneName, true);
        }

        public static string ConsumePendingSceneName()
        {
            var pendingSceneName = PendingSceneName;
            PendingSceneName = string.Empty;
            return pendingSceneName;
        }

        private void LoadSceneInternal(string sceneName, bool forceLoading)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Scene name is empty", this);
                return;
            }

            var fromScene = SceneManager.GetActiveScene().name;
            if (ShouldUseLoadingScene(sceneName, forceLoading))
            {
                PendingSceneName = sceneName;
                SceneManager.LoadScene(LoadingSceneName);
                EventBus.Publish(EventType.SceneChanged, new SceneChangedEventPayload(fromScene, LoadingSceneName));
                return;
            }

            PendingSceneName = string.Empty;
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
            LoadSceneWithLoading("Gameplay");
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private static bool ShouldUseLoadingScene(string sceneName, bool forceLoading)
        {
            if (string.Equals(SceneManager.GetActiveScene().name, LoadingSceneName, System.StringComparison.Ordinal))
                return false;

            return forceLoading || ScenesUsingLoading.Contains(sceneName);
        }
    }
}
