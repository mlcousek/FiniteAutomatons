using System.Net;
using System.Text;
using FiniteAutomatons.Core.Models.DoMain;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;

namespace FiniteAutomatons.IntegrationTests.RegexApiTests;

[Collection("Integration Tests")]
public class RegexToAutomatonApiTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task PostRegex_BuildsAutomatonAndReturnsJson()
    {
        var client = GetHttpClient();
        var regex = "(a|b)*c";
        var content = new StringContent(regex, Encoding.UTF8, "text/plain");
        var response = await client.PostAsync("/_tests/build-from-regex", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("States");
        json.ShouldContain("Transitions");
    }

    [Fact]
    public async Task PostRegex_InvalidRegex_ReturnsBadRequest()
    {
        var client = GetHttpClient();
        var regex = "(a|b"; // missing closing parenthesis
        var content = new StringContent(regex, Encoding.UTF8, "text/plain");
        var response = await client.PostAsync("/_tests/build-from-regex", content);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Mismatched");
    }

    [Fact]
    public async Task Integration_EndToEnd_RegexAcceptance()
    {
        var client = GetHttpClient();
        var regex = "a+b?";
        var response = await client.PostAsync("/_tests/build-from-regex", new StringContent(regex, Encoding.UTF8, "text/plain"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.ShouldContain("States");

        // Use service directly via DI to validate acceptance for some strings
        using var scope = GetServiceScope();
        var services = scope.ServiceProvider;
        var regexService = services.GetRequiredService<FiniteAutomatons.Services.Interfaces.IRegexToAutomatonService>();
        var enfa = regexService.BuildEpsilonNfaFromRegex(regex);
        enfa.Execute("a").ShouldBeTrue();
        enfa.Execute("aa").ShouldBeTrue();
        enfa.Execute("ab").ShouldBeTrue();
        enfa.Execute("").ShouldBeFalse();
    }
}
