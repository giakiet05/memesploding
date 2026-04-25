namespace Memesploding.Shared.Messaging.Integration.Channels;

public static class GameIntegrationChannels
{
    public const string StartMatchRequested = "integration:game:start_match_requested";
    public const string MatchStarted = "integration:game:match_started";
    public const string MatchEnded = "integration:game:match_ended";
    public const string RoomUpdated = "integration:room:updated";
}
