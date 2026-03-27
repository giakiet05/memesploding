using System.Collections.Generic;

namespace Memesploding.Api.DTOs;

// 1. Định dạng Trả về Data Đơn Lẻ (T)
public record ApiResponse<T>(string Message, T Data);

// 2. Định dạng Trả về Data Danh Sách (Kèm Nhảy Trang)
public record PaginationMeta(int Page, int PageSize, int TotalCount, bool HasMore);
public record ListResponseData<T>(IEnumerable<T> Items, PaginationMeta Pagination);
public record ApiListResponse<T>(string Message, ListResponseData<T> Data);

// 3. Định dạng Trả về Lỗi
public record ApiErrorResponse(string Message, Memesploding.Shared.Enums.ErrorCode ErrorCode);
