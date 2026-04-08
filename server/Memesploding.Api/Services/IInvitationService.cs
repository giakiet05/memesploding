namespace Memesploding.Api.Services;

public interface IInvitationService
{

    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> InviteToRoomAsync(
        Guid inviterId, 
        string roomCode, 
        Guid friendUserId
    );


    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RespondInvitationAsync(
        Guid userId,
        string invitationId,
        bool accepted
    );

    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RequestJoinRoomAsync(
        Guid requesterId,
        string roomCode
    );

   
    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RespondJoinRequestAsync(
        Guid userId,
        string requestId,
        bool accepted
    );
}
