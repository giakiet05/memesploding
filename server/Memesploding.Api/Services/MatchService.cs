using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Memesploding.Api.Services;

public class MatchService(ApplicationDbContext db) : IMatchService
{
    public async Task<ListResponseData<MatchSummaryDto>> GetUserMatchHistoryAsync(Guid userId, PaginationQueryDto query)
    {
        // 1. Kiểm tra User tồn tại
        var userExists = await db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            throw AppException.NotFound("User not found");
        }

        // 2. Query đi từ bảng Matches để Include hoạt động chính xác
        var matchesQuery = db.Matches
            .Where(m => m.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(m => m.StartedAt);

        var totalCount = await matchesQuery.CountAsync();

        var matches = await matchesQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(m => m.Participants)
                .ThenInclude(p => p.User)
            .ToListAsync();

        // 3. Xử lý Parse JSON Settings và load CardSets theo batch
        var allCardSetIds = new List<Guid>();
        var matchSettings = new Dictionary<Guid, MatchSettingsDto>();

        foreach (var match in matches)
        {
            try 
            {
                var settingsJson = JsonDocument.Parse(match.Settings);
                var cardSetIds = settingsJson.RootElement
                    .GetProperty("cardSetIds")
                    .EnumerateArray()
                    .Select(e => Guid.Parse(e.GetString()!))
                    .ToList();

                var roomCode = settingsJson.RootElement.GetProperty("roomCode").GetString() ?? "UNKNOWN";
                var maxPlayers = settingsJson.RootElement.GetProperty("maxPlayers").GetInt32();
                var turnTimer = settingsJson.RootElement.GetProperty("turnTimer").GetInt32();

                matchSettings[match.Id] = new MatchSettingsDto(roomCode, cardSetIds, maxPlayers, turnTimer);
                allCardSetIds.AddRange(cardSetIds);
            }
            catch (Exception)
            {
                matchSettings[match.Id] = new MatchSettingsDto("UNKNOWN", new List<Guid>(), 0, 0);
            }
        }

        var distinctCardSetIds = allCardSetIds.Distinct().ToList();
        var cardSetsDict = await db.CardSets
            .Where(cs => distinctCardSetIds.Contains(cs.Id))
            .ToDictionaryAsync(cs => cs.Id, cs => new CardSetInfoDto(cs.Id, cs.Name));

        // 4. Map sang DTO
        var summaries = matches.Select(m =>
        {
            var settings = matchSettings.GetValueOrDefault(m.Id);
            var cardSets = settings?.CardSetIds
                .Select(csId => cardSetsDict.GetValueOrDefault(csId))
                .Where(cs => cs != null)
                .Cast<CardSetInfoDto>()
                .ToList() ?? new List<CardSetInfoDto>();

            var myParticipant = m.Participants.FirstOrDefault(p => p.UserId == userId);
            var players = m.Participants
                .OrderBy(p => p.FinalRank)
                .Select(p => new MatchPlayerSummaryDto(
                    UserId: p.UserId,
                    Nickname: p.User?.Username ?? "Unknown",
                    AvatarUrl: p.User?.AvatarUrl,
                    FinalRank: p.FinalRank
                ))
                .ToList();

            return new MatchSummaryDto(
                MatchId: m.Id,
                RoomCode: settings?.RoomCode ?? "UNKNOWN",
                StartedAt: m.StartedAt,
                EndedAt: m.EndedAt,
                TotalPlayers: m.Participants.Count,
                FinalRank: myParticipant?.FinalRank,
                XpEarned: myParticipant?.XpEarned ?? 0,
                ScoreChange: myParticipant?.ScoreChange ?? 0,
                CardSets: cardSets,
                Players: players
            );
        }).ToList();

        return new ListResponseData<MatchSummaryDto>(
            Items: summaries,
            Pagination: new PaginationMeta(
                query.Page,
                query.PageSize,
                totalCount,
                totalCount > query.Page * query.PageSize
            )
        );
    }

    public async Task<MatchDetailDto> GetMatchDetailAsync(Guid matchId)
    {
        var match = await db.Matches
            .Include(m => m.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null)
        {
            throw AppException.NotFound("Match not found");
        }

        var settingsJson = JsonDocument.Parse(match.Settings);
        var cardSetIds = settingsJson.RootElement
            .GetProperty("cardSetIds")
            .EnumerateArray()
            .Select(e => Guid.Parse(e.GetString()!))
            .ToList();

        var roomCode = settingsJson.RootElement.GetProperty("roomCode").GetString() ?? "UNKNOWN";

        var cardSets = await db.CardSets
            .Where(cs => cardSetIds.Contains(cs.Id))
            .Select(cs => new CardSetInfoDto(cs.Id, cs.Name))
            .ToListAsync();

        MatchStatsDto? stats = null;
        if (!string.IsNullOrEmpty(match.Stats))
        {
            var statsJson = JsonDocument.Parse(match.Stats);
            stats = new MatchStatsDto(
                TotalTurns: statsJson.RootElement.GetProperty("totalTurns").GetInt32(),
                TotalCards: statsJson.RootElement.GetProperty("totalCards").GetInt32(),
                Events: statsJson.RootElement.TryGetProperty("events", out var events) ? events : null
            );
        }

        var participants = match.Participants
            .Select(p => new MatchParticipantDetailDto(
                UserId: p.UserId,
                Nickname: p.User?.Username ?? "Unknown",
                AvatarUrl: p.User?.AvatarUrl,
                FinalRank: p.FinalRank,
                XpEarned: p.XpEarned,
                ScoreChange: p.ScoreChange
            ))
            .OrderBy(p => p.FinalRank)
            .ToList();

        return new MatchDetailDto(
            MatchId: match.Id,
            RoomCode: roomCode,
            StartedAt: match.StartedAt,
            EndedAt: match.EndedAt,
            WinnerId: match.WinnerId,
            CardSets: cardSets,
            Participants: participants,
            Stats: stats
        );
    }
}
