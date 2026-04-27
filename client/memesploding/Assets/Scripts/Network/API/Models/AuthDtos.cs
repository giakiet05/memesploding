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
