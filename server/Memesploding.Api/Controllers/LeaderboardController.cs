using Memesploding.Api.DTOs;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/leaderboard")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("global")]
    public async Task<IActionResult> GetGlobalLeaderboard([FromQuery] PaginationQueryDto query)
    {
        var leaderboard = await _leaderboardService.GetGlobalLeaderboardAsync(query);
        return Ok(new ApiResponse<ListResponseData<LeaderboardEntryDto>>("Successfully!", leaderboard));
    }
}
