//using FiniteAutomatons.Core.Models.ViewModel;
//using Shouldly;
//using System.Net;
//using System.Text.RegularExpressions;

//namespace FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonConvertion;

//[Collection("Integration Tests")]
//public class AutomatonConversionTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
//{
//    [Fact]
//    public async Task ConvertToDFA_FromNFA_CreatesEquivalentDFA()
//    {
//        // Arrange
//        var client = GetHttpClient();
//        var nfaModel = new AutomatonViewModel
//        {
//            Type = AutomatonType.NFA,
//            States =
//            [
//                new() { Id = 1, IsStart = true, IsAccepting = false },
//                new() { Id = 2, IsStart = false, IsAccepting = true }
//            ],
//            Transitions =
//            [
//                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' },
//                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
//            ]
//        };

//        // Act
//        var response = await PostConversionAsync(client, nfaModel);

//        // Assert
//        response.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await response.Content.ReadAsStringAsync();

//        // Verify DFA type in button
//        html.ShouldContain("data-type=\"DFA\"");

//        // Verify states were created
//        html.ShouldContain("data-state-id=");
//    }

//    [Fact]
//    public async Task ConvertToDFA_FromEpsilonNFA_HandlesEpsilonTransitions()
//    {
//        // Arrange
//        var client = GetHttpClient();
//        var enfaModel = new AutomatonViewModel
//        {
//            Type = AutomatonType.EpsilonNFA,
//            States =
//            [
//                new() { Id = 1, IsStart = true, IsAccepting = false },
//                new() { Id = 2, IsStart = false, IsAccepting = false },
//                new() { Id = 3, IsStart = false, IsAccepting = true }
//            ],
//            Transitions =
//            [
//                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
//                new() { FromStateId = 2, ToStateId = 3, Symbol = 'a' }
//            ]
//        };

//        // Act
//        var response = await PostConversionAsync(client, enfaModel);

//        // Assert
//        response.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await response.Content.ReadAsStringAsync();

//        // Should have created DFA
//        html.ShouldContain("data-type=\"DFA\"");
//    }

//    [Fact]
//    public async Task ConvertToDFA_FromDFA_ReturnsMinimizedDFA()
//    {
//        // Arrange
//        var client = GetHttpClient();
//        var dfaModel = new AutomatonViewModel
//        {
//            Type = AutomatonType.DFA,
//            States =
//            [
//                new() { Id = 1, IsStart = true, IsAccepting = false },
//                new() { Id = 2, IsStart = false, IsAccepting = true },
//                new() { Id = 3, IsStart = false, IsAccepting = true }
//            ],
//            Transitions =
//            [
//                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
//                new() { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
//                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
//                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' },
//                new() { FromStateId = 3, ToStateId = 3, Symbol = 'a' },
//                new() { FromStateId = 3, ToStateId = 3, Symbol = 'b' }
//            ]
//        };

//        // Act
//        var response = await PostConversionAsync(client, dfaModel);

//        // Assert
//        response.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await response.Content.ReadAsStringAsync();

//        // Should still be DFA
//        html.ShouldContain("data-type=\"DFA\"");

//        // After minimization, should have fewer or equal states
//        var stateMatches = Regex.Matches(html, @"data-state-id=""(\d+)""");
//        stateMatches.Count.ShouldBeLessThanOrEqualTo(3);
//    }

//    [Fact]
//    public async Task ConvertedAutomaton_AcceptsSameLanguage()
//    {
//        // Arrange
//        var client = GetHttpClient();
//        var nfaModel = new AutomatonViewModel
//        {
//            Type = AutomatonType.NFA,
//            States =
//            [
//                new() { Id = 1, IsStart = true, IsAccepting = false },
//                new() { Id = 2, IsStart = false, IsAccepting = true }
//            ],
//            Transitions =
//            [
//                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' },
//                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
//            ],
//            Input = "a"
//        };

//        // Act 1 - Test NFA accepts "a"
//        var nfaResponse = await PostExecutionAsync(client, nfaModel);
//        nfaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var nfaHtml = await nfaResponse.Content.ReadAsStringAsync();
//        var nfaAccepted = Regex.IsMatch(nfaHtml, @"<input[^>]*\bname=""IsAccepted""[^>]*\bvalue=""true""", RegexOptions.IgnoreCase);

//        // Act 2 - Convert to DFA
//        var dfaResponse = await PostConversionAsync(client, nfaModel);
//        dfaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var dfaHtml = await dfaResponse.Content.ReadAsStringAsync();

//        // Parse DFA model
//        var dfaModel = ParseAutomatonFromHtml(dfaHtml);
//        dfaModel.Input = "a";

//        // Act 3 - Test DFA accepts "a"
//        var dfaExecResponse = await PostExecutionAsync(client, dfaModel);
//        dfaExecResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var dfaExecHtml = await dfaExecResponse.Content.ReadAsStringAsync();
//        var dfaAccepted = Regex.IsMatch(dfaExecHtml, @"<input[^>]*\bname=""IsAccepted""[^>]*\bvalue=""true""", RegexOptions.IgnoreCase);

//        // Assert - Both should accept the same input
//        nfaAccepted.ShouldBe(dfaAccepted);
//    }

