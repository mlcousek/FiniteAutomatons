using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class RegexPresetServiceTests
{
    private readonly IRegexPresetService service;

    public RegexPresetServiceTests(IRegexPresetService service)
    {
        this.service = service;
    }

    public RegexPresetServiceTests()
    {
        service = new RegexPresetService();
    }

    [Fact]
    public void GetAllPresets_ShouldReturnNonEmpty()
    {
        var presets = service.GetAllPresets();

        presets.ShouldNotBeEmpty();
        presets.Count().ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetAllPresets_AllPresetsHaveValidData()
    {
        var presets = service.GetAllPresets();

        foreach (var preset in presets)
        {
            preset.Key.ShouldNotBeNullOrWhiteSpace();
            preset.DisplayName.ShouldNotBeNullOrWhiteSpace();
            preset.Pattern.ShouldNotBeNullOrWhiteSpace();
            preset.Description.ShouldNotBeNullOrWhiteSpace();
            preset.AcceptExamples.ShouldNotBeNull();
            preset.RejectExamples.ShouldNotBeNull();
        }
    }

    [Fact]
    public void GetPresetByKey_ValidKey_ShouldReturnPreset()
    {
        var preset = service.GetPresetByKey("simple-literal");

        preset.ShouldNotBeNull();
        preset.Pattern.ShouldBe("abc");
        preset.DisplayName.ShouldBe("Simple Literal");
    }

    [Fact]
    public void GetPresetByKey_InvalidKey_ShouldReturnNull()
    {
        var preset = service.GetPresetByKey("nonexistent-key");

        preset.ShouldBeNull();
    }

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void GetPresetByKey_AllKnownKeys_ShouldReturnPreset(string key)
    {
        var preset = service.GetPresetByKey(key);

        preset.ShouldNotBeNull();
        preset.Key.ShouldBe(key);
    }

    [Fact]
    public void GetAllPresets_AllKeysAreUnique()
    {
        var presets = service.GetAllPresets();
        var keys = presets.Select(p => p.Key).ToList();

        keys.Count.ShouldBe(keys.Distinct().Count());
    }

    [Fact]
    public void Presets_ShouldHaveExamplesForAcceptAndReject()
    {
        var presets = service.GetAllPresets();

        foreach (var preset in presets)
        {
            preset.AcceptExamples.ShouldNotBeEmpty($"Preset '{preset.Key}' should have accept examples");
            preset.RejectExamples.ShouldNotBeEmpty($"Preset '{preset.Key}' should have reject examples");
        }
    }
}
