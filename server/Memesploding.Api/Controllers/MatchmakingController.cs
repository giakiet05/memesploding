using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/matchmaking")]
[Authorize]
public class MatchmakingController : ControllerBase
{
    private readonly IMatchmakingService _matchmakingService;

    public MatchmakingController(IMatchmakingService matchmakingService)
    {
        _matchmakingService = matchmakingService;
    }

    [HttpPost("quick-play")]
    public async Task<IActionResult> QuickPlay()
    {
        var userId = User.GetUserId();
        var room = await _matchmakingService.QuickPlayAsync(userId);
        
        return Ok(new ApiResponse<RoomDetailDto>("Matched! Join the room to play.", room));
    }
}
