using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/me/friends")]
[Authorize]
public class FriendsController(IFriendshipService friendshipService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiListResponse<UserProfileDto>>> GetFriends([FromQuery] FriendshipQueryDto query)
    {
        var userId = User.GetUserId();
        var result = await friendshipService.GetFriendsAsync(userId, query);
        return Ok(new ApiListResponse<UserProfileDto>("Friends list retrieved successfully", result));
    }

    [HttpPost("invitations")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> SendInvitation([FromBody] FriendshipRequestDto request)
    {
        var userId = User.GetUserId();
        var data = await friendshipService.SendFriendRequestAsync(userId, request.UserId);
        return Ok(new ApiResponse<UserProfileDto>("Invitation sent successfully", data));
    }

    [HttpPatch("invitations/{requesterId:guid}")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> RespondInvitation(Guid requesterId, [FromBody] ProcessFriendRequestDto request)
    {
        var userId = User.GetUserId();
        var data = await friendshipService.RespondToFriendRequestAsync(userId, requesterId, request.Accept);
        return Ok(new ApiResponse<UserProfileDto>(request.Accept ? "Invitation accepted" : "Invitation rejected", data));
    }

    [HttpDelete("{friendId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> RemoveFriend(Guid friendId)
    {
        var userId = User.GetUserId();
        await friendshipService.RemoveFriendAsync(userId, friendId);
        return Ok(new ApiResponse<object?>("Friend removed successfully", null));
    }
}