//    // Helper methods
//    private static async Task<HttpResponseMessage> PostConversionAsync(HttpClient client, AutomatonViewModel model)
//    {
//        var formData = BuildFormData(model);
//        // SwitchType expects a targetType field; post to SwitchType endpoint
//        formData.Add(new KeyValuePair<string, string>("targetType", ((int)AutomatonType.DFA).ToString()));
//        var content = new FormUrlEncodedContent(formData);
//        return await client.PostAsync("/Automaton/SwitchType", content);
//    }

//    private static async Task<HttpResponseMessage> PostExecutionAsync(HttpClient client, AutomatonViewModel model)
//    {
//        var formData = BuildFormData(model);
//        var content = new FormUrlEncodedContent(formData);
//        return await client.PostAsync("/AutomatonExecution/ExecuteAll", content);
//    }

//    private static List<KeyValuePair<string, string>> BuildFormData(AutomatonViewModel model)
//    {
//        var formData = new List<KeyValuePair<string, string>>
//        {
//            new("Type", ((int)model.Type).ToString()),
//            new("Input", model.Input ?? ""),
//            new("Position", model.Position.ToString()),
//            new("HasExecuted", model.HasExecuted.ToString().ToLower()),
//            new("IsCustomAutomaton", "true")
//        };

//        if (model.CurrentStateId.HasValue)
//            formData.Add(new("CurrentStateId", model.CurrentStateId.Value.ToString()));

//        for (int i = 0; i < model.States.Count; i++)
//        {
//            formData.Add(new($"States.Index", i.ToString()));
//            formData.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
//            formData.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLower()));
//            formData.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLower()));
//        }

//        for (int i = 0; i < model.Transitions.Count; i++)
//        {
//            formData.Add(new($"Transitions.Index", i.ToString()));
//            formData.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
//            formData.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
//            formData.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol.ToString()));
//        }

//        return formData;
//    }

//    private static AutomatonViewModel ParseAutomatonFromHtml(string html)
//    {
//        var model = new AutomatonViewModel
//        {
//            States = new List<Core.Models.DoMain.State>(),
//            Transitions = new List<Core.Models.DoMain.Transition>()
//        };

//        // Parse Type - more flexible pattern
//        var typeMatch = Regex.Match(html, @"name\s*=\s*""Type""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Type""", RegexOptions.IgnoreCase);
//        if (typeMatch.Success)
//            model.Type = (AutomatonType)int.Parse(typeMatch.Groups[1].Success ? typeMatch.Groups[1].Value : typeMatch.Groups[2].Value);

//        // Parse States
//        var stateIdMatches = Regex.Matches(html, @"name\s*=\s*""States\[(\d+)\]\.Id""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""States\[(\d+)\]\.Id""", RegexOptions.IgnoreCase);
//        var stateStartMatches = Regex.Matches(html, @"name\s*=\s*""States\[\d+\]\.IsStart""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""States\[\d+\]\.IsStart""", RegexOptions.IgnoreCase);
//        var stateAcceptMatches = Regex.Matches(html, @"name\s*=\s*""States\[\d+\]\.IsAccepting""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""States\[\d+\]\.IsAccepting""", RegexOptions.IgnoreCase);

//        for (int i = 0; i < stateIdMatches.Count; i++)
//        {
//            var match = stateIdMatches[i];
//            var idValue = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

//            model.States.Add(new Core.Models.DoMain.State
//            {
//                Id = int.Parse(idValue),
//                IsStart = i < stateStartMatches.Count && bool.Parse(stateStartMatches[i].Groups[1].Success ? stateStartMatches[i].Groups[1].Value : stateStartMatches[i].Groups[2].Value),
//                IsAccepting = i < stateAcceptMatches.Count && bool.Parse(stateAcceptMatches[i].Groups[1].Success ? stateAcceptMatches[i].Groups[1].Value : stateAcceptMatches[i].Groups[2].Value)
//            });
//        }

//        // Parse Transitions
//        var transFromMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.FromStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.FromStateId""", RegexOptions.IgnoreCase);
//        var transToMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.ToStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.ToStateId""", RegexOptions.IgnoreCase);
//        var transSymbolMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.Symbol""[^>]*value\s*=\s*""(.)""|value\s*=\s*""(.)""[^>]*name\s*=\s*""Transitions\[\d+\]\.Symbol""", RegexOptions.IgnoreCase);

//        for (int i = 0; i < transFromMatches.Count && i < transToMatches.Count; i++)
//        {
//            var fromValue = transFromMatches[i].Groups[1].Success ? transFromMatches[i].Groups[1].Value : transFromMatches[i].Groups[2].Value;
//            var toValue = transToMatches[i].Groups[1].Success ? transToMatches[i].Groups[1].Value : transToMatches[i].Groups[2].Value;
//            char symbol = '\0';
//            if (i < transSymbolMatches.Count)
//            {
//                var symbolValue = transSymbolMatches[i].Groups[1].Success ? transSymbolMatches[i].Groups[1].Value : transSymbolMatches[i].Groups[2].Value;
//                if (!string.IsNullOrEmpty(symbolValue))
//                    symbol = symbolValue[0];
//            }

//            model.Transitions.Add(new Core.Models.DoMain.Transition
//            {
//                FromStateId = int.Parse(fromValue),
//                ToStateId = int.Parse(toValue),
//                Symbol = symbol
//            });
//        }

//        return model;
//    }
//}
