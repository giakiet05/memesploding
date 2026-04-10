using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

public class ServiceResult
{
    public bool Success { get; init; }
    public ErrorCode? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ServiceResult Ok() => new() { Success = true };
    
    public static ServiceResult Fail(ErrorCode code, string message) 
        => new() { Success = false, ErrorCode = code, ErrorMessage = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data) 
        => new() { Success = true, Data = data };

    public new static ServiceResult<T> Fail(ErrorCode code, string message) 
        => new() { Success = false, ErrorCode = code, ErrorMessage = message };
}
