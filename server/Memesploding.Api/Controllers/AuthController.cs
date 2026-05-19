namespace Memesploding.Api.Controllers;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest("device_id is required");
        }

        var responseData = await _authService.RegisterGuestAsync(request);
        
        // Đóng gói data bằng ApiResponse chuẩn thiết kế
        return Ok(new ApiResponse<AuthResponseDto>("Login successful", responseData));
    }

    [HttpPost("google")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginGoogle([FromBody] LoginGoogleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest("Authorization code is required");
        }

        var responseData = await _authService.LoginGoogleAsync(request);
        return Ok(new ApiResponse<AuthResponseDto>("Login successful", responseData));
    }

    [HttpPost("email/register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RegisterEmail([FromBody] RegisterEmailRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("email and password are required");
        }

        var responseData = await _authService.RegisterEmailAsync(request);
        return Ok(new ApiResponse<AuthResponseDto>("Registration successful", responseData));
    }

    [HttpPost("email/login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginEmail([FromBody] LoginEmailRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("email and password are required");
        }

        var responseData = await _authService.LoginEmailAsync(request);
        return Ok(new ApiResponse<AuthResponseDto>("Login successful", responseData));
    }

    [HttpPost("forgot-password/send-otp")]
    public async Task<ActionResult<ApiResponse<object?>>> SendForgotPasswordOtp([FromBody] ForgotPasswordSendOtpRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("email is required");
        }

        await _authService.SendForgotPasswordOtpAsync(request);
        return Ok(new ApiResponse<object?>("OTP sent", null));
    }

    [HttpPost("forgot-password/verify-otp")]
    public async Task<ActionResult<ApiResponse<object?>>> VerifyForgotPasswordOtp([FromBody] ForgotPasswordVerifyOtpRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest("email and otp are required");
        }

        await _authService.VerifyForgotPasswordOtpAsync(request);
        return Ok(new ApiResponse<object?>("OTP verified", null));
    }

    [HttpPost("forgot-password/reset")]
    public async Task<ActionResult<ApiResponse<object?>>> ResetForgotPassword([FromBody] ForgotPasswordResetRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Otp) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("email, otp and new password are required");
        }

        await _authService.ResetForgotPasswordAsync(request);
        return Ok(new ApiResponse<object?>("Password reset successful", null));
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
    [Authorize]
    public async Task<ActionResult<ApiResponse<object?>>> Logout([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("Refresh token is required");

        // Lấy Access Token từ Authorization header
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        var accessToken = authHeader.StartsWith("Bearer ") ? authHeader["Bearer ".Length..] : string.Empty;

        await _authService.LogoutAsync(accessToken, request.RefreshToken);
        return Ok(new ApiResponse<object?>("Logged out successfully", null));
    }
}
