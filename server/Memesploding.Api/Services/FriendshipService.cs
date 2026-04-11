using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;
using Microsoft.EntityFrameworkCore;

namespace Memesploding.Api.Services;

public class FriendshipService(
    ApplicationDbContext db, 
    IEventBus eventBus) : IFriendshipService
{
    public async Task<ListResponseData<UserProfileDto>> GetFriendsAsync(Guid userId, FriendshipQueryDto query)
    {
        var baseQuery = db.Friendships
            .Where(f => f.UserId1 == userId || f.UserId2 == userId);

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(f => f.Status == query.Status.Value);
        }

        var totalCount = await baseQuery.CountAsync();
        
        // Sử dụng trực tiếp từ DTO vì đã có mặc định
        var page = query.Pagination.Page;
        var pageSize = query.Pagination.PageSize;

        var friendships = await baseQuery
            .OrderByDescending(f => f.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                Friendship = f,
                FriendUserId = f.UserId1 == userId ? f.UserId2 : f.UserId1
            })
            .ToListAsync();

        var friendUserIds = friendships.Select(x => x.FriendUserId).ToList();
        var friendUsers = await db.Users
            .Where(u => friendUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var friendDtos = friendships.Select(f =>
        {
            var friendUser = friendUsers[f.FriendUserId];
            var relationship = UserProfileDto.MapRelationship(f.Friendship, userId);
            return UserProfileDto.FromEntity(friendUser, relationship);
        }).ToList();

        return new ListResponseData<UserProfileDto>(
            friendDtos, 
            new PaginationMeta(page, pageSize, totalCount, totalCount > page * pageSize)
        );
    }

    public async Task<UserProfileDto> SendFriendRequestAsync(Guid senderId, Guid receiverId)
    {
        if (senderId == receiverId)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You cannot send a friend request to yourself.");

        var sender = await db.Users.FindAsync(senderId);
        var receiver = await db.Users.FindAsync(receiverId);
        if (receiver == null || sender == null)
            throw AppException.NotFound("User not found.");

        var (uid1, uid2) = NormalizeUserIds(senderId, receiverId);

        var existing = await db.Friendships
            .FirstOrDefaultAsync(f => f.UserId1 == uid1 && f.UserId2 == uid2);

        if (existing != null)
        {
            throw AppException.BadRequest(ErrorCode.AlreadyFriends, "Friendship already exists or request is pending.");
        }

        var friendship = new Friendship
        {
            UserId1 = uid1,
            UserId2 = uid2,
            RequesterId = senderId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Friendships.Add(friendship);
        await db.SaveChangesAsync();

        // Bắn event ra Redis thay vì gọi trực tiếp SignalR
        var @event = new FriendRequestSentEvent(senderId, sender.Username, sender.AvatarUrl ?? "", receiverId);
        await eventBus.PublishAsync(EventChannels.FriendRequestSent, @event);

        return UserProfileDto.FromEntity(receiver, RelationshipType.PendingSent);
    }

    public async Task<UserProfileDto> RespondToFriendRequestAsync(Guid userId, Guid requesterId, bool accept)
    {
        var (uid1, uid2) = NormalizeUserIds(userId, requesterId);

        var friendship = await db.Friendships
            .FirstOrDefaultAsync(f => f.UserId1 == uid1 && f.UserId2 == uid2);

        if (friendship == null || friendship.Status != FriendshipStatus.Pending)
            throw AppException.NotFound("No pending request found.");

        if (friendship.RequesterId == userId)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You cannot respond to your own friend request.");

        var requester = await db.Users.FindAsync(requesterId);
        var currentUser = await db.Users.FindAsync(userId);
        if (requester == null || currentUser == null)
            throw AppException.NotFound("User not found.");

        if (accept)
        {
            friendship.Status = FriendshipStatus.Accepted;
            friendship.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Bắn event ra Redis
            var @event = new FriendRequestRespondedEvent(requesterId, userId, currentUser.Username, true);
            await eventBus.PublishAsync(EventChannels.FriendRequestResponded, @event);

            return UserProfileDto.FromEntity(requester, RelationshipType.Accepted);
        }
        else
        {
            db.Friendships.Remove(friendship);
            await db.SaveChangesAsync();
            return UserProfileDto.FromEntity(requester, RelationshipType.None);
        }
    }

    public async Task RemoveFriendAsync(Guid userId, Guid friendId)
    {
        var (uid1, uid2) = NormalizeUserIds(userId, friendId);

        var friendship = await db.Friendships
            .FirstOrDefaultAsync(f => f.UserId1 == uid1 && f.UserId2 == uid2);

        if (friendship == null)
            throw AppException.NotFound("Friendship not found.");

        if (friendship.Status != FriendshipStatus.Accepted)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Can only remove accepted friendships.");

        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync();
    }

    //Chuẩn hoá userId để đảm bảo luôn lưu theo thứ tự nhất định (uid1 < uid2) giúp tránh trùng lặp và dễ dàng truy vấn
    private static (Guid uid1, Guid uid2) NormalizeUserIds(Guid userId1, Guid userId2)
    {
        return userId1.CompareTo(userId2) < 0 
            ? (userId1, userId2) 
            : (userId2, userId1);
    }
}
