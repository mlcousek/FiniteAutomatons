using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace FiniteAutomatons.UnitTests.Controllers;

public class UserPreferencesControllerTests
{
    private sealed class FakeUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName?.ToUpperInvariant());
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string>(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    }

    private class TestUserManager : UserManager<ApplicationUser>
    {
        private readonly ApplicationUser? _userToReturn;

        public TestUserManager(ApplicationUser? user)
            : base(new FakeUserStore(),
                  Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                  new PasswordHasher<ApplicationUser>(),
                  Array.Empty<IUserValidator<ApplicationUser>>(),
                  Array.Empty<IPasswordValidator<ApplicationUser>>(),
                  new UpperInvariantLookupNormalizer(),
                  new IdentityErrorDescriber(),
                  services: null!,
                  logger: new NullLogger<UserManager<ApplicationUser>>())
        {
            _userToReturn = user;
        }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            return Task.FromResult(_userToReturn);
        }

        public override Task<IdentityResult> UpdateAsync(ApplicationUser user)
        {
            return Task.FromResult(IdentityResult.Success);
        }
    }

    [Fact]
    public async Task GetPanelOrderPreferences_ReturnsUserPreferences()
    {
        var prefs = "{ \"automatonSidebar\": [ \"alphabet\" ] }";
        var user = new ApplicationUser { Id = "u1", UserName = "test", PanelOrderPreferences = prefs };
        var controller = new UserPreferencesController(new TestUserManager(user));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetPanelOrderPreferences();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var value = okResult.Value;
        string? returnedPrefs = (string?)value?.GetType()?.GetProperty("preferences")?.GetValue(value, null);
        returnedPrefs.ShouldBe(prefs);
    }

    [Fact]
    public async Task GetPanelOrderPreferences_NoPreferencesForUser_ReturnsEmpty()
    {
        var user = new ApplicationUser { Id = "u2", UserName = "test" }; // Null preferences
        var controller = new UserPreferencesController(new TestUserManager(user));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetPanelOrderPreferences();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var value = okResult.Value;
        string? returnedPrefs = (string?)value?.GetType()?.GetProperty("preferences")?.GetValue(value, null);
        returnedPrefs.ShouldBeNull();
    }

    [Fact]
    public async Task SavePanelOrderPreferences_UpdatesUserAndReturnsOk()
    {
        var user = new ApplicationUser { Id = "u3", UserName = "test", PanelOrderPreferences = null };
        var controller = new UserPreferencesController(new TestUserManager(user));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new PanelOrderRequest
        {
            Preferences = "{ \"a\": 1 }"
        };

        var result = await controller.SavePanelOrderPreferences(request);

        result.ShouldBeOfType<OkResult>();

        user.PanelOrderPreferences.ShouldBe("{ \"a\": 1 }");
    }

    [Fact]
    public async Task SavePanelOrderPreferences_UserNotFound_ReturnsUnauthorized()
    {
        var controller = new UserPreferencesController(new TestUserManager(null));

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new PanelOrderRequest
        {
            Preferences = "{}"
        };

        var result = await controller.SavePanelOrderPreferences(request);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCanvasWheelPreference_ReturnsEnabledValue()
    {
        var user = new ApplicationUser { Id = "u4", UserName = "test", CanvasWheelZoomEnabled = true };
        var controller = new UserPreferencesController(new TestUserManager(user));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCanvasWheelPreference();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var value = okResult.Value;
        bool returnedEnabled = (bool)(value?.GetType()?.GetProperty("enabled")?.GetValue(value, null) ?? false);
        returnedEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveCanvasWheelPreference_UpdatesUserAndReturnsOk()
    {
        var user = new ApplicationUser { Id = "u5", UserName = "test", CanvasWheelZoomEnabled = false };
        var controller = new UserPreferencesController(new TestUserManager(user));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CanvasWheelRequest
        {
            Enabled = true
        };

        var result = await controller.SaveCanvasWheelPreference(request);

        result.ShouldBeOfType<OkResult>();

        user.CanvasWheelZoomEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveCanvasWheelPreference_UserNotFound_ReturnsUnauthorized()
    {
        var controller = new UserPreferencesController(new TestUserManager(null));

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CanvasWheelRequest
        {
            Enabled = true
        };

        var result = await controller.SaveCanvasWheelPreference(request);

        result.ShouldBeOfType<UnauthorizedResult>();
    }
}
