using Shouldly;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.Seeding;

[Collection("Integration Tests")]
public class EmailPickupIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Register_SendsConfirmationEmail_ToPickupDirectory()
    {
        // Arrange
        var client = GetHttpClient();

        // Act: GET the register page first to obtain antiforgery token + cookies
        var get = await client.GetAsync("/Identity/Account/Register");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(html, "name=\"__RequestVerificationToken\".*?value=\"(?<t>.*?)\"");
        tokenMatch.Success.ShouldBeTrue("Antiforgery token not found on Register page");
        var token = tokenMatch.Groups["t"].Value;

        // register a fresh user
        var email = $"integ_test_{Guid.NewGuid():N}@test.local";

        var formData = new Dictionary<string, string>
        {
            { "__RequestVerificationToken", token },
            { "Input.Email", email },
            { "Input.Password", "Test123" },
            { "Input.ConfirmPassword", "Test123" }
        };

        var form = new FormUrlEncodedContent(formData);

        var res = await client.PostAsync("/Identity/Account/Register", form);
        res.EnsureSuccessStatusCode();

        // Give the app a moment to write the email file
        await Task.Delay(250);

        // Assert: an .eml file exists in the temp pickup directory and contains the recipient email
        var dir = fixture.TempEmailDirectory;
        Directory.Exists(dir).ShouldBeTrue($"Pickup directory '{dir}' not found");

        var files = Directory.GetFiles(dir, "*.eml");
        files.Length.ShouldBeGreaterThan(0, "No .eml files found in pickup directory");

        var found = files.Any(f => File.ReadAllText(f).Contains(email));
        found.ShouldBeTrue($"No .eml file contained the expected recipient {email}");
    }
}
