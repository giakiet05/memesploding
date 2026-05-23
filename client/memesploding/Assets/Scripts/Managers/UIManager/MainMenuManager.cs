using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Managers.UIManager
{
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance;

        [Header("Popups")]
        [SerializeField] private Popup quickMatchBackdrop;
        [SerializeField] private Popup settingPopupBackdrop;
        [SerializeField] private Popup playNowBackdrop;
        [SerializeField] private Popup roomInvitationBackdrop;
        [SerializeField] private Popup leaderboardPopup;

        [Header("Panels")]
        [SerializeField] private Popup friendsPannel;
        [SerializeField] private Popup notificationPannel;

        private readonly List<Popup[]> _popupHistory = new();

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
            _popupHistory.Clear();
        }

        public void ClosePreviousPopup()
        {
            if (_popupHistory.Count == 0)
                return;

            var previous = _popupHistory[^1];
            _popupHistory.RemoveAt(_popupHistory.Count - 1);

            foreach (var popup in previous)
            {
                Hide(popup);
            }
        }

        public void OpenQuickMatch()
        {
            ShowGroup(quickMatchBackdrop);
        }

        public void OpenSettings()
        {
            ShowGroup(settingPopupBackdrop);
        }

        public void OpenPlayNowBackdrop()
        {
            ShowGroup(playNowBackdrop);
        }

        public void OpenFriends()
        {
            ShowGroup(friendsPannel);
        }

        public void OpenNotifications()
        {
            ShowGroup(notificationPannel);
        }

        public void OpenRoomInvitation()
        {
            ShowGroup(roomInvitationBackdrop);
        }

        public void OpenLeaderboard()
        {
            ShowGroup(leaderboardPopup);
            var controller = FindFirstObjectByType<LeaderboardPopupController>(FindObjectsInactive.Include);
            if (controller != null)
                controller.Open();
        }

        private void ShowGroup(params Popup[] popups)
        {
            if (popups == null || popups.Length == 0)
                return;

            var shown = new List<Popup>();
            foreach (var popup in popups)
            {
                if (popup == null)
                    continue;

                popup.Show();
                shown.Add(popup);
            }

            if (shown.Count > 0)
                _popupHistory.Add(shown.ToArray());
        }

        private void Show(Popup popup)
        {
            if (popup == null)
                return;

            popup.Show();
        }

        private void Hide(Popup popup)
        {
            if (popup == null)
                return;

            popup.Hide();
        }
    }
}
