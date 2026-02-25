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

    /// <summary>
    /// JSON serialized dictionary of panel layouts and ordering preferences
    /// </summary>
    public string? PanelOrderPreferences { get; set; }

    /// <summary>
    /// Whether the user has enabled mouse-wheel zoom on the canvas
    /// </summary>
    public bool CanvasWheelZoomEnabled { get; set; } = false;
}
