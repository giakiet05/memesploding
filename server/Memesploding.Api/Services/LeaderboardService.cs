using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Memesploding.Api.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly ApplicationDbContext _db;

    public LeaderboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ListResponseData<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(PaginationQueryDto query)
    {
        // Get users with at least 1 match, ordered by Score DESC
        var usersQuery = _db.Users
            .Where(u => u.TotalMatches > 0)
            .OrderByDescending(u => u.Score)
            .ThenBy(u => u.Username);

        var totalCount = await usersQuery.CountAsync();

        var users = await usersQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.AvatarUrl,
                u.Level,
                u.Score,
                u.TotalWins
            })
            .ToListAsync();

        // Calculate ranks (simple approach: rank based on position in current page + offset)
        var startRank = (query.Page - 1) * query.PageSize + 1;
        
        var entries = users.Select((u, index) => new LeaderboardEntryDto(
            Rank: startRank + index,
            UserId: u.Id,
            Nickname: u.Username,
            AvatarUrl: u.AvatarUrl,
            Level: u.Level,
            Score: u.Score,
            TotalWins: u.TotalWins
        )).ToList();

        return new ListResponseData<LeaderboardEntryDto>(
            Items: entries,
            Pagination: new PaginationMeta(
                query.Page,
                query.PageSize,
                totalCount,
                totalCount > query.Page * query.PageSize
            )
        );
    }
}
