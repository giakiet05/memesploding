namespace Memesploding.Api.Controllers;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Memesploding.Api.DTOs;
using Memesploding.Api.Services;

[ApiController]
[Route("api/v1/[controller]")] // Tự động lấy chữ Auth gắn vô đường dẫn: api/v1/auth
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    // Gọi Bếp trưởng AuthService vô phục vụ
    // Không cần gọi ApplicationDbContext ở đây vì Controller chỉ chơi với Service (Lọc Logic)
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // Endpoint: POST /api/auth/guest
    [HttpPost("guest")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RegisterGuest([FromBody] RegisterGuestRequestDto request)
    {
        // Kiểm tra đầu vào thô (Validation)
        if (string.IsNullOrWhiteSpace(request.Nickname))
        {
            return BadRequest("Nickname is required");
        }

        var responseData = await _authService.RegisterGuestAsync(request);
        
        // Đóng gói data bằng ApiResponse chuẩn thiết kế
        return Ok(new ApiResponse<AuthResponseDto>("Login successful", responseData));
    }

    [HttpPost("google")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginGoogle([FromBody] LoginGoogleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Authorization code is required");
        }

        var responseData = await _authService.LoginGoogleAsync(request);
        return Ok(new ApiResponse<AuthResponseDto>("Login successful", responseData));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required");
        }

        var responseData = await _authService.RefreshAsync(request);
        return Ok(new ApiResponse<AuthResponseDto>("Token refreshed", responseData));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object?>>> Logout([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required");
        }

        await _authService.LogoutAsync(request.RefreshToken);
        return Ok(new ApiResponse<object?>("Logged out successfully", null));
    }
}
