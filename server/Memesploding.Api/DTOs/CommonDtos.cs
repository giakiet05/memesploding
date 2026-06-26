using System.Collections.Generic;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;


// 1. Định dạng Trả về Data Đơn Lẻ (T)
public record ApiResponse<T>(string Message, T Data);

// 2. Định dạng Trả về Data Danh Sách (Kèm Nhảy Trang)
public record PaginationQueryDto
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
public record PaginationMeta(int Page, int PageSize, int TotalCount, bool HasMore);
public record ListResponseData<T>(IEnumerable<T> Items, PaginationMeta Pagination);
public record ApiListResponse<T>(string Message, ListResponseData<T> Data);

// 3. Định dạng Trả về Lỗi
public record ApiErrorResponse(string Message, ErrorCode ErrorCode, object? Details = null);

