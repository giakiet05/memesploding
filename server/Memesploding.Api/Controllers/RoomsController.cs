using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Authorize]
public class RoomsController(IRoomService roomService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var userId = User.GetUserId();
        var room = await roomService.CreateRoomAsync(userId, dto);
        
        return CreatedAtAction(
            nameof(GetRoomByCode),
            new { code = room.Code },
            new ApiResponse<RoomDetailDto>("Room created successfully", room)
        );
    }

    [HttpPost("{code}/join")]
    public async Task<IActionResult> JoinRoom(string code)
    {
        var userId = User.GetUserId();
        var room = await roomService.JoinRoomAsync(userId, code);
        return Ok(new ApiResponse<RoomDetailDto>("Joined room successfully", room));
    }

    [HttpPost("{code}/leave")]
    public async Task<IActionResult> LeaveRoom(string code)
    {
        var userId = User.GetUserId();
        await roomService.LeaveRoomAsync(userId, code);
        return Ok(new ApiResponse<object?>("Left room successfully", null));
    }

    [HttpPatch("{code}")]
    public async Task<IActionResult> UpdateRoomSettings(string code, [FromBody] UpdateRoomDto dto)
    {
        var userId = User.GetUserId();
        var room = await roomService.UpdateRoomSettingsAsync(userId, code, dto);
        return Ok(new ApiResponse<RoomDetailDto>("Room updated successfully", room));
    }

    [HttpGet]
    public async Task<IActionResult> GetPublicRooms([FromQuery] RoomQueryDto query)
    {
        var rooms = await roomService.GetPublicRoomsAsync(query);
        return Ok(new ApiResponse<ListResponseData<RoomSummaryDto>>("Successfully!", rooms));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetRoomByCode(string code)
    {
        var userId = User.GetUserId();
        var room = await roomService.GetRoomByCodeAsync(code.ToUpper(), userId);
        return Ok(new ApiResponse<RoomDetailDto>("Successfully!", room));
    }
}
