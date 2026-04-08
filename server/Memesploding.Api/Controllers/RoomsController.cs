using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var userId = User.GetUserId();
        var room = await _roomService.CreateRoomAsync(userId, dto);
        
        return CreatedAtAction(
            nameof(GetRoomByCode),
            new { code = room.Code },
            new ApiResponse<RoomDetailDto>("Room created successfully", room)
        );
    }

    [HttpGet]
    public async Task<IActionResult> GetPublicRooms([FromQuery] RoomQueryDto query)
    {
        var rooms = await _roomService.GetPublicRoomsAsync(query);
        return Ok(new ApiResponse<ListResponseData<RoomSummaryDto>>("Successfully!", rooms));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetRoomByCode(string code)
    {
        var room = await _roomService.GetRoomByCodeAsync(code.ToUpper());
        return Ok(new ApiResponse<RoomDetailDto>("Successfully!", room));
    }
}
