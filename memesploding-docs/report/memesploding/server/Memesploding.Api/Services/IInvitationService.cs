using Memesploding.Shared.Enums;
using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IInvitationService
{
    Task<ServiceResult> InviteToRoomAsync(
        Guid inviterId, 
        string roomCode, 
        Guid friendUserId
    );

    Task<ServiceResult> RespondInvitationAsync(
        Guid userId,
        string invitationId,
        bool accepted
    );

    Task<ServiceResult> RequestJoinRoomAsync(
        Guid requesterId,
        string roomCode
    );
   
    Task<ServiceResult> RespondJoinRequestAsync(
        Guid userId,
        string requestId,
        bool accepted
    );
}
