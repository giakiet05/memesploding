using System.Text.Json;

namespace Memesploding.Game.Infrastructure.Serialization;

public class GameStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, Options);
}
