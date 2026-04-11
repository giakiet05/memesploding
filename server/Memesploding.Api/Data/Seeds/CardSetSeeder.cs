using Memesploding.Api.Data;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Memesploding.Api.Services;

/// <summary>
/// Seeder để load cards từ YAML file vào database
/// </summary>
public class CardSetSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, string yamlFilePath)
    {
        // Check if data already exists
        if (await db.CardSets.AnyAsync())
        {
            Console.WriteLine("CardSets already seeded. Skipping...");
            return;
        }

        Console.WriteLine($"Seeding CardSets from {yamlFilePath}...");

        // Read YAML file
        var yamlContent = await File.ReadAllTextAsync(yamlFilePath);

        // Parse YAML
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var data = deserializer.Deserialize<CardSetYamlRoot>(yamlContent);

        if (data?.CardSets == null || data.CardSets.Count == 0)
        {
            Console.WriteLine("No card sets found in YAML file.");
            return;
        }

        // Convert YAML data to entities
        foreach (var csYaml in data.CardSets)
        {
            var cardSet = new CardSet
            {
                Id = Guid.Parse(csYaml.Id),
                Name = csYaml.Name,
                Description = csYaml.Description,
                IsActive = csYaml.IsActive,
                ImageUrl = csYaml.ImageUrl,
                CreatedAt = DateTime.UtcNow
            };

            var cards = new List<Card>();
            foreach (var cardYaml in csYaml.Cards)
            {
                cards.Add(new Card
                {
                    Id = Guid.NewGuid(),
                    CardSetId = cardSet.Id,
                    Code = Enum.Parse<CardCode>(cardYaml.Code),
                    Name = cardYaml.Name,
                    Description = cardYaml.Description,
                    Type = Enum.Parse<CardType>(cardYaml.Type),
                    ImageUrl = cardYaml.ImageUrl,
                    IconUrl = cardYaml.IconUrl
                });
            }

            db.CardSets.Add(cardSet);
            db.Cards.AddRange(cards);

            Console.WriteLine($"  + Seeded CardSet: {cardSet.Name} ({cards.Count} cards)");
        }

        await db.SaveChangesAsync();
        Console.WriteLine("CardSets seed completed successfully.");
    }
}

// YAML schema classes
public class CardSetYamlRoot
{
    public List<CardSetYaml> CardSets { get; set; } = new();
}

public class CardSetYaml
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public List<CardYaml> Cards { get; set; } = new();
}

public class CardYaml
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? IconUrl { get; set; }
}
