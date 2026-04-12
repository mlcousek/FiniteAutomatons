using FiniteAutomatons.Core.Models.Api;
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
    private readonly UserManager<ApplicationUser> userManager = userManager;

    [HttpGet("panel-order")]
    public async Task<IActionResult> GetPanelOrderPreferences()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { preferences = user.PanelOrderPreferences });
    }

    [HttpPost("panel-order")]
    public async Task<IActionResult> SavePanelOrderPreferences([FromBody] PanelOrderRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        user.PanelOrderPreferences = request.Preferences;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest(result.Errors);
    }

    [HttpGet("canvas-wheel")]
    public async Task<IActionResult> GetCanvasWheelPreference()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { enabled = user.CanvasWheelZoomEnabled });
    }

    [HttpPost("canvas-wheel")]
    public async Task<IActionResult> SaveCanvasWheelPreference([FromBody] CanvasWheelRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        user.CanvasWheelZoomEnabled = request.Enabled;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest(result.Errors);
    }

    [HttpGet("canvas-edit-mode")]
    public async Task<IActionResult> GetCanvasEditModePreference()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { enabled = user.CanvasEditModeEnabled });
    }

    [HttpPost("canvas-edit-mode")]
    public async Task<IActionResult> SaveCanvasEditModePreference([FromBody] CanvasToggleRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        user.CanvasEditModeEnabled = request.Enabled;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest(result.Errors);
    }

    [HttpGet("canvas-move")]
    public async Task<IActionResult> GetCanvasMovePreference()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { enabled = user.CanvasMoveEnabled });
    }

    [HttpPost("canvas-move")]
    public async Task<IActionResult> SaveCanvasMovePreference([FromBody] CanvasToggleRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        user.CanvasMoveEnabled = request.Enabled;
        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Ok();
        }

        return BadRequest(result.Errors);
    }
}
