using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for managing group invitation notifications
/// </summary>
public interface IInvitationNotificationService
{
    /// <summary>
    /// Get pending invitations for a user by their registered email
    /// </summary>
    Task<List<SharedAutomatonGroupInvitation>> GetPendingInvitationsForUserAsync(string userId, string email);

    /// <summary>
    /// Check if user has enabled invitation notifications
    /// </summary>
    Task<bool> HasInvitationNotificationsEnabledAsync(string userId);

    /// <summary>
    /// Toggle invitation notification preference
    /// </summary>
    Task SetInvitationNotificationsAsync(string userId, bool enabled);
}
