using System.ComponentModel.DataAnnotations;

namespace Memesploding.Api.DTOs;

public record CreateRoomDto(
    [Range(2, 6)] int MaxPlayers,
    bool IsPublic,
    List<Guid> CardSetIds
);

public record UpdateReadyStatusDto(
    bool IsReady
);

// Quick Play không cần input - random vào phòng public bất kỳ
public record QuickPlayDto;

public record RoomQueryDto : PaginationQueryDto
{
    public List<Guid>? CardSetIds { get; init; }
    public List<int>? MaxPlayers { get; init; }
}

public record RoomSummaryDto(
    string Code,
    Guid HostId,
    string HostNickname,
    int MaxPlayers,
    int CurrentPlayers,
    string Status,
    List<CardSetInfoDto> CardSets,
    bool IsPublic,
    DateTime CreatedAt
);

public record RoomDetailDto(
    string Code,
    Guid HostId,
    string Status,
    bool IsPublic,
    RoomSettingsDto Settings,
    List<CardSetInfoDto> CardSets,
    List<RoomParticipantDto> CurrentParticipants,
    RoomConnectionDto? Connection
);

public record RoomSettingsDto(
    int MaxPlayers,
    int TurnTimer
);

public record RoomParticipantDto(
    Guid UserId,
    string Nickname,
    string AvatarUrl,
    string Role,
    bool IsReady
);

public record CardSetInfoDto(
    Guid Id,
    string Name
);

public record RoomConnectionDto(
    string WsUrl,
    string WsAccessToken
);
