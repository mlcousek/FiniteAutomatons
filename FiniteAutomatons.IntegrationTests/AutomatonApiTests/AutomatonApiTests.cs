using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class AutomatonApiTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel GetDefaultDfaViewModel(string input)
    {
        return new AutomatonViewModel
        {
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = false },
                new() { Id = 4, IsStart = false, IsAccepting = false },
                new() { Id = 5, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new() { FromStateId = 1, ToStateId = 4, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 5, Symbol = 'b' },
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'c' },
                new() { FromStateId = 3, ToStateId = 4, Symbol = 'a' },
                new() { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
                new() { FromStateId = 3, ToStateId = 1, Symbol = 'c' },
                new() { FromStateId = 4, ToStateId = 5, Symbol = 'a' },
                new() { FromStateId = 4, ToStateId = 2, Symbol = 'b' },
                new() { FromStateId = 4, ToStateId = 4, Symbol = 'c' },
                new() { FromStateId = 5, ToStateId = 5, Symbol = 'a' },
                new() { FromStateId = 5, ToStateId = 5, Symbol = 'b' },
                new() { FromStateId = 5, ToStateId = 5, Symbol = 'c' }
            ],
            Input = input
        };
    }

    private static FormUrlEncodedContent ToFormContent(AutomatonViewModel model)
    {
        var dict = new List<KeyValuePair<string, string>>
        {
            new("Input", model.Input ?? ""),
            new("CurrentStateId", model.CurrentStateId?.ToString() ?? ""),
            new("Position", model.Position.ToString()),
            new("IsAccepted", model.IsAccepted?.ToString().ToLower() ?? ""),
            new("StateHistorySerialized", model.StateHistorySerialized ?? ""),
            new("HasExecuted", model.HasExecuted.ToString().ToLower()),
            new("Type", ((int)model.Type).ToString())
        };
        for (int i = 0; i < model.States.Count; i++)
        {
            dict.Add(new($"States.Index", i.ToString()));
            dict.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
            dict.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLower()));
            dict.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLower()));
        }
        for (int i = 0; i < model.Transitions.Count; i++)
        {
            dict.Add(new($"Transitions.Index", i.ToString()));
            dict.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
            dict.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
            dict.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol.ToString()));
        }
        if (model.Alphabet != null && model.Alphabet.Count > 0)
        {
            for (int i = 0; i < model.Alphabet.Count; i++)
            {
                dict.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
            }
        }
        return new FormUrlEncodedContent(dict);
    }

    private static void UpdateModelFromHtml(AutomatonViewModel model, string html)
    {
        // Extract CurrentStateId
        var currentStateMatch = Regex.Match(html, @"name=""CurrentStateId"" value=""([^""]*)""");
        if (currentStateMatch.Success && int.TryParse(currentStateMatch.Groups[1].Value, out int currentStateId))
        {
            model.CurrentStateId = currentStateId;
        }

        // Extract Position
        var positionMatch = Regex.Match(html, @"name=""Position"" value=""([^""]*)""");
        if (positionMatch.Success && int.TryParse(positionMatch.Groups[1].Value, out int position))
        {
            model.Position = position;
        }

        // Extract IsAccepted
        var isAcceptedMatch = Regex.Match(html, @"name=""IsAccepted"" value=""([^""]*)""");
        if (isAcceptedMatch.Success && bool.TryParse(isAcceptedMatch.Groups[1].Value, out bool isAccepted))
        {
            model.IsAccepted = isAccepted;
        }

        // Extract HasExecuted
        var hasExecutedMatch = Regex.Match(html, @"name=""HasExecuted"" value=""([^""]*)""");
        if (hasExecutedMatch.Success && bool.TryParse(hasExecutedMatch.Groups[1].Value, out bool hasExecuted))
        {
            model.HasExecuted = hasExecuted;
        }

        // Extract StateHistorySerialized
        var stateHistoryMatch = Regex.Match(html, @"name=""StateHistorySerialized"" value=""([^""]*)""");
        if (stateHistoryMatch.Success)
        {
            model.StateHistorySerialized = stateHistoryMatch.Groups[1].Value;
        }
    }

    [Fact]
    public async Task ExecuteAll_AcceptsInputLeadingToAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca"); // 1->2(a)->3(b)->4(c)->5(a), state 5 is accepting
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ToLowerInvariant().ShouldContain("accepted");
    }

    [Fact]
    public async Task ExecuteAll_RejectsInputNotLeadingToAccepting()
    {
        // Arrange
        _ = GetDefaultDfaViewModel("ab");
        AutomatonViewModel? model = GetDefaultDfaViewModel("a");

        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        html.Contains("rejected", StringComparison.InvariantCultureIgnoreCase).ShouldBeTrue($"Expected 'rejected' to appear in HTML, but it didn't. HTML snippet: {html[..Math.Min(500, html.Length)]}");
    }

    [Fact]
    public async Task StepForward_And_StepBackward_Works()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        var client = GetHttpClient();
        var form = ToFormContent(model);

        var response = await client.PostAsync("/AutomatonExecution/StepForward", form);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        html.ShouldContain("q2");

        UpdateModelFromHtml(model, html);

        form = ToFormContent(model);
        response = await client.PostAsync("/AutomatonExecution/StepForward", form);
        html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("q5");

        UpdateModelFromHtml(model, html);

        form = ToFormContent(model);
        response = await client.PostAsync("/AutomatonExecution/StepBackward", form);
        html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("q2", Case.Insensitive);
    }

    [Fact]
    public async Task BackToStart_ResetsState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        model.CurrentStateId = 3;
        model.Position = 2;
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/AutomatonExecution/BackToStart", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Current State:");
        html.ShouldContain("q1");
        html.ShouldContain("Current Position:");
        html.ShouldContain("0 /"); // Position 0 out of total
    }

    [Fact]
    public async Task Reset_ClearsInputAndState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        model.CurrentStateId = 3;
        model.Position = 2;
        model.HasExecuted = true;
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/AutomatonExecution/Reset", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("INPUT");
        html.ShouldNotContain("execution-state-item");
    }

    [Fact]
    public async Task ExecuteAll_LongInput_LeadsToAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abcaabcaabca"); // Should end in state 5 (accepting)
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ToLowerInvariant().ShouldContain("accepted");
    }

    [Fact]
    public async Task ExecuteAll_EmptyInput_ShouldReject()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("");
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Rejected", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAll_InputWithAllSymbols()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abcabcabc");
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (html.Contains("accepted", StringComparison.InvariantCultureIgnoreCase) || html.Contains("rejected", StringComparison.InvariantCultureIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAll_OnlyOneSymbol_NotAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("a"); // 1->2, not accepting
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Rejected", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAll_LoopInAcceptingState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abcaaaa"); // 1->2(a)->3(b)->4(c)->5(a)->5(a)->5(a)->5(a)
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ToLowerInvariant().ShouldContain("accepted");
    }

    [Fact]
    public async Task Stepwise_MultipleActions_ForwardBackwardExecuteAllReset()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        var client = GetHttpClient();
        var form = ToFormContent(model);

        var response = await client.PostAsync("/AutomatonExecution/StepForward", form);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("q2");
        UpdateModelFromHtml(model, html);

        form = ToFormContent(model);
        response = await client.PostAsync("/AutomatonExecution/StepForward", form);
        html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("q5");
        UpdateModelFromHtml(model, html);

        form = ToFormContent(model);
        response = await client.PostAsync("/AutomatonExecution/StepBackward", form);
        html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("q2", Case.Insensitive);
        UpdateModelFromHtml(model, html);

        form = ToFormContent(model);
        response = await client.PostAsync("/AutomatonExecution/ExecuteAll", form);
        html = await response.Content.ReadAsStringAsync();
        var containsAccepted = html.Contains("accepted", StringComparison.InvariantCultureIgnoreCase);
        containsAccepted.ShouldBeTrue("Expected 'accepted' in HTML");

        response = await client.PostAsync("/AutomatonExecution/Reset", form);
        html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("INPUT");
        html.ShouldNotContain("execution-state-item");
    }
}
