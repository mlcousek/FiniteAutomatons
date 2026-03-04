using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.Services.Interfaces;

public interface ISharedAutomatonSharingService
{
    Task<SharedAutomatonGroupInvitation> InviteByEmailAsync(int groupId, string inviterUserId, string email, SharedGroupRole role, int expirationDays = 7);
    Task<List<SharedAutomatonGroupInvitation>> ListPendingInvitationsAsync(int groupId, string userId);
    Task<SharedAutomatonGroupInvitation?> GetInvitationByTokenAsync(string token);
    Task AcceptInvitationAsync(string token, string userId);
    Task DeclineInvitationAsync(string token);
    Task CancelInvitationAsync(int invitationId, string userId);
    Task<string> GenerateInviteLinkAsync(int groupId, string userId, SharedGroupRole defaultRole, int? expirationDays = null);
    Task<string?> GetInviteLinkAsync(int groupId, string userId);
    Task DeactivateInviteLinkAsync(int groupId, string userId);
    Task<SharedAutomatonGroup?> JoinViaInviteLinkAsync(string inviteCode, string userId);
    Task<string> GetInvitationEmailContentAsync(SharedAutomatonGroupInvitation invitation);
}
