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
    public async Task<IActionResult> RegisterGuest([FromBody] RegisterGuestRequestDto request)
    {
        // Kiểm tra đầu vào thô (Validation)
        if (string.IsNullOrWhiteSpace(request.Nickname))
        {
            return BadRequest("Nickname is required");
        }

        // Chuyền bóng cho tầng Service xử lý database và logic
        var result = await _authService.RegisterGuestAsync(request);
        
        // Trả HTTP Mã 200 (Thành công) nguyên cục JSON
        return Ok(result);
    }
}
