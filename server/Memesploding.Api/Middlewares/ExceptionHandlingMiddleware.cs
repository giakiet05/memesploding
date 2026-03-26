using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Memesploding.Api.DTOs;

namespace Memesploding.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Cứ cho dòng lệnh chạy qua như bình thường, ngầm theo dõi
            await _next(context);
        }
        catch (Exception ex)
        {
            // Bẫy sập bẫy! Server quăng Lỗi rùi, ghi log đỏ cất đi
            _logger.LogError(ex, "Lỗi vỡ mặt Server: {Message}", ex.Message);
            
            // Xoay trục rẽ nhánh bọc lại thành DTO Lỗi Chuẩn Chỉ trả về Client
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        // Luôn là lỗi 500 do kịch bản sập ngầm
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Vứt ra 1 mặt phẳng cái format Error như trong File MD chỉ định
        var errorResponse = new ApiErrorResponse(
            Message: "Internal error happened!",
            ErrorCode: Memesploding.Shared.Enums.ErrorCode.InternalError
        );

        // Chuyển đối tượng C# thành chữ JSON (Nhớ kẹp luật Snake_Case nha)
        var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower 
        });
        
        return context.Response.WriteAsync(result);
    }
}
