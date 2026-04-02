using Memesploding.Api.DTOs;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;

    public MatchesController(IMatchService matchService)
    {
        _matchService = matchService;
    }

    [HttpGet("me/match-history")]
    public async Task<IActionResult> GetMyMatchHistory([FromQuery] PaginationQueryDto query)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var matches = await _matchService.GetMyMatchHistoryAsync(userId, query);
        return Ok(new ApiResponse<ListResponseData<MatchSummaryDto>>("Successfully!", matches));
    }

    [HttpGet("users/{id}/match-history")]
    public async Task<IActionResult> GetUserMatchHistory(Guid id, [FromQuery] PaginationQueryDto query)
    {
        var matches = await _matchService.GetUserMatchHistoryAsync(id, query);
        return Ok(new ApiResponse<ListResponseData<MatchSummaryDto>>("Successfully!", matches));
    }

    [HttpGet("matches/{matchId}")]
    public async Task<IActionResult> GetMatchDetail(Guid matchId)
    {
        var match = await _matchService.GetMatchDetailAsync(matchId);
        return Ok(new ApiResponse<MatchDetailDto>("Successfully!", match));
    }
}
