using UI.MainMenu;
using UnityEngine;

namespace Managers
{
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance;

        [Header("Popups")]
        [SerializeField] private QuickMatchBackdrop quickMatchBackdrop;
        [SerializeField] private SettingPopupBackdrop settingPopupBackdrop;
        [SerializeField] private PlayNowBackdrop playNowBackdrop;
        [SerializeField] private RoomInvitationBackdrop roomInvitationBackdrop;
        [SerializeField] private LeaderboardPopup leaderboardPopup;

        [Header("Panels")]
        [SerializeField] private FriendsPannel friendsPannel;
        [SerializeField] private NotificationPannel notificationPannel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        public void HideAllPopups()
        {
            Hide(quickMatchBackdrop);
            Hide(settingPopupBackdrop);
            Hide(playNowBackdrop);
            Hide(roomInvitationBackdrop);
            Hide(leaderboardPopup);
            Hide(friendsPannel);
            Hide(notificationPannel);
        }

        public void OpenQuickMatch()
        {
            Show(quickMatchBackdrop);
        }

        public void CloseQuickMatch()
        {
            Hide(quickMatchBackdrop);
        }

        public void ToggleQuickMatch()
        {
            Toggle(quickMatchBackdrop);
        }

        public void OpenSettings()
        {
            Show(settingPopupBackdrop);
        }

        public void CloseSettings()
        {
            Hide(settingPopupBackdrop);
        }

        public void ToggleSettings()
        {
            Toggle(settingPopupBackdrop);
        }

        public void OpenPlayNowBackdrop()
        {
            Show(playNowBackdrop);
        }

        public void ClosePlayNowBackdrop()
        {
            Hide(playNowBackdrop);
        }

        public void TogglePlayNowBackdrop()
        {
            Toggle(playNowBackdrop);
        }

        public void OpenFriends()
        {
            Show(friendsPannel);
        }

        public void CloseFriends()
        {
            Hide(friendsPannel);
        }

        public void ToggleFriends()
        {
            Toggle(friendsPannel);
        }

        public void OpenNotifications()
        {
            Show(notificationPannel);
        }

        public void CloseNotifications()
        {
            Hide(notificationPannel);
        }

        public void ToggleNotifications()
        {
            Toggle(notificationPannel);
        }

        public void OpenRoomInvitation()
        {
            Show(roomInvitationBackdrop);
        }

        public void CloseRoomInvitation()
        {
            Hide(roomInvitationBackdrop);
        }

        public void ToggleRoomInvitation()
        {
            Toggle(roomInvitationBackdrop);
        }

        public void OpenLeaderboard()
        {
            Show(leaderboardPopup);
        }

        public void CloseLeaderboard()
        {
            Hide(leaderboardPopup);
        }

        public void ToggleLeaderboard()
        {
            Toggle(leaderboardPopup);
        }

        private void Show(MainMenuPopup popup)
        {
            if (popup == null)
                return;

            popup.Show();
        }

        private void Hide(MainMenuPopup popup)
        {
            if (popup == null)
                return;

            popup.Hide();
        }

        private void Toggle(MainMenuPopup popup)
        {
            if (popup == null)
                return;

            popup.Toggle();
        }
    }
}
