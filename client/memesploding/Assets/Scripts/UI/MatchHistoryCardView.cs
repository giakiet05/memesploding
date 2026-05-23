using System;
using System.Collections.Generic;
using System.Linq;
using Network.API.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class MatchHistoryCardView : MonoBehaviour
    {
        [Header("Card")]
        [SerializeField] private Image badgeBackground;
        [SerializeField] private TextMeshProUGUI badgeText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI periodText;
        [SerializeField] private TextMeshProUGUI xpText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI playersTotalText;
        [SerializeField] private RectTransform playerGridContent;

        [Header("Style")]
        [SerializeField] private Color winBadgeColor = new(0.20f, 0.65f, 0.34f, 1f);
        [SerializeField] private Color lossBadgeColor = new(0.80f, 0.27f, 0.27f, 1f);
        [SerializeField] private Color neutralBadgeColor = new(0.25f, 0.35f, 0.52f, 1f);
        [SerializeField] private Color positiveTextColor = new(0.18f, 0.60f, 0.30f, 1f);
        [SerializeField] private Color negativeTextColor = new(0.80f, 0.27f, 0.27f, 1f);
        [SerializeField] private Color neutralTextColor = Color.white;

        private TextMeshProUGUI _playersSummaryText;

        private void Awake()
        {
            AutoBind();
        }

        public void Bind(MatchSummaryDto match, string selfUserId)
        {
            AutoBind();

            var rank = match?.FinalRank;
            var isWin = rank.HasValue && rank.Value == 1;
            var isLoss = rank.HasValue && rank.Value > 1;

            if (badgeText != null)
                badgeText.text = rank.HasValue ? $"#{rank.Value}" : "-";

            if (badgeBackground != null)
                badgeBackground.color = isWin ? winBadgeColor : isLoss ? lossBadgeColor : neutralBadgeColor;

            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(match?.RoomCode) ? "Match History" : $"Room {match.RoomCode}";

            if (dateText != null)
                dateText.text = match == null ? "-" : match.StartedAt.ToLocalTime().ToString("dd MMM yyyy");

            if (periodText != null)
                periodText.text = FormatDuration(match?.StartedAt, match?.EndedAt);

            if (xpText != null)
            {
                xpText.text = FormatSigned(match?.XpEarned ?? 0, "XP");
                xpText.color = GetValueColor(match?.XpEarned ?? 0);
            }

            if (scoreText != null)
            {
                scoreText.text = FormatSigned(match?.ScoreChange ?? 0, "Score");
                scoreText.color = GetValueColor(match?.ScoreChange ?? 0);
            }

            if (playersTotalText != null)
                playersTotalText.text = $"{Mathf.Max(0, match?.TotalPlayers ?? 0)} Players";

            SetPlayers(match?.Players, selfUserId);
        }

        private void SetPlayers(List<MatchPlayerSummaryDto> players, string selfUserId)
        {
            if (playerGridContent == null)
                return;

            if (_playersSummaryText == null)
                _playersSummaryText = EnsurePlayersSummaryText();

            if (_playersSummaryText == null)
                return;

            if (players == null || players.Count == 0)
            {
                _playersSummaryText.text = "No player data";
                return;
            }

            var ordered = players
                .OrderBy(player => player.FinalRank <= 0 ? int.MaxValue : player.FinalRank)
                .ThenBy(player => player.Nickname)
                .Take(4)
                .Select(player =>
                {
                    var nickname = string.IsNullOrWhiteSpace(player.Nickname) ? "Player" : player.Nickname;
                    var rankText = player.FinalRank > 0 ? $"#{player.FinalRank}" : "-";
                    var selfTag = !string.IsNullOrWhiteSpace(selfUserId) &&
                                  string.Equals(player.UserId, selfUserId, StringComparison.OrdinalIgnoreCase)
                        ? " (You)"
                        : string.Empty;
                    return $"{rankText} {nickname}{selfTag}";
                });

            _playersSummaryText.text = string.Join("\n", ordered);
        }

        private void AutoBind()
        {
            badgeBackground ??= transform.Find("Badge")?.GetComponent<Image>();
            badgeText ??= transform.Find("Badge/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            titleText ??= transform.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            dateText ??= transform.Find("Date")?.GetComponent<TextMeshProUGUI>();
            periodText ??= transform.Find("Period")?.GetComponent<TextMeshProUGUI>();
            xpText ??= transform.Find("Points")?.GetComponent<TextMeshProUGUI>();
            scoreText ??= transform.Find("Points (1)")?.GetComponent<TextMeshProUGUI>();
            playersTotalText ??= transform.Find("PlayersTotal")?.GetComponent<TextMeshProUGUI>();
            playerGridContent ??= transform.Find("PlayerGrid/Content") as RectTransform;
        }

        private TextMeshProUGUI EnsurePlayersSummaryText()
        {
            if (playerGridContent == null)
                return null;

            var existing = playerGridContent.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existing != null)
                return existing;

            var textObject = new GameObject("PlayersSummary", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(playerGridContent, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.color = neutralTextColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static string FormatSigned(int value, string suffix)
        {
            var sign = value > 0 ? "+" : string.Empty;
            return $"{sign}{value} {suffix}";
        }

        private string FormatDuration(DateTime? startedAt, DateTime? endedAt)
        {
            if (!startedAt.HasValue || !endedAt.HasValue || endedAt.Value < startedAt.Value)
                return "--";

            var duration = endedAt.Value - startedAt.Value;
            if (duration.TotalHours >= 1d)
                return $"{(int)duration.TotalHours}h {duration.Minutes:D2}m";

            if (duration.TotalMinutes >= 1d)
                return $"{duration.Minutes}m {duration.Seconds:D2}s";

            return $"{Mathf.Max(0, duration.Seconds)}s";
        }

        private Color GetValueColor(int value)
        {
            if (value > 0)
                return positiveTextColor;

            if (value < 0)
                return negativeTextColor;

            return neutralTextColor;
        }
    }
}
