using FiniteAutomatons.Core.Models.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

[Authorize]
[ApiController]
[Route("api/preferences")]
public class UserPreferencesController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    [HttpGet("panel-order")]
    public async Task<IActionResult> GetPanelOrderPreferences()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { preferences = user.PanelOrderPreferences });
    }

    [HttpPost("panel-order")]
    public async Task<IActionResult> SavePanelOrderPreferences([FromBody] PanelOrderRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        user.PanelOrderPreferences = request.Preferences;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest(result.Errors);
    }
}

public class PanelOrderRequest
{
    public string Preferences { get; set; } = string.Empty;
}
