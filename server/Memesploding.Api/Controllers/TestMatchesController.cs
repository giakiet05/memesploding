using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/test-matches")]
[Authorize]
public class TestMatchesController(IBotTestMatchService botTestMatchService) : ControllerBase
{
    [HttpPost("bot")]
    public async Task<IActionResult> StartBotMatch()
    {
        var userId = User.GetUserId();
        var match = await botTestMatchService.StartAsync(userId);

        return Ok(new ApiResponse<BotTestMatchDto>("Bot test match started successfully", match));
    }
}
