using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.Services.Interfaces;

public interface IInvitationNotificationService
{
    Task<List<SharedAutomatonGroupInvitation>> GetPendingInvitationsForUserAsync(string userId, string email);
    Task<bool> HasInvitationNotificationsEnabledAsync(string userId);
    Task SetInvitationNotificationsAsync(string userId, bool enabled);
}
