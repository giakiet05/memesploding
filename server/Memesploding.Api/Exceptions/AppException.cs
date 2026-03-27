using Memesploding.Shared.Enums;

namespace Memesploding.Api.Exceptions;

/// <summary>
/// Exception có chủ ý - Lỗi business logic (4xx).
/// Middleware bắt riêng loại này và trả đúng HTTP status + ErrorCode enum.
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public ErrorCode ErrorCode { get; }

    public AppException(int statusCode, ErrorCode errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    // --- Factory methods ---

    public static AppException Unauthorized(string message = "Unauthorized")
        => new(401, ErrorCode.Unauthorized, message);

    public static AppException NotFound(ErrorCode errorCode, string message)
        => new(404, errorCode, message);

    public static AppException BadRequest(ErrorCode errorCode, string message)
        => new(400, errorCode, message);
}
