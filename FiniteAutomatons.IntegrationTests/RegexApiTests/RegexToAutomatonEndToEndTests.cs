using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.RegexApiTests;

[Collection("Integration Tests")]
public class RegexToAutomatonEndToEndTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Theory]
    [InlineData("a*b*c*", "aaabbbccc", true)]
    [InlineData("a*b*c*", "ac", true)]
    [InlineData("a*b*c*", "abab", false)]
    public async Task BuildFromRegex_ThenExecuteAll_ShouldMatchExpected(string regex, string input, bool shouldAccept)
    {
        var client = GetHttpClient();

        // Call dev endpoint to build automaton from regex
        var content = new StringContent(regex, Encoding.UTF8, "text/plain");
        var resp = await client.PostAsync("/_tests/build-from-regex", content);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await resp.Content.ReadAsStringAsync();
        json.ShouldNotBeNullOrEmpty();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement statesElem = GetPropertyIgnoreCase(root, "states");
        JsonElement transElem = GetPropertyIgnoreCase(root, "transitions");

        // Build AutomatonViewModel from returned description
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = new List<FiniteAutomatons.Core.Models.DoMain.State>(),
            Transitions = new List<FiniteAutomatons.Core.Models.DoMain.Transition>(),
            Input = input,
            IsCustomAutomaton = true
        };

        foreach (var s in statesElem.EnumerateArray())
        {
            int id = GetPropertyIgnoreCase(s, "id").GetInt32();
            bool isStart = TryGetBooleanIgnoreCase(s, "IsStart");
            bool isAccept = TryGetBooleanIgnoreCase(s, "IsAccepting");

            model.States.Add(new FiniteAutomatons.Core.Models.DoMain.State
            {
                Id = id,
                IsStart = isStart,
                IsAccepting = isAccept
            });
        }

        foreach (var t in transElem.EnumerateArray())
        {
            int from = GetPropertyIgnoreCase(t, "fromStateId").GetInt32();
            int to = GetPropertyIgnoreCase(t, "toStateId").GetInt32();

            string? sym = null;
            if (TryGetPropertyIgnoreCase(t, "symbol", out var symProp))
            {
                if (symProp.ValueKind == JsonValueKind.Null) sym = null;
                else sym = symProp.GetString();
            }

            char ch;
            if (string.IsNullOrEmpty(sym) || sym == "?" || sym == "?" || sym == "\\0") ch = '\0';
            else ch = sym[0];

            model.Transitions.Add(new FiniteAutomatons.Core.Models.DoMain.Transition
            {
                FromStateId = from,
                ToStateId = to,
                Symbol = ch
            });
        }

        // Post to ExecuteAll endpoint
        var form = ToFormContent(model);
        var executeResp = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        executeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await executeResp.Content.ReadAsStringAsync();

        if (shouldAccept)
            html.ShouldContain("Accepted");
        else
            html.ShouldContain("Rejected");
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement parent, string name)
    {
        // Try exact
        if (parent.TryGetProperty(name, out var e)) return e;
        // try different casing
        foreach (var prop in parent.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) return prop.Value;
        }
        // helpful error
        throw new KeyNotFoundException($"Property '{name}' not found in JSON response. Available properties: {string.Join(",", parent.EnumerateObject().Select(p => p.Name))}");
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement parent, string name, out JsonElement element)
    {
        if (parent.TryGetProperty(name, out var e)) { element = e; return true; }
        foreach (var prop in parent.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                element = prop.Value;
                return true;
            }
        }
        element = default;
        return false;
    }

    private static bool TryGetBooleanIgnoreCase(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p)) return p.GetBoolean();
        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) return prop.Value.GetBoolean();
        }
        return false;
    }

    private static FormUrlEncodedContent ToFormContent(AutomatonViewModel model)
    {
        var dict = new List<KeyValuePair<string, string>>
        {
            new("Input", model.Input ?? ""),
            new("CurrentStateId", model.CurrentStateId?.ToString() ?? ""),
            new("Position", model.Position.ToString()),
            new("IsAccepted", model.IsAccepted?.ToString().ToLower() ?? ""),
            new("StateHistorySerialized", model.StateHistorySerialized ?? "")
        };
        for (int i = 0; i < model.States.Count; i++)
        {
            dict.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
            dict.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLower()));
            dict.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLower()));
        }
        for (int i = 0; i < model.Transitions.Count; i++)
        {
            dict.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
            dict.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
            dict.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol == '\0' ? "\\0" : model.Transitions[i].Symbol.ToString()));
        }
        for (int i = 0; i < model.Alphabet.Count; i++)
        {
            dict.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
        }
        dict.Add(new("Type", model.Type.ToString()));
        dict.Add(new("IsCustomAutomaton", (model.IsCustomAutomaton).ToString().ToLower()));

        return new FormUrlEncodedContent(dict);
    }
}
