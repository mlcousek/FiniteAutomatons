using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonCreation;

[Collection("Integration Tests")]
public class AutomatonCreationIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task CreateAutomaton_Get_ReturnsSuccessAndEmptyForm()
    {
        // Arrange
        var client = GetHttpClient();

        // Act
        var response = await client.GetAsync("/Home/CreateAutomaton");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Create Your Own Automaton", html);
        Assert.Contains("No states added yet", html);
    }

    [Fact]
    public async Task AddState_ValidState_AddsStateSuccessfully()
    {
        // Arrange
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("stateId", "1"),
            new("isStart", "true"),
            new("isStart", "false"), // Hidden field for checkbox
            new("isAccepting", "false"),
            new("isAccepting", "false") // Hidden field for checkbox
        };

        // Act
        var response = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(formData));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("State 1", html);
        Assert.Contains("Start", html);
    }

    [Fact]
    public async Task AddState_DuplicateStateId_ShowsValidationError()
    {
        // Arrange
        var client = GetHttpClient();

        // First add a state
        var firstStateData = new List<KeyValuePair<string, string>>
        {
            new("stateId", "1"),
            new("isStart", "true"),
            new("isStart", "false"),
            new("isAccepting", "false"),
            new("isAccepting", "false")
        };
        await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(firstStateData));

        // Try to add duplicate state
        var duplicateStateData = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("stateId", "1"),
            new("isStart", "false"),
            new("isStart", "false"),
            new("isAccepting", "true"),
            new("isAccepting", "false")
        };

        // Act
        var response = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(duplicateStateData));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("State with ID 1 already exists", html);
    }

    [Fact]
    public async Task AddTransition_ValidTransition_AddsTransitionSuccessfully()
    {
        // Arrange
        var client = GetHttpClient();

        // First add two states
        var state1Data = new List<KeyValuePair<string, string>>
        {
            new("stateId", "1"),
            new("isStart", "true"),
            new("isStart", "false"),
            new("isAccepting", "false"),
            new("isAccepting", "false")
        };
        var response1 = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(state1Data));
        _ = await response1.Content.ReadAsStringAsync();

        var state2Data = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("stateId", "2"),
            new("isStart", "false"),
            new("isStart", "false"),
            new("isAccepting", "true"),
            new("isAccepting", "false")
        };
        _ = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(state2Data));

        // Now add transition
        var transitionData = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "true"),
            new("fromStateId", "1"),
            new("toStateId", "2"),
            new("symbol", "a")
        };

        // Act
        var response = await client.PostAsync("/Home/AddTransition", new FormUrlEncodedContent(transitionData));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("1", html); // From state
        Assert.Contains("2", html); // To state
        Assert.Contains("a", html); // Symbol
        Assert.Contains("Alphabet", html);
    }

    [Fact]
    public async Task CreateCompleteAutomaton_ValidAutomaton_ReturnsSuccessResponse()
    {
        // Arrange
        var client = GetHttpClient();

        // Create a simple valid automaton data
        var finalData = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "true"),
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "2"),
            new("Transitions[0].Symbol", "a"),
            new("Alphabet[0]", "a")
        };

        // Act
        var response = await client.PostAsync("/Home/CreateAutomaton", new FormUrlEncodedContent(finalData));

        // Assert
        // Check for either success or redirect
        Assert.True(response.IsSuccessStatusCode ||
                   response.StatusCode == HttpStatusCode.Redirect ||
                   response.StatusCode == HttpStatusCode.Found ||
                   response.StatusCode == HttpStatusCode.SeeOther);
    }

    [Fact]
    public async Task CreateAutomaton_NoStates_ShowsValidationError()
    {
        // Arrange
        var client = GetHttpClient();
        var emptyData = new List<KeyValuePair<string, string>>();

        // Act
        var response = await client.PostAsync("/Home/CreateAutomaton", new FormUrlEncodedContent(emptyData));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Automaton must have at least one state", html);
    }

    [Fact]
    public async Task RemoveState_ExistingState_RemovesStateSuccessfully()
    {
        // Arrange
        var client = GetHttpClient();

        // Add a state first
        var addStateData = new List<KeyValuePair<string, string>>
        {
            new("stateId", "1"),
            new("isStart", "true"),
            new("isStart", "false"),
            new("isAccepting", "false"),
            new("isAccepting", "false")
        };
        await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(addStateData));

        // Remove the state
        var removeStateData = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("stateId", "1")
        };

        // Act
        var response = await client.PostAsync("/Home/RemoveState", new FormUrlEncodedContent(removeStateData));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("No states added yet", html);
        Assert.DoesNotContain("State 1", html);
    }

    [Fact]
    public async Task AddState_SecondStartState_ShowsValidationError()
    {
        // Arrange
        var client = GetHttpClient();

        // Add first start state
        var firstStateData = new List<KeyValuePair<string, string>>
        {
            new("stateId", "1"),
            new("isStart", "true"),
            new("isStart", "false"),
            new("isAccepting", "false"),
            new("isAccepting", "false")
        };
        await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(firstStateData));

        // Try to add second start state
        var secondStartStateData = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("stateId", "2"),
            new("isStart", "true"),
            new("isStart", "false"),
            new("isAccepting", "false"),
            new("isAccepting", "false")
        };

        // Act
        var response = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(secondStartStateData));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only one start state is allowed", html);
    }

    [Fact]
    public async Task AddMultipleStates_StatesPersistCorrectly()
    {
        // Arrange
        var client = GetHttpClient();

        // Add first state (start)
        var firstStateData = new List<KeyValuePair<string, string>>
        {
            new("stateId", "1"),
            new("isStart", "true"),
            new("isStart", "false"),
            new("isAccepting", "false"),
            new("isAccepting", "false")
        };
        var response1 = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(firstStateData));
        var html1 = await response1.Content.ReadAsStringAsync();

        // Verify first state is added and marked as start
        Assert.Contains("State 1", html1);
        Assert.Contains("Start", html1);

        // Add second state (accepting) - THIS IS THE CRITICAL TEST
        var secondStateData = new List<KeyValuePair<string, string>>
        {
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("stateId", "2"),
            new("isStart", "false"),
            new("isStart", "false"),
            new("isAccepting", "true"),
            new("isAccepting", "false")
        };
        var response2 = await client.PostAsync("/Home/AddState", new FormUrlEncodedContent(secondStateData));
        var html2 = await response2.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // CRITICAL: Both states should be present
        Assert.Contains("State 1", html2);
        Assert.Contains("State 2", html2);

        // CRITICAL: First state should still be marked as start
        Assert.Contains("Start", html2); // State 1 should still have Start badge
        Assert.Contains("Accept", html2); // State 2 should have Accept badge

        // Verify state count
        Assert.Contains("States (2)", html2);
    }
}
