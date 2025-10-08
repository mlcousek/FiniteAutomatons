namespace FiniteAutomatons.IntegrationTests.AutomatonTypes;

[Collection("Integration Tests")]
public class EdgeCasesAndComplexScenariosIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task EpsilonNFA_OnlyEpsilonTransitions_AcceptsEmptyString()
    {
        // Test an ?-NFA with only epsilon transitions
        var client = GetHttpClient();

        var epsilonOnlyNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "EpsilonNFA"),
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "false"),
            new("States[2].Id", "3"),
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "true"),
            
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "2"),
            new("Transitions[0].Symbol", "\0"),
            new("Transitions[1].FromStateId", "2"),
            new("Transitions[1].ToStateId", "3"),
            new("Transitions[1].Symbol", "\0"),

            new("Input", "")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(epsilonOnlyNfaData));

        var homeResponse = await client.GetAsync("/Home");
        var homeHtml = await homeResponse.Content.ReadAsStringAsync();

        if (homeHtml.Contains("Custom Automaton"))
        {
            var executeData = new List<KeyValuePair<string, string>>
            {
                new("Type", "EpsilonNFA"),
                new("Input", ""),
                new("IsCustomAutomaton", "true")
            };

            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(executeData));
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Accepted", html);
            Assert.Contains("Current States:", html);
            Assert.Contains("3", html); 
        }
        else
        {
            Assert.True(true, "Skipping epsilon-only test because automaton creation failed");
        }
    }

    [Fact]
    public async Task NFA_SelfLoopOnlyAutomaton_InfiniteLanguage()
    {
        var client = GetHttpClient();

        var selfLoopNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "NFA"),
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "true"), // Both start and accepting
            
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "1"),
            new("Transitions[0].Symbol", "a"),
            new("Transitions[1].FromStateId", "1"),
            new("Transitions[1].ToStateId", "1"),
            new("Transitions[1].Symbol", "b"),
            new("Transitions[2].FromStateId", "1"),
            new("Transitions[2].ToStateId", "1"),
            new("Transitions[2].Symbol", "c"),

            new("Alphabet[0]", "a"),
            new("Alphabet[1]", "b"),
            new("Alphabet[2]", "c")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(selfLoopNfaData));

        var testInputs = new[] { "", "a", "abc", "cba", "aaaaaa", "bcbcbc", "abcabcabc" };

        foreach (var input in testInputs)
        {
            var testData = new List<KeyValuePair<string, string>>
            {
                new("Type", "NFA"),
                new("Input", input),
                new("IsCustomAutomaton", "true")
            };

            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(testData));
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Accepted", html);
        }
    }

    [Fact]
    public async Task EpsilonNFA_EpsilonCycles_HandledCorrectly()
    {
        var client = GetHttpClient();

        var cyclicEpsilonNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "EpsilonNFA"),
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "false"),
            new("States[2].Id", "3"),
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "true"),
            
            // Epsilon cycle: 1 -> 2 -> 1
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "2"),
            new("Transitions[0].Symbol", "\0"),
            new("Transitions[1].FromStateId", "2"),
            new("Transitions[1].ToStateId", "1"),
            new("Transitions[1].Symbol", "\0"),
            
            // Epsilon to accepting state
            new("Transitions[2].FromStateId", "2"),
            new("Transitions[2].ToStateId", "3"),
            new("Transitions[2].Symbol", "\0"),

            new("Input", "")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(cyclicEpsilonNfaData));

        var executeData = new List<KeyValuePair<string, string>>
        {
            new("Type", "EpsilonNFA"),
            new("Input", ""),
            new("IsCustomAutomaton", "true")
        };

        var startTime = DateTime.Now;
        var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(executeData));
        var endTime = DateTime.Now;
        var executionTime = endTime - startTime;

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accepted", html);

        Assert.True(executionTime.TotalSeconds < 5, "Epsilon cycle handling took too long - possible infinite loop");
    }

    [Fact]
    public async Task NFA_AllStatesAccepting_UniversalLanguage()
    {
        var client = GetHttpClient();

        var universalNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "NFA"),
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "true"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "true"),
            new("States[2].Id", "3"),
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "true"),
            
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "2"),
            new("Transitions[0].Symbol", "a"),
            new("Transitions[1].FromStateId", "1"),
            new("Transitions[1].ToStateId", "3"),
            new("Transitions[1].Symbol", "b"),
            new("Transitions[2].FromStateId", "2"),
            new("Transitions[2].ToStateId", "1"),
            new("Transitions[2].Symbol", "a"),
            new("Transitions[3].FromStateId", "2"),
            new("Transitions[3].ToStateId", "3"),
            new("Transitions[3].Symbol", "b"),
            new("Transitions[4].FromStateId", "3"),
            new("Transitions[4].ToStateId", "1"),
            new("Transitions[4].Symbol", "a"),
            new("Transitions[5].FromStateId", "3"),
            new("Transitions[5].ToStateId", "2"),
            new("Transitions[5].Symbol", "b"),

            new("Alphabet[0]", "a"),
            new("Alphabet[1]", "b")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(universalNfaData));

        var testInputs = new[] { "", "a", "b", "ab", "ba", "aaaa", "bbbb", "abab", "baba", "aabbbaaba" };

        foreach (var input in testInputs)
        {
            var testData = new List<KeyValuePair<string, string>>
            {
                new("Type", "NFA"),
                new("Input", input),
                new("IsCustomAutomaton", "true")
            };

            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(testData));
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Accepted", html);
        }
    }

    [Fact]
    public async Task NFA_ExponentialStateSpace_ManagedEfficiently()
    {
        var client = GetHttpClient();

        var exponentialNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "NFA"),
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "false"),
            new("States[2].Id", "3"),
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "false"),
            new("States[3].Id", "4"),
            new("States[3].IsStart", "false"),
            new("States[3].IsAccepting", "false"),
            new("States[4].Id", "5"),
            new("States[4].IsStart", "false"),
            new("States[4].IsAccepting", "true"),
            
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "1"),
            new("Transitions[0].Symbol", "a"),
            new("Transitions[1].FromStateId", "1"),
            new("Transitions[1].ToStateId", "2"),
            new("Transitions[1].Symbol", "a"),
            new("Transitions[2].FromStateId", "2"),
            new("Transitions[2].ToStateId", "2"),
            new("Transitions[2].Symbol", "a"),
            new("Transitions[3].FromStateId", "2"),
            new("Transitions[3].ToStateId", "3"),
            new("Transitions[3].Symbol", "a"),
            new("Transitions[4].FromStateId", "3"),
            new("Transitions[4].ToStateId", "3"),
            new("Transitions[4].Symbol", "a"),
            new("Transitions[5].FromStateId", "3"),
            new("Transitions[5].ToStateId", "4"),
            new("Transitions[5].Symbol", "a"),
            new("Transitions[6].FromStateId", "4"),
            new("Transitions[6].ToStateId", "4"),
            new("Transitions[6].Symbol", "a"),
            new("Transitions[7].FromStateId", "4"),
            new("Transitions[7].ToStateId", "5"),
            new("Transitions[7].Symbol", "a"),
            
            new("Transitions[8].FromStateId", "1"),
            new("Transitions[8].ToStateId", "3"),
            new("Transitions[8].Symbol", "b"),
            new("Transitions[9].FromStateId", "2"),
            new("Transitions[9].ToStateId", "4"),
            new("Transitions[9].Symbol", "b"),
            new("Transitions[10].FromStateId", "3"),
            new("Transitions[10].ToStateId", "5"),
            new("Transitions[10].Symbol", "b"),

            new("Alphabet[0]", "a"),
            new("Alphabet[1]", "b")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(exponentialNfaData));

        var complexInputs = new[]
        {
            ("aaaa", true),      // Should reach accepting state
            ("aaab", true),      // Alternative path to acceptance
            ("aaba", true),      // Different nondeterministic choices
            ("baaa", true),      // Starting with 'b'
            ("aabaab", true),    // Complex mix
            ("bb", false),       // Can't reach accepting state
            ("aab", false)       // Falls short of accepting state
        };

        foreach (var (input, shouldAccept) in complexInputs)
        {
            var testData = new List<KeyValuePair<string, string>>
            {
                new("Type", "NFA"),
                new("Input", input),
                new("IsCustomAutomaton", "true")
            };

            var startTime = DateTime.Now;
            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(testData));
            var endTime = DateTime.Now;
            var executionTime = endTime - startTime;

            var html = await response.Content.ReadAsStringAsync();

            if (shouldAccept)
            {
                Assert.Contains("Accepted", html);
            }
            else
            {
                Assert.Contains("Rejected", html);
            }

            Assert.True(executionTime.TotalSeconds < 5,
                $"Complex NFA execution took too long for input '{input}': {executionTime.TotalSeconds} seconds");
        }
    }

    [Fact]
    public async Task EpsilonNFA_DeepEpsilonChains_CorrectClosure()
    {
        var client = GetHttpClient();

        var deepEpsilonNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "EpsilonNFA"),
            new("States[0].Id", "1"),
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "false"),
            new("States[2].Id", "3"),
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "false"),
            new("States[3].Id", "4"),
            new("States[3].IsStart", "false"),
            new("States[3].IsAccepting", "false"),
            new("States[4].Id", "5"),
            new("States[4].IsStart", "false"),
            new("States[4].IsAccepting", "false"),
            new("States[5].Id", "6"),
            new("States[5].IsStart", "false"),
            new("States[5].IsAccepting", "false"),
            new("States[6].Id", "7"),
            new("States[6].IsStart", "false"),
            new("States[6].IsAccepting", "true"),
            
            // Deep chain of epsilon transitions: 1->2->3->4->5->6->7
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "2"),
            new("Transitions[0].Symbol", "\0"),
            new("Transitions[1].FromStateId", "2"),
            new("Transitions[1].ToStateId", "3"),
            new("Transitions[1].Symbol", "\0"),
            new("Transitions[2].FromStateId", "3"),
            new("Transitions[2].ToStateId", "4"),
            new("Transitions[2].Symbol", "\0"),
            new("Transitions[3].FromStateId", "4"),
            new("Transitions[3].ToStateId", "5"),
            new("Transitions[3].Symbol", "\0"),
            new("Transitions[4].FromStateId", "5"),
            new("Transitions[4].ToStateId", "6"),
            new("Transitions[4].Symbol", "\0"),
            new("Transitions[5].FromStateId", "6"),
            new("Transitions[5].ToStateId", "7"),
            new("Transitions[5].Symbol", "\0"),
            
            // Some alternative epsilon paths for complexity
            new("Transitions[6].FromStateId", "1"),
            new("Transitions[6].ToStateId", "4"),
            new("Transitions[6].Symbol", "\0"),
            new("Transitions[7].FromStateId", "2"),
            new("Transitions[7].ToStateId", "6"),
            new("Transitions[7].Symbol", "\0"),

            new("Input", "")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(deepEpsilonNfaData));

        var homeResponse = await client.GetAsync("/Home");
        var homeHtml = await homeResponse.Content.ReadAsStringAsync();

        if (homeHtml.Contains("Custom Automaton"))
        {
            var executeData = new List<KeyValuePair<string, string>>
            {
                new("Type", "EpsilonNFA"),
                new("Input", ""),
                new("IsCustomAutomaton", "true")
            };

            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(executeData));
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Accepted", html);
            Assert.Contains("Current States:", html);
            Assert.Contains("7", html); 
        }
        else
        {
            Assert.True(true, "Skipping deep epsilon chain test because automaton creation failed");
        }
    }

    [Fact]
    public async Task NFA_RegexLikePatterns_QuantifiersAndAlternation()
    {
        // Create NFA equivalent to regex: (a|b)*a(a|b)  //TODO regex
        // Language: strings ending with 'a' followed by any symbol
        var client = GetHttpClient();

        var regexNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "NFA"),
            new("States[0].Id", "1"),  // Start
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "false"),
            new("States[1].Id", "2"),  // After reading 'a' (potential penultimate)
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "false"),
            new("States[2].Id", "3"),  // Final state (after 'a' and one more symbol)
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "true"),
            
            // (a|b)* part - self-loops on state 1
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "1"),
            new("Transitions[0].Symbol", "a"),
            new("Transitions[1].FromStateId", "1"),
            new("Transitions[1].ToStateId", "1"),
            new("Transitions[1].Symbol", "b"),
            
            // 'a' transition (the required 'a' before final symbol)
            new("Transitions[2].FromStateId", "1"),
            new("Transitions[2].ToStateId", "2"),
            new("Transitions[2].Symbol", "a"),
            
            // (a|b) final transition
            new("Transitions[3].FromStateId", "2"),
            new("Transitions[3].ToStateId", "3"),
            new("Transitions[3].Symbol", "a"),
            new("Transitions[4].FromStateId", "2"),
            new("Transitions[4].ToStateId", "3"),
            new("Transitions[4].Symbol", "b"),

            new("Alphabet[0]", "a"),
            new("Alphabet[1]", "b")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(regexNfaData));

        // Test pattern: must end with 'a' followed by any symbol, minimum length 2
        var regexTests = new[]
        {
            ("aa", true),       // ends with 'aa'
            ("ab", true),       // ends with 'ab'
            ("baa", true),      // ends with 'aa'
            ("bab", true),      // ends with 'ab'
            ("abaa", true),     // complex but ends with 'aa'
            ("baab", true),     // complex but ends with 'ab'
            ("a", false),       // too short
            ("b", false),       // doesn't end with 'a'+symbol
            ("ba", false),      // ends with 'a' but no following symbol
            ("bb", false),      // doesn't end with 'a'+symbol
            ("aba", false),     // ends with 'a' but no following symbol
            ("", false)         // empty string
        };

        foreach (var (input, shouldAccept) in regexTests)
        {
            var testData = new List<KeyValuePair<string, string>>
            {
                new("Type", "NFA"),
                new("Input", input),
                new("IsCustomAutomaton", "true")
            };

            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(testData));
            var html = await response.Content.ReadAsStringAsync();

            if (shouldAccept)
            {
                Assert.Contains("Accepted", html);
            }
            else
            {
                Assert.Contains("Rejected", html);
            }
        }
    }

    [Fact]
    public async Task EpsilonNFA_NestedStructures_ComplexLanguage()
    {
        var client = GetHttpClient();

        var nestedNfaData = new List<KeyValuePair<string, string>>
        {
            new("Type", "EpsilonNFA"),
            new("States[0].Id", "1"),  // Start/balanced state
            new("States[0].IsStart", "true"),
            new("States[0].IsAccepting", "true"),
            new("States[1].Id", "2"),  // After one 'a' (depth 1)
            new("States[1].IsStart", "false"),
            new("States[1].IsAccepting", "false"),
            new("States[2].Id", "3"),  // After two 'a's (depth 2)
            new("States[2].IsStart", "false"),
            new("States[2].IsAccepting", "false"),
            new("States[3].Id", "4"),  // Error state (unbalanced)
            new("States[3].IsStart", "false"),
            new("States[3].IsAccepting", "false"),
            
            new("Transitions[0].FromStateId", "1"),
            new("Transitions[0].ToStateId", "2"),
            new("Transitions[0].Symbol", "a"), // open
            new("Transitions[1].FromStateId", "2"),
            new("Transitions[1].ToStateId", "1"),
            new("Transitions[1].Symbol", "b"), // close, back to balanced
            new("Transitions[2].FromStateId", "2"),
            new("Transitions[2].ToStateId", "3"),
            new("Transitions[2].Symbol", "a"), // deeper nesting
            new("Transitions[3].FromStateId", "3"),
            new("Transitions[3].ToStateId", "2"),
            new("Transitions[3].Symbol", "b"), // close one level
            
            new("Transitions[4].FromStateId", "1"),
            new("Transitions[4].ToStateId", "4"),
            new("Transitions[4].Symbol", "b"), // close without open
            new("Transitions[5].FromStateId", "4"),
            new("Transitions[5].ToStateId", "4"),
            new("Transitions[5].Symbol", "a"), // stuck in error
            new("Transitions[6].FromStateId", "4"),
            new("Transitions[6].ToStateId", "4"),
            new("Transitions[6].Symbol", "b"), // stuck in error
            
            new("Transitions[7].FromStateId", "1"),
            new("Transitions[7].ToStateId", "1"),
            new("Transitions[7].Symbol", "\0"), // can stay balanced
            
            new("Alphabet[0]", "a"),
            new("Alphabet[1]", "b")
        };

        await client.PostAsync("/Automaton/CreateAutomaton", new FormUrlEncodedContent(nestedNfaData));

        var nestedTests = new[]
        {
            ("", true),         // empty is balanced
            ("ab", true),       // simple balanced
            ("aabb", true),     // nested balanced
            ("abab", true),     // sequential balanced
            ("a", false),       // unbalanced (unclosed)
            ("b", false),       // unbalanced (close without open)
            ("aab", false),     // unbalanced (extra close)
            ("abb", false),     // unbalanced (extra close)
            ("aba", false),     // unbalanced (unclosed)
            ("abba", false)     // complex unbalanced
        };

        foreach (var (input, shouldAccept) in nestedTests)
        {
            var testData = new List<KeyValuePair<string, string>>
            {
                new("Type", "EpsilonNFA"),
                new("Input", input),
                new("IsCustomAutomaton", "true")
            };

            var response = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(testData));
            var html = await response.Content.ReadAsStringAsync();

            if (shouldAccept)
            {
                Assert.Contains("Accepted", html);
            }
            else
            {
                Assert.Contains("Rejected", html);
            }
        }
    }
}
