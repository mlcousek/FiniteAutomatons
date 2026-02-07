using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for sharing groups via email invitations and shareable links
/// </summary>
public interface ISharedAutomatonSharingService
{
    // Email invitations
    Task<SharedAutomatonGroupInvitation> InviteByEmailAsync(int groupId, string inviterUserId, string email, SharedGroupRole role, int expirationDays = 7);
    Task<List<SharedAutomatonGroupInvitation>> ListPendingInvitationsAsync(int groupId, string userId);
    Task<SharedAutomatonGroupInvitation?> GetInvitationByTokenAsync(string token);
    Task AcceptInvitationAsync(string token, string userId);
    Task DeclineInvitationAsync(string token);
    Task CancelInvitationAsync(int invitationId, string userId);
    
    // Shareable links
    Task<string> GenerateInviteLinkAsync(int groupId, string userId, SharedGroupRole defaultRole, int? expirationDays = null);
    Task<string?> GetInviteLinkAsync(int groupId, string userId);
    Task DeactivateInviteLinkAsync(int groupId, string userId);
    Task<SharedAutomatonGroup?> JoinViaInviteLinkAsync(string inviteCode, string userId);
    
    // Notifications (for future email integration)
    Task<string> GetInvitationEmailContentAsync(SharedAutomatonGroupInvitation invitation);
}
