namespace Memesploding.Api.Services;

using System.Threading.Tasks;
using Memesploding.Api.DTOs;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterGuestAsync(RegisterGuestRequestDto request);
}
