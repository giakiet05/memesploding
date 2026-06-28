using System.Text.Json;

namespace Memesploding.Game.DTOs;

public record WsClientCommand(
    string Event,
    JsonElement Data
);
