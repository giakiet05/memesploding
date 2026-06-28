using Models;
using Network.API.Models;
using TMPro;
using UnityEngine;

namespace UI
{
    public class RankingCardView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;

        private void Awake()
        {
            AutoBind();
        }

        public void Bind(UserProfileDto user, int rank)
        {
            BindCore(user?.Username, user?.Level ?? 0, user?.Score ?? 0, rank);
        }

        public void BindSelf(Player player, int globalRank)
        {
            BindCore(player?.Username, player?.Level ?? 0, player?.Score ?? 0, globalRank);
        }

        private void BindCore(string username, int level, int score, int rank)
        {
            AutoBind();

            if (rankText != null)
                rankText.text = rank > 0 ? $"#{rank}" : "-";

            if (usernameText != null)
                usernameText.text = string.IsNullOrWhiteSpace(username) ? "Player" : username;

            if (scoreText != null)
                scoreText.text = FormatScore(score);

            if (levelText != null)
                levelText.text = $"Lv{Mathf.Max(1, level)}";
        }

        private void AutoBind()
        {
            rankText ??= FindText("Ranking Number");
            usernameText ??= FindText("Username");
            scoreText ??= FindText("Username (1)");
            if (scoreText == null)
                scoreText = FindText("Pts");

            levelText ??= FindText("LevelStatus");
        }

        private TextMeshProUGUI FindText(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return null;

            var target = transform.Find(childName);
            return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
        }

        private static string FormatScore(int score)
        {
            return score.ToString("N0");
        }
    }
}
