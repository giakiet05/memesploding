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

    /// <summary>
    /// 401 - Authentication issue: token missing/invalid/expired, chưa đăng nhập
    /// </summary>
    public static AppException Unauthorized(string message = "Unauthorized")
        => new(401, ErrorCode.Unauthorized, message);

    /// <summary>
    /// 403 - Authorization issue: đã đăng nhập nhưng không có quyền
    /// </summary>
    public static AppException Forbidden(string message = "Forbidden")
        => new(403, ErrorCode.Forbidden, message);

    /// <summary>
    /// 404 - Resource not found. Message nên specific (e.g., "User not found")
    /// </summary>
    public static AppException NotFound(string message = "Not found")
        => new(404, ErrorCode.NotFound, message);

    /// <summary>
    /// 400 - Bad request với specific error code cho business logic
    /// </summary>
    public static AppException BadRequest(ErrorCode errorCode, string message)
        => new(400, errorCode, message);
}
