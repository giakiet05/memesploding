using Network.Websocket;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers.UIManager
{
    public class GameplaySceneBootstrapper : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        private bool _hasStarted;

        private async void Start()
        {
            if (_hasStarted)
                return;

            _hasStarted = true;

            var roomManager = RoomManager.EnsureInstance();
            if (!roomManager.HasRoom ||
                string.IsNullOrWhiteSpace(roomManager.GetWsUrl()) ||
                string.IsNullOrWhiteSpace(roomManager.GetWsAccessToken()))
            {
                UniversalPopup.ShowError("Missing game connection information.");
                LoadMainMenu();
                return;
            }

            if (GameWebsocketClient.Instance.Status != WebsocketConnectionStatus.Disconnected)
            {
                try
                {
                    await GameWebsocketClient.Instance.DisconnectAsync();
                }
                catch
                {
                }
            }

            GameWebsocketClient.Instance.ResetSession();
            NetworkManager.Instance.ConnectWs(
                roomManager.GetWsUrl(),
                roomManager.GetWsAccessToken(),
                roomManager.GetRoomCode());
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
