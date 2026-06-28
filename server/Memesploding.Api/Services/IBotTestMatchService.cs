using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IBotTestMatchService
{
    Task<BotTestMatchDto> StartAsync(Guid userId);
}
