using System.Threading.Tasks;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.AspNetCore.Http;

namespace Memesploding.Api.Middlewares;

public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICacheStore cache)
    {
        var authHeader = context.Request.Headers["Authorization"].ToString();
        
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            
            if (accessToken.Length > 20)
            {
                var tokenSuffix = accessToken[^20..];
                var isRevoked = await cache.GetAsync<string>($"bl:{tokenSuffix}");
                
                if (!string.IsNullOrEmpty(isRevoked))
                {
                    // Token đã bị blacklist -> ném lỗi để ExceptionHandlingMiddleware lo việc trả 401
                    throw AppException.Unauthorized("Access token has been revoked.");
                }
            }
        }

        // Chuyển tiếp request nếu không vấn đề gì
        await _next(context);
    }
}
