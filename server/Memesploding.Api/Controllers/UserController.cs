using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UserController(IUserService userService) : ControllerBase
{
    [HttpGet("me/profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MeDto>>> GetMe()
    {
        var userId = User.GetUserId();
        var user = await userService.GetMeAsync(userId);
        return Ok(new ApiResponse<MeDto>("Profile retrieved successfully", user));
    }

    [HttpPatch("me/profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MeDto>>> UpdateProfile([FromBody] UpdateUserRequestDto request)
    {
        var userId = User.GetUserId();
        var user = await userService.UpdateUserAsync(userId, request);
        return Ok(new ApiResponse<MeDto>("Profile updated successfully", user));
    }

    [HttpGet("me/stats")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserStatsDto>>> GetMyStats()
    {
        var userId = User.GetUserId();
        var data = await userService.GetUserStatsAsync(userId);
        return Ok(new ApiResponse<UserStatsDto>("User stats retrieved successfully", data));
    }

    [HttpGet("{userId:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetUserById(Guid userId)
    {
        var currentUserId = User.GetUserId();
        var data = await userService.GetUserProfileAsync(currentUserId, userId);
        return Ok(new ApiResponse<UserProfileDto>("Information retrieved successfully", data));
    }

    [HttpGet("{userId:guid}/stats")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserStatsDto>>> GetUserStatsById(Guid userId)
    {
        var data = await userService.GetUserStatsAsync(userId);
        return Ok(new ApiResponse<UserStatsDto>("User stats retrieved successfully", data));
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiListResponse<UserProfileDto>>> GetUsers([FromQuery] UserQueryDto query)
    {
        var currentUserId = User.GetUserId();
        var result = await userService.GetUsersAsync(currentUserId, query, excludeUserId: currentUserId);
        return Ok(new ApiListResponse<UserProfileDto>("Users retrieved successfully", result));
    }
    
    [HttpGet("leaderboard")]
    [Authorize]
    public async Task<ActionResult<ApiListResponse<UserProfileDto>>> GetLeaderboard([FromQuery] PaginationQueryDto pagination)
    {
        var currentUserId = User.GetUserId();
        var result = await userService.GetUsersAsync(currentUserId, new UserQueryDto { Pagination = pagination });
        return Ok(new ApiListResponse<UserProfileDto>("Leaderboard retrieved successfully", result));
    }
}
