using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.Hubs;

public partial class AppHub
{
    public async Task Heartbeat()
    {
        var userId = Context.User?.GetUserId();
        if (userId == null) return;

        await _presenceService.RefreshPresenceAsync(userId.Value);
    }
}
