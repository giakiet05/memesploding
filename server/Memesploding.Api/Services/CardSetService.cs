using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Memesploding.Api.Services;

public class CardSetService(ApplicationDbContext db) : ICardSetService
{
    public async Task<ListResponseData<CardSetDto>> GetCardSetsAsync(PaginationQueryDto pagination)
    {
        var query = db.CardSets.Where(cs => cs.IsActive);

        var totalCount = await query.CountAsync();

        var cardSets = await query
            .OrderBy(cs => cs.Name)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        // Load card counts for each card set
        var cardSetIds = cardSets.Select(cs => cs.Id).ToList();
        var cardCounts = await db.Cards
            .Where(c => cardSetIds.Contains(c.CardSetId))
            .GroupBy(c => c.CardSetId)
            .Select(g => new { CardSetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CardSetId, x => x.Count);

        var dtos = cardSets.Select(cs => 
            CardSetDto.FromEntity(cs, cardCounts.GetValueOrDefault(cs.Id, 0))
        ).ToList();

        var paginationMeta = new PaginationMeta(
            pagination.Page,
            pagination.PageSize,
            totalCount,
            totalCount > pagination.Page * pagination.PageSize
        );

        return new ListResponseData<CardSetDto>(dtos, paginationMeta);
    }

    public async Task<ListResponseData<CardDto>> GetCardSetCardsAsync(Guid cardSetId, PaginationQueryDto pagination)
    {
        // Validate card set exists and is active
        var cardSet = await db.CardSets.FindAsync(cardSetId);
        if (cardSet == null || !cardSet.IsActive)
            throw AppException.NotFound("Card set not found");

        var query = db.Cards.Where(c => c.CardSetId == cardSetId);

        var totalCount = await query.CountAsync();

        var cards = await query
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        var dtos = cards.Select(CardDto.FromEntity).ToList();

        var paginationMeta = new PaginationMeta(
            pagination.Page,
            pagination.PageSize,
            totalCount,
            totalCount > pagination.Page * pagination.PageSize
        );

        return new ListResponseData<CardDto>(dtos, paginationMeta);
    }
}
