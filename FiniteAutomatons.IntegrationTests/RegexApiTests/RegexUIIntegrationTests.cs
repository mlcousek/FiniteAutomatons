using Shouldly;
using System.Net;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.RegexApiTests;

[Collection("Integration Tests")]
public class RegexUIIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task GetPresets_ShouldReturnJsonArray()
    {
        var client = GetHttpClient();

        var response = await client.GetAsync("/Regex/GetPresets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldNotBeNullOrEmpty();

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetPresets_AllPresetsHaveRequiredFields()
    {
        var client = GetHttpClient();

        var response = await client.GetAsync("/Regex/GetPresets");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        foreach (var preset in doc.RootElement.EnumerateArray())
        {
            preset.TryGetProperty("key", out var key).ShouldBeTrue();
            key.GetString().ShouldNotBeNullOrWhiteSpace();

            preset.TryGetProperty("displayName", out var displayName).ShouldBeTrue();
            displayName.GetString().ShouldNotBeNullOrWhiteSpace();

            preset.TryGetProperty("pattern", out var pattern).ShouldBeTrue();
            pattern.GetString().ShouldNotBeNullOrWhiteSpace();

            preset.TryGetProperty("description", out var description).ShouldBeTrue();
            description.GetString().ShouldNotBeNullOrWhiteSpace();

            preset.TryGetProperty("acceptExamples", out var acceptExamples).ShouldBeTrue();
            acceptExamples.ValueKind.ShouldBe(JsonValueKind.Array);

            preset.TryGetProperty("rejectExamples", out var rejectExamples).ShouldBeTrue();
            rejectExamples.ValueKind.ShouldBe(JsonValueKind.Array);
        }
    }

    [Theory]
    [InlineData("simple-literal", "abc")]
    [InlineData("star-operator", "a*")]
    [InlineData("alternation", "a|b")]
    public async Task GetPresets_KnownKeys_ShouldHaveExpectedPattern(string key, string expectedPattern)
    {
        var client = GetHttpClient();

        var response = await client.GetAsync("/Regex/GetPresets");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var preset = doc.RootElement.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("key").GetString() == key);

        preset.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        preset.GetProperty("pattern").GetString().ShouldBe(expectedPattern);
    }
}

