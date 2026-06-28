using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class AuthService
    {
        private static AuthService _instance;
        public static AuthService Instance => _instance ??= new AuthService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1/auth";

        private AuthService() { }

        public Task<ApiResponse<AuthResponseDto>> RegisterGuestAsync(RegisterGuestRequestDto request)
        {
            return ApiClient.Instance.PostAsync<AuthResponseDto>($"{_baseUrl}/guest", request);
        }

        public Task<ApiResponse<AuthResponseDto>> LoginGoogleAsync(LoginGoogleRequestDto request)
        {
            return ApiClient.Instance.PostAsync<AuthResponseDto>($"{_baseUrl}/google", request);
        }

        public Task<ApiResponse<AuthResponseDto>> RegisterEmailAsync(RegisterEmailRequestDto request)
        {
            return ApiClient.Instance.PostAsync<AuthResponseDto>($"{_baseUrl}/email/register", request);
        }

        public Task<ApiResponse<AuthResponseDto>> LoginEmailAsync(LoginEmailRequestDto request)
        {
            return ApiClient.Instance.PostAsync<AuthResponseDto>($"{_baseUrl}/email/login", request);
        }

        public Task<ApiResponse<object>> SendForgotPasswordOtpAsync(ForgotPasswordSendOtpRequestDto request)
        {
            return ApiClient.Instance.PostAsync<object>($"{_baseUrl}/forgot-password/send-otp", request);
        }

        public Task<ApiResponse<object>> VerifyForgotPasswordOtpAsync(ForgotPasswordVerifyOtpRequestDto request)
        {
            return ApiClient.Instance.PostAsync<object>($"{_baseUrl}/forgot-password/verify-otp", request);
        }

        public Task<ApiResponse<object>> ResetForgotPasswordAsync(ForgotPasswordResetRequestDto request)
        {
            return ApiClient.Instance.PostAsync<object>($"{_baseUrl}/forgot-password/reset", request);
        }

        public Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            return ApiClient.Instance.PostAsync<AuthResponseDto>($"{_baseUrl}/refresh", request);
        }

        public Task<ApiResponse<object>> LogoutAsync(RefreshTokenRequestDto request, string accessToken)
        {
            return ApiClient.Instance.PostAsync<object>($"{_baseUrl}/logout", request, accessToken);
        }
    }
}
