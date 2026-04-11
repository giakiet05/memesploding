using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

/// <summary>
/// Card Set summary DTO - dùng cho list view
/// </summary>
public record CardSetDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CardCount { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public static CardSetDto FromEntity(CardSet entity, int cardCount)
    {
        return new CardSetDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            CardCount = cardCount,
            ImageUrl = entity.ImageUrl,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        };
    }
}

/// <summary>
/// Card detail DTO
/// </summary>
public record CardDto
{
    public Guid Id { get; init; }
    public CardCode Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CardType Type { get; init; }
    public string? ImageUrl { get; init; }
    public string? IconUrl { get; init; }

    public static CardDto FromEntity(Card entity)
    {
        return new CardDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            Type = entity.Type,
            ImageUrl = entity.ImageUrl,
            IconUrl = entity.IconUrl
        };
    }
}
