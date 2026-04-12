using Microsoft.AspNetCore.Identity;

namespace FiniteAutomatons.Core.Models.Database;

public class ApplicationUser : IdentityUser
{
    public bool EnableInvitationNotifications { get; set; } = true;

    public string? PanelOrderPreferences { get; set; }

    public bool CanvasWheelZoomEnabled { get; set; } = false;

    public bool CanvasEditModeEnabled { get; set; } = false;

    public bool CanvasMoveEnabled { get; set; } = true;
}
