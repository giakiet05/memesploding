using Memesploding.Api.DTOs;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/card-sets")]
[Authorize]
public class CardSetsController(ICardSetService cardSetService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách các bộ bài đang active
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiListResponse<CardSetDto>>> GetCardSets([FromQuery] PaginationQueryDto pagination)
    {
        var result = await cardSetService.GetCardSetsAsync(pagination);
        return Ok(new ApiListResponse<CardSetDto>("Card sets retrieved successfully", result));
    }

    /// <summary>
    /// Lấy danh sách các lá bài trong một bộ cụ thể
    /// </summary>
    [HttpGet("{id:guid}/cards")]
    public async Task<ActionResult<ApiListResponse<CardDto>>> GetCardSetCards(Guid id, [FromQuery] PaginationQueryDto pagination)
    {
        var result = await cardSetService.GetCardSetCardsAsync(id, pagination);
        return Ok(new ApiListResponse<CardDto>("Cards retrieved successfully", result));
    }
}
