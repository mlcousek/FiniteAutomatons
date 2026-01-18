using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonGeneration;
using FiniteAutomatons.Observability;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class AuditIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task GenerateRandomAutomaton_Post_EmitsGeneratorAudit()
    {
        var client = GetHttpClient();

        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", AutomatonType.DFA.ToString()),
            new("StateCount", "5"),
            new("TransitionCount", "8"),
            new("AlphabetSize", "3"),
            new("AcceptingStateRatio", "0.3")
        };

        var response = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);

        using var scope = GetServiceScope();
        var audit = scope.ServiceProvider.GetRequiredService<InMemoryAuditService>();
        var start = audit.GetByEventType("MethodStart").FirstOrDefault(r => r.Message == "IAutomatonGeneratorService.GenerateRandomAutomaton");
        var end = audit.GetByEventType("MethodEnd").FirstOrDefault(r => r.Message == "IAutomatonGeneratorService.GenerateRandomAutomaton");

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAll_Post_EmitsExecutionAudit()
    {
        var client = GetHttpClient();

        var dfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ],
        };

        var response = await PostAutomatonForm(client, "/AutomatonExecution/ExecuteAll", dfaModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = GetServiceScope();
        var audit = scope.ServiceProvider.GetRequiredService<InMemoryAuditService>();
        var start = audit.GetByEventType("MethodStart").FirstOrDefault(r => r.Message == "IAutomatonExecutionService.ExecuteAll");
        var end = audit.GetByEventType("MethodEnd").FirstOrDefault(r => r.Message == "IAutomatonExecutionService.ExecuteAll");

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
    }


    private static async Task<HttpResponseMessage> PostAutomatonForm(HttpClient client, string url, AutomatonViewModel model)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("Input", model.Input ?? ""),
            new("IsCustomAutomaton", model.IsCustomAutomaton.ToString().ToLower()),
            new("Position", model.Position.ToString()),
            new("StateHistorySerialized", model.StateHistorySerialized ?? "")
        };

        if (model.CurrentStateId.HasValue)
        {
            formData.Add(new("CurrentStateId", model.CurrentStateId.Value.ToString()));
        }

        if (model.CurrentStates != null)
        {
            for (int i = 0; i < model.CurrentStates.Count; i++)
            {
                formData.Add(new($"CurrentStates[{i}]", model.CurrentStates.ElementAt(i).ToString()));
            }
        }

        if (model.IsAccepted.HasValue)
        {
            formData.Add(new("IsAccepted", model.IsAccepted.Value.ToString().ToLower()));
        }

        for (int i = 0; i < model.States.Count; i++)
        {
            var state = model.States[i];
            formData.Add(new($"States[{i}].Id", state.Id.ToString()));
            formData.Add(new($"States[{i}].IsStart", state.IsStart.ToString().ToLower()));
            formData.Add(new($"States[{i}].IsAccepting", state.IsAccepting.ToString().ToLower()));
        }

        for (int i = 0; i < model.Transitions.Count; i++)
        {
            var transition = model.Transitions[i];
            formData.Add(new($"Transitions[{i}].FromStateId", transition.FromStateId.ToString()));
            formData.Add(new($"Transitions[{i}].ToStateId", transition.ToStateId.ToString()));
            formData.Add(new($"Transitions[{i}].Symbol", transition.Symbol == '\0' ? "?" : transition.Symbol.ToString()));
        }

        for (int i = 0; i < model.Alphabet.Count; i++)
        {
            formData.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
        }

        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync(url, formContent);
    }
}
