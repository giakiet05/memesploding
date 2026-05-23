using System.Collections;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers.UIManager
{
    public class LoadingSceneManager : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [SerializeField] private float minimumDisplaySeconds = 0.15f;

        private bool _isLoading;

        private void Start()
        {
            if (_isLoading)
                return;

            _isLoading = true;
            StartCoroutine(LoadGameplayRoutine());
        }

        private IEnumerator LoadGameplayRoutine()
        {
            var targetSceneName = NavigationManager.ConsumePendingSceneName();
            if (string.IsNullOrWhiteSpace(targetSceneName))
                targetSceneName = "Gameplay";

            if (string.Equals(targetSceneName, "Gameplay", System.StringComparison.Ordinal))
            {
                var roomManager = RoomManager.EnsureInstance();
                if (!roomManager.HasRoom ||
                    string.IsNullOrWhiteSpace(roomManager.GetWsUrl()) ||
                    string.IsNullOrWhiteSpace(roomManager.GetWsAccessToken()))
                {
                    UniversalPopup.ShowError("Missing game connection information.");
                    LoadMainMenu();
                    yield break;
                }
            }

            if (minimumDisplaySeconds > 0f)
                yield return new WaitForSeconds(minimumDisplaySeconds);
            else
                yield return null;

            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadScene(targetSceneName);
                yield break;
            }

            SceneManager.LoadScene(targetSceneName);
        }

        private static void LoadMainMenu()
        {
            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadMainMenu();
                return;
            }

            SceneManager.LoadScene(MainMenuSceneName);
        }
    }
}
