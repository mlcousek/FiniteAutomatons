// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using FiniteAutomatons.Core.Models.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace FiniteAutomatons.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _sender;
        private readonly IConfiguration _configuration;

        public RegisterConfirmationModel(UserManager<ApplicationUser> userManager, IEmailSender sender, IConfiguration configuration)
        {
            _userManager = userManager;
            _sender = sender;
            _configuration = configuration;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public bool DisplayConfirmAccountLink { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string EmailConfirmationUrl { get; set; }

        // Indicates that the app is configured to write emails to a pickup directory (development mode)
        public bool IsPickupDirectoryConfigured { get; set; }

        // Resolved absolute path to the pickup directory when configured
        public string PickupDirectoryPath { get; set; }

        public async Task<IActionResult> OnGetAsync(string email, string returnUrl = null)
        {
            if (email == null)
            {
                return RedirectToPage("/Index");
            }
            returnUrl = returnUrl ?? Url.Content("~/");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound($"Unable to load user with email '{email}'.");
            }

            Email = email;
            // Once you add a real email sender, you should remove this code that lets you confirm the account
            DisplayConfirmAccountLink = true;
            // Determine pickup directory path for developer feedback when UsePickupDirectory is enabled
            try
            {
                var usePickup = _configuration.GetValue<bool>("Smtp:UsePickupDirectory");
                IsPickupDirectoryConfigured = usePickup;
                if (usePickup)
                {
                    var configured = _configuration.GetValue<string>("Smtp:PickupDirectory");
                    var pickup = string.IsNullOrWhiteSpace(configured)
                        ? Path.Combine(AppContext.BaseDirectory, "emails")
                        : configured!;
                    if (!Path.IsPathRooted(pickup))
                    {
                        pickup = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, pickup));
                    }
                    PickupDirectoryPath = pickup;
                }
            }
            catch
            {
                // ignore configuration errors; this is non-critical helper for developer UX
                IsPickupDirectoryConfigured = false;
                PickupDirectoryPath = string.Empty;
            }
            if (DisplayConfirmAccountLink)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                EmailConfirmationUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme);
            }

            return Page();
        }
    }
}
