using Microsoft.AspNetCore.Identity;

namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Extended identity user with additional notification preferences
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Whether the user wants to receive invitation notifications on login
    /// </summary>
    public bool EnableInvitationNotifications { get; set; } = true;
}
