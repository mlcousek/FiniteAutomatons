using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FiniteAutomatons.Areas.Identity.Pages.Account.Manage;

public class NotificationsModel(
    UserManager<ApplicationUser> userManager,
    IInvitationNotificationService notificationService) : PageModel
{
    private readonly UserManager<ApplicationUser> userManager = userManager;
    private readonly IInvitationNotificationService notificationService = notificationService;

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public bool EnableInvitationNotifications { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        EnableInvitationNotifications = user.EnableInvitationNotifications;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        try
        {
            await notificationService.SetInvitationNotificationsAsync(user.Id, EnableInvitationNotifications);
            StatusMessage = "Your notification preferences have been updated successfully.";
            return RedirectToPage();
        }
        catch (Exception)
        {
            StatusMessage = "Error: Failed to update notification preferences.";
            return RedirectToPage();
        }
    }
}
