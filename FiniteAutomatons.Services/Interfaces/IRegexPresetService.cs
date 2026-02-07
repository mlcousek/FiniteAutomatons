namespace FiniteAutomatons.Services.Interfaces;

public interface IRegexPresetService
{
    IEnumerable<RegexPreset> GetAllPresets();
    RegexPreset? GetPresetByKey(string key);
}

public record RegexPreset(
    string Key,
    string DisplayName,
    string Pattern,
    string Description,
    string[] AcceptExamples,
    string[] RejectExamples);
