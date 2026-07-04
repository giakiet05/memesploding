using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface ICardSetService
{
    /// <summary>
    /// Lấy danh sách card sets đang active
    /// </summary>
    Task<ListResponseData<CardSetDto>> GetCardSetsAsync(PaginationQueryDto pagination);

    /// <summary>
    /// Lấy danh sách cards trong một card set
    /// </summary>
    Task<ListResponseData<CardDto>> GetCardSetCardsAsync(Guid cardSetId, PaginationQueryDto pagination);
}
