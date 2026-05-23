using Network.API.Models;
using TMPro;
using UnityEngine;

namespace UI
{
    public class LeaderboardCurrentUserView : LeaderboardEntryView
    {
        [SerializeField] private TextMeshProUGUI secondaryLevelText;

        protected override void AutoBind()
        {
            base.AutoBind();
            secondaryLevelText ??= transform.Find("LevelStatus (1)")?.GetComponent<TextMeshProUGUI>();
        }

        public new void Bind(UserProfileDto user, int rank)
        {
            base.Bind(user, rank);

            if (secondaryLevelText != null)
                secondaryLevelText.text = user == null ? "You" : $"Score {user.Score}";
        }
    }
}
