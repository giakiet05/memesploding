using System.Text.Json;

namespace Memesploding.Api.DTOs;

public record MatchSummaryDto(
    Guid MatchId,
    string RoomCode,
    DateTime StartedAt,
    DateTime? EndedAt,
    int TotalPlayers,
    int? FinalRank,
    int XpEarned,
    int ScoreChange,
    List<CardSetInfoDto> CardSets,
    List<MatchPlayerSummaryDto> Players
);

public record MatchPlayerSummaryDto(
    Guid UserId,
    string Nickname,
    string? AvatarUrl,
    int FinalRank
);

public record MatchDetailDto(
    Guid MatchId,
    string RoomCode,
    DateTime StartedAt,
    DateTime? EndedAt,
    Guid? WinnerId,
    List<CardSetInfoDto> CardSets,
    List<MatchParticipantDetailDto> Participants,
    MatchStatsDto? Stats
);

public record MatchParticipantDetailDto(
    Guid UserId,
    string Nickname,
    string? AvatarUrl,
    int FinalRank,
    int XpEarned,
    int ScoreChange
);

public record MatchStatsDto(
    int TotalTurns,
    int TotalCards,
    JsonElement? Events
);

public record MatchSettingsDto(
    string RoomCode,
    List<Guid> CardSetIds,
    int MaxPlayers
);

public record BotTestMatchDto(
    Guid MatchId,
    string RoomCode,
    RoomConnectionDto Connection,
    List<RoomParticipantDto> Participants
);
