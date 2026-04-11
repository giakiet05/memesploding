using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.EntityFrameworkCore;

namespace Memesploding.Api.Services;

public class UserService(ApplicationDbContext db) : IUserService
{
    public async Task<MeDto> GetMeAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException("User not found.");

        return MeDto.FromEntity(user);
    }

    public async Task<MeDto> UpdateUserAsync(Guid userId, UpdateUserRequestDto request)
    {
        var user = await db.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(request.Username))
            user.Username = request.Username;
        
        if (!string.IsNullOrWhiteSpace(request.Bio))
            user.Bio = request.Bio;

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
            user.AvatarUrl = request.AvatarUrl;

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return MeDto.FromEntity(user);
    }

    public async Task<ListResponseData<UserProfileDto>> GetUsersAsync(Guid currentUserId, UserQueryDto query, Guid? excludeUserId = null)
    {
        var dbQuery = db.Users.AsQueryable();

        if (excludeUserId.HasValue)
        {
            dbQuery = dbQuery.Where(u => u.Id != excludeUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchQuery))
        {
            dbQuery = dbQuery.Where(u => EF.Functions.ILike(u.Username, $"%{query.SearchQuery}%"));
        }

        var totalCount = await dbQuery.CountAsync();

        var meta = new PaginationMeta(query.Pagination.Page, query.Pagination.PageSize, totalCount, totalCount > query.Pagination.Page * query.Pagination.PageSize);

        var rows = await dbQuery
            .Select(u => new {
                TargetUser = u,
                Friendship = db.Friendships.FirstOrDefault(f => 
                    (f.UserId1 == currentUserId && f.UserId2 == u.Id) || 
                    (f.UserId1 == u.Id && f.UserId2 == currentUserId))
            })
            .OrderByDescending(x => x.TargetUser.Score)
            .Skip((query.Pagination.Page - 1) * query.Pagination.PageSize)
            .Take(query.Pagination.PageSize)
            .ToListAsync();

        var data = rows.Select(x => UserProfileDto.FromEntity(
            x.TargetUser, 
            UserProfileDto.MapRelationship(x.Friendship, currentUserId)
        )).ToList();

        return new ListResponseData<UserProfileDto>(data, meta);
    }

    public async Task<UserProfileDto> GetUserProfileAsync(Guid currentUserId, Guid targetUserId)
    {
        var user = await db.Users.FindAsync(targetUserId);
        if (user == null) throw AppException.NotFound("User not found");

        var friendship = await db.Friendships.FirstOrDefaultAsync(f => 
            (f.UserId1 == currentUserId && f.UserId2 == targetUserId) || 
            (f.UserId1 == targetUserId && f.UserId2 == currentUserId));

        return UserProfileDto.FromEntity(user, UserProfileDto.MapRelationship(friendship, currentUserId));
    }

    public async Task<UserStatsDto> GetUserStatsAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException("User not found.");

        var globalRank = await db.Users.CountAsync(u => u.Score > user.Score) + 1;
        
        var winRate = user.TotalMatches > 0 
            ? Math.Round((double)user.TotalWins / user.TotalMatches * 100, 2) 
            : 0;

        var userStatsDto = new UserStatsDto(
            user.Xp,
            CalculateNextLevelXp(user.Level),
            user.Score,
            user.Level,
            user.HighestScore,
            user.TotalMatches,
            user.TotalWins,
            winRate,
            globalRank
        );
        
        return userStatsDto;
    }

    public long CalculateNextLevelXp(int currentLevel)
    {
        return (long)(1000 * Math.Pow(1.5, currentLevel - 1));
    }
}