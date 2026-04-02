using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IFriendshipService
{
    Task<ListResponseData<UserProfileDto>> GetFriendsAsync(Guid userId, FriendshipQueryDto query);
    Task<UserProfileDto> SendFriendRequestAsync(Guid senderId, Guid receiverId);
    Task<UserProfileDto> RespondToFriendRequestAsync(Guid userId, Guid requesterId, bool accept);
    Task RemoveFriendAsync(Guid userId, Guid friendId);
}