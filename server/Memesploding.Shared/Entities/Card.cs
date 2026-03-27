using System;
using Memesploding.Shared.Enums;

namespace Memesploding.Shared.Entities;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CardSetId { get; set; }
    public CardCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CardType Type { get; set; }
    public string? ImageUrl { get; set; }
    public string? IconUrl { get; set; }

    // Navigation property
    public CardSet? CardSet { get; set; }
}
