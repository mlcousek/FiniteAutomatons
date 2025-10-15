using System.Net;
using System.Text;
using System.Text.Json;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.RegexApiTests;

[Collection("Integration Tests")]
public class AutomatonControllerBuildFromRegexTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Controller_PostBuildFromRegex_StoresTempDataAndRedirects()
    {
        var client = GetHttpClient();

        // GET the RegexToAutomaton UI to obtain antiforgery token
        var getResp = await client.GetAsync("/Automaton/RegexToAutomaton");
        getResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await getResp.Content.ReadAsStringAsync();
        html.ShouldContain("Regular expression");

        // Extract antiforgery token from hidden input using verbatim string for readability
        var match = Regex.Match(html, @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""(?<val>[^""]+)""", RegexOptions.IgnoreCase);
        string token = match.Success ? match.Groups["val"].Value : string.Empty;

        var formPairs = new List<KeyValuePair<string, string>>
        {
            new("regex", "a*b*c*")
        };
        if (!string.IsNullOrEmpty(token))
        {
            formPairs.Add(new("__RequestVerificationToken", token));
        }

        var resp = await client.PostAsync("/Automaton/BuildFromRegex", new FormUrlEncodedContent(formPairs));

        var text = await resp.Content.ReadAsStringAsync();

        resp.StatusCode.ShouldBe(HttpStatusCode.OK, text);

        JsonDocument.Parse(text).RootElement.TryGetProperty("success", out var succ).ShouldBeTrue();
        succ.GetBoolean().ShouldBeTrue();

        var obj = JsonDocument.Parse(text).RootElement;
        var redirect = obj.GetProperty("redirect").GetString() ?? "/";

        var homeResp = await client.GetAsync(redirect);
        homeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var homeHtml = await homeResp.Content.ReadAsStringAsync();

        homeHtml.ShouldContain("Converted regex to automaton and loaded into simulator");
    }
}
