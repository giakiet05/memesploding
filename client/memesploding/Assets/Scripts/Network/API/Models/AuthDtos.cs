using System;

namespace Network.API.Models
{
    [Serializable]
    public class RegisterGuestRequestDto
    {
        public string DeviceId;
    }

    [Serializable]
    public class LoginGoogleRequestDto
    {
        public string IdToken;
    }

    [Serializable]
    public class RegisterEmailRequestDto
    {
        public string Email;
        public string Password;
    }

    [Serializable]
    public class LoginEmailRequestDto
    {
        public string Email;
        public string Password;
    }

    [Serializable]
    public class ForgotPasswordSendOtpRequestDto
    {
        public string Email;
    }

    [Serializable]
    public class ForgotPasswordVerifyOtpRequestDto
    {
        public string Email;
        public string Otp;
    }

    [Serializable]
    public class ForgotPasswordResetRequestDto
    {
        public string Email;
        public string Otp;
        public string NewPassword;
    }

    [Serializable]
    public class RefreshTokenRequestDto
    {
        public string RefreshToken;
    }

    [Serializable]
    public class AuthResponseDto
    {
        public MeDto User;
        public string AccessToken;
        public string RefreshToken;
        public bool IsNewUser;
    }
}
