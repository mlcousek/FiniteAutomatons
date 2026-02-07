using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FiniteAutomatons.Services.Services;

public class InvitationNotificationService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : IInvitationNotificationService
{
    private readonly ApplicationDbContext context = context;
    private readonly UserManager<ApplicationUser> userManager = userManager;

    public async Task<List<SharedAutomatonGroupInvitation>> GetPendingInvitationsForUserAsync(
        string userId, string email)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(email);

        return await context.SharedAutomatonGroupInvitations
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending)
            .Include(i => i.Group)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> HasInvitationNotificationsEnabledAsync(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return false;

        return user.EnableInvitationNotifications;
    }

    public async Task SetInvitationNotificationsAsync(string userId, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var user = await userManager.FindByIdAsync(userId);
        ArgumentNullException.ThrowIfNull(user);

        user.EnableInvitationNotifications = enabled;
        await userManager.UpdateAsync(user);
    }
}
