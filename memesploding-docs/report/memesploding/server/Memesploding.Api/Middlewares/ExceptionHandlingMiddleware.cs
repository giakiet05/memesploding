using System;
using System.Text.Json;
using System.Threading.Tasks;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
            _logger.LogError(ex, "Unhandled Server Exception: {Message}", ex.Message);
            
            // Xoay trục rẽ nhánh bọc lại thành DTO Lỗi Chuẩn Chỉ trả về Client
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        ApiErrorResponse errorResponse;

        if (exception is Memesploding.Api.Exceptions.AppException appEx)
        {
            // Lỗi có chủ ý (4xx): Dùng status code và error code của AppException
            context.Response.StatusCode = appEx.StatusCode;
            errorResponse = new ApiErrorResponse(appEx.Message, appEx.ErrorCode, appEx.Details);
        }
        else
        {
            // Lỗi bất ngờ (5xx): Ẩn chi tiết, chỉ trả về INTERNAL_ERROR
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            errorResponse = new ApiErrorResponse("Internal error happened!", ErrorCode.InternalError);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var result = JsonSerializer.Serialize(errorResponse, options);

        return context.Response.WriteAsync(result);
    }
}
