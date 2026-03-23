using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.Serialization;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FiniteAutomatons.Data.Seeding;

public class DemoDataSeeder(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<DemoDataSeeder> logger)
{
    private const string Password = "Test123";

    private static readonly string Supervisor = "supervisor@test.test";
    private static readonly string Alice = "alice@test.test";
    private static readonly string Bob = "bob@test.test";
    private static readonly string Charlie = "charlie@test.test";
    private static readonly string Diana = "diana@test.test";
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByEmailAsync(Supervisor) is not null)
        {
            logger.LogInformation("Demo data already seeded — skipping");
            return;
        }

        logger.LogInformation("Seeding demo data...");

        var supervisorUser = await CreateUserAsync(Supervisor);
        var aliceUser = await CreateUserAsync(Alice);
        var bobUser = await CreateUserAsync(Bob);
        var charlieUser = await CreateUserAsync(Charlie);
        var dianaUser = await CreateUserAsync(Diana);

        // ---- Saved automatons ----

        // Supervisor: classical DFA and NFA examples
        var supDfaEvenAs = SavedWithState(supervisorUser.Id,
            "DFA – Even number of a's",
            "Accepts all strings over {a,b} where the count of 'a' is even. Classic 2-state DFA.",
            DfaEvenAsJson,
            input: "aab", position: 3, currentStateId: 0, isAccepted: true);
            // "aab": q0 -a-> q1 -a-> q0 -b-> q0 ✓ (2 a's → even → accepted)

        var supDfaBinaryDiv3 = SavedWithInput(supervisorUser.Id,
            "DFA – Binary numbers divisible by 3",
            "Accepts binary strings whose decimal value is divisible by 3. Demonstrates DFA with mathematical invariant.",
            DfaBinaryDiv3Json,
            "110"); // 6 in decimal – divisible by 3

        var supNfaContainsAb = Saved(supervisorUser.Id,
            "NFA – Contains substring 'ab'",
            "Nondeterministic FA accepting all strings over {a,b} that contain the pattern 'ab'.",
            NfaContainsAbJson);

        var supDfaNoConsecAs = SavedWithState(supervisorUser.Id,
            "DFA – No two consecutive a's",
            "Accepts strings over {a,b} with no 'aa' substring. Classic 3-state DFA with a trap state.",
            DfaNoConsecAsJson,
            input: "abab", position: 4, currentStateId: 0, isAccepted: true);
            // "abab": q0 -a-> q1 -b-> q0 -a-> q1 -b-> q0 ✓ (no 'aa' → accepted)

        var supNfaEndsInAb = Saved(supervisorUser.Id,
            "NFA – Ends in 'ab'",
            "Nondeterministic FA accepting strings over {a,b} whose last two symbols are 'ab'.",
            NfaEndsInAbJson);

        // Alice: DFAs (one minimizable, one counting, one length-parity) and an epsilon-NFA
        var aliceDfaEndsInB = SavedWithState(aliceUser.Id,
            "DFA – Ends in 'b'",
            "Accepts strings over {a,b} whose last symbol is 'b'. Simple 2-state DFA.",
            DfaEndsInBJson,
            input: "abb", position: 3, currentStateId: 1, isAccepted: true);
            // "abb": q0 -a-> q0 -b-> q1 -b-> q1 ✓ (ends in 'b' → accepted)

        var aliceDfaMinimizable = Saved(aliceUser.Id,
            "DFA – Starts with 'a' (minimizable)",
            "Accepts strings starting with 'a'. Contains an unreachable state — good for the minimization demo.",
            DfaStartsWithAMinimizableJson);

        var aliceDfaEvenLength = SavedWithInput(aliceUser.Id,
            "DFA – Even-length strings",
            "Accepts all strings over {a,b} whose length is even (including the empty string).",
            DfaEvenLengthJson,
            "abab"); // length 4 – accepted

        var aliceDfaExactlyTwoAs = Saved(aliceUser.Id,
            "DFA – Exactly two a's",
            "Accepts strings over {a,b} containing exactly two 'a' characters. 4-state counting DFA.",
            DfaExactlyTwoAsJson);

        var aliceEnfaABorC = Saved(aliceUser.Id,
            "ε-NFA – a(b|c)",
            "Epsilon-NFA for the regular expression a(b|c): accepts 'ab' and 'ac'. Shows Thompson-style construction.",
            EnfaABorCJson);

        // Bob: DPDA and NPDA examples
        var bobDpdaAnBn = Saved(bobUser.Id,
            "DPDA – aⁿbⁿ (n ≥ 0)",
            "Deterministic PDA for the context-free language {aⁿbⁿ | n ≥ 0}. Demonstrates stack usage.",
            DpdaAnBnJson);

        var bobDpdaBalancedParens = Saved(bobUser.Id,
            "DPDA – Balanced parentheses",
            "Deterministic PDA that accepts strings with properly balanced ( and ) characters.",
            DpdaBalancedParensJson);

        var bobNpdaPalindromes = Saved(bobUser.Id,
            "NPDA – Even-length palindromes",
            "Nondeterministic PDA accepting even-length palindromes over {a,b} using a midpoint guess.",
            NpdaEvenPalindromesJson);

        var bobDpdaAtLeastAsManyAs = Saved(bobUser.Id,
            "DPDA – At least as many a's as b's",
            "Accepts aⁿbᵐ where n ≥ m ≥ 0. Uses the stack to compare 'a' and 'b' counts.",
            DpdaAtLeastAsManyAsJson);

        // Charlie: simple and classic examples (good for a viewer exploring the app)
        var charlieDfaAcceptAll = Saved(charlieUser.Id,
            "DFA – Accepts all strings",
            "Trivial single-state DFA that accepts every string over {a,b}.",
            DfaAcceptAllJson);

        var charlieNfaSecondToLast = Saved(charlieUser.Id,
            "NFA – 'a' as second-to-last symbol",
            "Accepts strings over {a,b} where the second-to-last symbol is 'a'. Classic NFA example.",
            NfaSecondToLastAJson);

        var charlieNfaContainsAba = SavedWithInput(charlieUser.Id,
            "NFA – Contains substring 'aba'",
            "Nondeterministic FA that accepts strings over {a,b} containing 'aba' as a substring.",
            NfaContainsAbaJson,
            "babab"); // contains "aba" at position 1

        var charlieDfaLengthDivBy3 = Saved(charlieUser.Id,
            "DFA – Length divisible by 3 over {a}",
            "Accepts strings over {a} whose length is a multiple of 3. Simple 3-state cyclic DFA.",
            DfaLengthDivBy3Json);

        // Diana: mixed examples including Thompson-style ε-NFAs
        var dianaNfaStartsWithAb = Saved(dianaUser.Id,
            "NFA – Starts with 'ab'",
            "Accepts all strings over {a,b} that begin with the pattern 'ab'.",
            NfaStartsWithAbJson);

        var dianaEnfaStarB = Saved(dianaUser.Id,
            "ε-NFA – a*b (Thompson construction)",
            "Epsilon-NFA for the regular expression a*b, illustrating Thompson's construction.",
            EnfaStarBJson);

        var dianaDfaAlternating = SavedWithInput(dianaUser.Id,
            "DFA – Alternating a and b",
            "Accepts strings where 'a' and 'b' strictly alternate, starting with 'a' (e.g. a, ab, aba, abab).",
            DfaAlternatingJson,
            "ababab"); // valid alternating string – accepted

        var dianaEnfaOptionalAB = Saved(dianaUser.Id,
            "ε-NFA – (a|ε)b",
            "Epsilon-NFA for (a|ε)b: accepts 'b' and 'ab'. Shows how ε-transitions model optional symbols.",
            EnfaOptionalABJson);

        dbContext.SavedAutomatons.AddRange(
            supDfaEvenAs, supDfaBinaryDiv3, supNfaContainsAb,
            supDfaNoConsecAs, supNfaEndsInAb,
            aliceDfaEndsInB, aliceDfaMinimizable, aliceDfaEvenLength,
            aliceDfaExactlyTwoAs, aliceEnfaABorC,
            bobDpdaAnBn, bobDpdaBalancedParens, bobNpdaPalindromes,
            bobDpdaAtLeastAsManyAs,
            charlieDfaAcceptAll, charlieNfaSecondToLast,
            charlieNfaContainsAba, charlieDfaLengthDivBy3,
            dianaNfaStartsWithAb, dianaEnfaStarB, dianaDfaAlternating,
            dianaEnfaOptionalAB);

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Personal saved automaton groups ----

        var supGroupFAs = SavedGroup(supervisorUser.Id,
            "Finite Automata",
            "Classical deterministic finite automaton examples.");

        var supGroupNFAs = SavedGroup(supervisorUser.Id,
            "NFA Examples",
            "Nondeterministic finite automaton examples.");

        var aliceGroupDFAs = SavedGroup(aliceUser.Id,
            "DFA Collection",
            "My deterministic finite automaton examples, including a minimizable one.");

        var aliceGroupENFAs = SavedGroup(aliceUser.Id,
            "Epsilon-NFA Lab",
            "Epsilon-NFA constructions and experiments.");

        var bobGroupPDAs = SavedGroup(bobUser.Id,
            "PDA Workshop",
            "Deterministic and nondeterministic pushdown automaton examples.");

        var charlieGroup = SavedGroup(charlieUser.Id,
            "My Automatons",
            "Personal automaton collection.");

        var dianaGroup = SavedGroup(dianaUser.Id,
            "Theory Examples",
            "Formal language theory automata collection.");

        dbContext.SavedAutomatonGroups.AddRange(
            supGroupFAs, supGroupNFAs,
            aliceGroupDFAs, aliceGroupENFAs,
            bobGroupPDAs,
            charlieGroup,
            dianaGroup);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Assign saved automatons to their personal groups
        dbContext.SavedAutomatonGroupAssignments.AddRange(
            new SavedAutomatonGroupAssignment { GroupId = supGroupFAs.Id, AutomatonId = supDfaEvenAs.Id },
            new SavedAutomatonGroupAssignment { GroupId = supGroupFAs.Id, AutomatonId = supDfaBinaryDiv3.Id },
            new SavedAutomatonGroupAssignment { GroupId = supGroupFAs.Id, AutomatonId = supDfaNoConsecAs.Id },
            new SavedAutomatonGroupAssignment { GroupId = supGroupNFAs.Id, AutomatonId = supNfaContainsAb.Id },
            new SavedAutomatonGroupAssignment { GroupId = supGroupNFAs.Id, AutomatonId = supNfaEndsInAb.Id },
            new SavedAutomatonGroupAssignment { GroupId = aliceGroupDFAs.Id, AutomatonId = aliceDfaEndsInB.Id },
            new SavedAutomatonGroupAssignment { GroupId = aliceGroupDFAs.Id, AutomatonId = aliceDfaMinimizable.Id },
            new SavedAutomatonGroupAssignment { GroupId = aliceGroupDFAs.Id, AutomatonId = aliceDfaEvenLength.Id },
            new SavedAutomatonGroupAssignment { GroupId = aliceGroupDFAs.Id, AutomatonId = aliceDfaExactlyTwoAs.Id },
            new SavedAutomatonGroupAssignment { GroupId = aliceGroupENFAs.Id, AutomatonId = aliceEnfaABorC.Id },
            new SavedAutomatonGroupAssignment { GroupId = bobGroupPDAs.Id, AutomatonId = bobDpdaAnBn.Id },
            new SavedAutomatonGroupAssignment { GroupId = bobGroupPDAs.Id, AutomatonId = bobDpdaBalancedParens.Id },
            new SavedAutomatonGroupAssignment { GroupId = bobGroupPDAs.Id, AutomatonId = bobNpdaPalindromes.Id },
            new SavedAutomatonGroupAssignment { GroupId = bobGroupPDAs.Id, AutomatonId = bobDpdaAtLeastAsManyAs.Id },
            new SavedAutomatonGroupAssignment { GroupId = charlieGroup.Id, AutomatonId = charlieDfaAcceptAll.Id },
            new SavedAutomatonGroupAssignment { GroupId = charlieGroup.Id, AutomatonId = charlieNfaSecondToLast.Id },
            new SavedAutomatonGroupAssignment { GroupId = charlieGroup.Id, AutomatonId = charlieNfaContainsAba.Id },
            new SavedAutomatonGroupAssignment { GroupId = charlieGroup.Id, AutomatonId = charlieDfaLengthDivBy3.Id },
            new SavedAutomatonGroupAssignment { GroupId = dianaGroup.Id, AutomatonId = dianaNfaStartsWithAb.Id },
            new SavedAutomatonGroupAssignment { GroupId = dianaGroup.Id, AutomatonId = dianaEnfaStarB.Id },
            new SavedAutomatonGroupAssignment { GroupId = dianaGroup.Id, AutomatonId = dianaDfaAlternating.Id },
            new SavedAutomatonGroupAssignment { GroupId = dianaGroup.Id, AutomatonId = dianaEnfaOptionalAB.Id });

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Shared automatons ----
        // Each shared automaton is an independent copy contributed to a shared group.

        var sharedDfaEvenAs = Shared(supervisorUser.Id,
            "DFA – Even number of a's",
            "Classic DFA contributed by supervisor for team study.",
            DfaEvenAsJson);

        var sharedDfaEndsInB = Shared(aliceUser.Id,
            "DFA – Ends in 'b'",
            "Alice's DFA contributed to the Formal Language Theory group.",
            DfaEndsInBJson);

        var sharedDpdaAnBn = Shared(bobUser.Id,
            "DPDA – aⁿbⁿ",
            "Bob's DPDA for the context-free language aⁿbⁿ.",
            DpdaAnBnJson);

        var sharedDpdaBalancedParens = Shared(bobUser.Id,
            "DPDA – Balanced parentheses",
            "Shared for team PDA studies.",
            DpdaBalancedParensJson);

        var sharedNpdaPalindromes = Shared(bobUser.Id,
            "NPDA – Even-length palindromes",
            "Nondeterministic palindrome recogniser, shared for team study.",
            NpdaEvenPalindromesJson);

        var sharedEnfaABorC = Shared(aliceUser.Id,
            "ε-NFA – a(b|c)",
            "Alice's epsilon-NFA, shared in the Team Playground.",
            EnfaABorCJson);

        var sharedNfaStartsWithAb = Shared(dianaUser.Id,
            "NFA – Starts with 'ab'",
            "Diana's NFA shared in the Team Playground.",
            NfaStartsWithAbJson);

        var sharedDfaNoConsecAs = Shared(supervisorUser.Id,
            "DFA – No two consecutive a's",
            "Classic 3-state DFA from supervisor, contributed to the Formal Language Theory group.",
            DfaNoConsecAsJson);

        var sharedNfaEndsInAb = Shared(supervisorUser.Id,
            "NFA – Ends in 'ab'",
            "Supervisor's NFA for strings ending in 'ab', shared for team study.",
            NfaEndsInAbJson);

        var sharedDpdaAtLeastAsManyAs = Shared(bobUser.Id,
            "DPDA – At least as many a's as b's",
            "Bob's stack-comparison DPDA, shared for PDA studies.",
            DpdaAtLeastAsManyAsJson);

        var sharedDfaEvenLength = Shared(aliceUser.Id,
            "DFA – Even-length strings",
            "Alice's even-length DFA, shared in the Team Playground.",
            DfaEvenLengthJson);

        var sharedEnfaOptionalAB = Shared(dianaUser.Id,
            "ε-NFA – (a|ε)b",
            "Diana's epsilon-NFA for optional 'a' before 'b', shared in the Team Playground.",
            EnfaOptionalABJson);

        dbContext.SharedAutomatons.AddRange(
            sharedDfaEvenAs, sharedDfaEndsInB, sharedDpdaAnBn,
            sharedDpdaBalancedParens, sharedNpdaPalindromes,
            sharedEnfaABorC, sharedNfaStartsWithAb,
            sharedDfaNoConsecAs, sharedNfaEndsInAb,
            sharedDpdaAtLeastAsManyAs, sharedDfaEvenLength, sharedEnfaOptionalAB);

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Shared automaton groups ----

        // Group 1 – "Formal Language Theory" (supervisor owns, cross-role membership)
        var groupFlt = SharedGroup(supervisorUser.Id,
            "Formal Language Theory",
            "Collaborative study group for formal language theory automaton examples.");

        // Group 2 – "PDA Studies" (bob owns, focused on PDAs)
        var groupPda = SharedGroup(bobUser.Id,
            "PDA Studies",
            "Focused group for pushdown automaton exploration.");

        // Group 3 – "Team Playground" (alice owns, informal collaboration)
        var groupPlayground = SharedGroup(aliceUser.Id,
            "Team Playground",
            "Informal group for sharing and experimenting with various automaton types.");

        dbContext.SharedAutomatonGroups.AddRange(groupFlt, groupPda, groupPlayground);
        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Group membership (demonstrates all four roles) ----

        var now = DateTime.UtcNow;

        dbContext.SharedAutomatonGroupMembers.AddRange(
            // Formal Language Theory: supervisor=Owner, alice=Editor, bob=Contributor, charlie=Viewer
            Member(groupFlt.Id, supervisorUser.Id, SharedGroupRole.Owner, now),
            Member(groupFlt.Id, aliceUser.Id, SharedGroupRole.Editor, now, supervisorUser.Id),
            Member(groupFlt.Id, bobUser.Id, SharedGroupRole.Contributor, now, supervisorUser.Id),
            Member(groupFlt.Id, charlieUser.Id, SharedGroupRole.Viewer, now, supervisorUser.Id),

            // PDA Studies: bob=Owner, diana=Contributor, supervisor=Viewer
            Member(groupPda.Id, bobUser.Id, SharedGroupRole.Owner, now),
            Member(groupPda.Id, dianaUser.Id, SharedGroupRole.Contributor, now, bobUser.Id),
            Member(groupPda.Id, supervisorUser.Id, SharedGroupRole.Viewer, now, bobUser.Id),

            // Team Playground: alice=Owner, charlie=Editor, diana=Viewer
            Member(groupPlayground.Id, aliceUser.Id, SharedGroupRole.Owner, now),
            Member(groupPlayground.Id, charlieUser.Id, SharedGroupRole.Editor, now, aliceUser.Id),
            Member(groupPlayground.Id, dianaUser.Id, SharedGroupRole.Viewer, now, aliceUser.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Assign shared automatons to shared groups ----

        dbContext.SharedAutomatonGroupAssignments.AddRange(
            // Formal Language Theory
            new SharedAutomatonGroupAssignment { GroupId = groupFlt.Id, AutomatonId = sharedDfaEvenAs.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupFlt.Id, AutomatonId = sharedDfaEndsInB.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupFlt.Id, AutomatonId = sharedDpdaAnBn.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupFlt.Id, AutomatonId = sharedDfaNoConsecAs.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupFlt.Id, AutomatonId = sharedNfaEndsInAb.Id, AssignedAt = now },
            // PDA Studies
            new SharedAutomatonGroupAssignment { GroupId = groupPda.Id, AutomatonId = sharedDpdaBalancedParens.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupPda.Id, AutomatonId = sharedNpdaPalindromes.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupPda.Id, AutomatonId = sharedDpdaAtLeastAsManyAs.Id, AssignedAt = now },
            // Team Playground
            new SharedAutomatonGroupAssignment { GroupId = groupPlayground.Id, AutomatonId = sharedEnfaABorC.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupPlayground.Id, AutomatonId = sharedNfaStartsWithAb.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupPlayground.Id, AutomatonId = sharedDfaEvenLength.Id, AssignedAt = now },
            new SharedAutomatonGroupAssignment { GroupId = groupPlayground.Id, AutomatonId = sharedEnfaOptionalAB.Id, AssignedAt = now });

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---- Pending invitation (demonstrates the invitation / notification system) ----

        dbContext.SharedAutomatonGroupInvitations.Add(new SharedAutomatonGroupInvitation
        {
            GroupId = groupFlt.Id,
            Email = "student5@test.test",
            Role = SharedGroupRole.Viewer,
            InvitedByUserId = supervisorUser.Id,
            Status = InvitationStatus.Pending,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Demo data seeded: 5 users, 22 saved automatons, 7 personal groups, 3 shared groups, 12 shared automatons");
    }

    // ---- Factory helpers ----

    private async Task<ApplicationUser> CreateUserAsync(string email)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            EnableInvitationNotifications = true
        };

        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create demo user '{email}': {errors}");
        }

        return user;
    }

    private static readonly JsonSerializerOptions ContentJsonOptions = new() { WriteIndented = false };

    internal static string ConvertToContentJson(string automatonJson)
    {
        var automaton = AutomatonJsonSerializer.Deserialize(automatonJson);
        var type = automaton switch
        {
            EpsilonNFA => AutomatonType.EpsilonNFA,
            NFA => AutomatonType.NFA,
            DFA => AutomatonType.DFA,
            NPDA => AutomatonType.NPDA,
            DPDA => AutomatonType.DPDA,
            _ => AutomatonType.DFA
        };
        return JsonSerializer.Serialize(
            new ContentPayload { Type = type, States = automaton.States, Transitions = automaton.Transitions },
            ContentJsonOptions);
    }

    private static string MakeInputJson(string input) =>
        JsonSerializer.Serialize(new { Input = input, Position = 0 }, ContentJsonOptions);

    private static string MakeStateJson(
        string input, int position, int currentStateId, bool isAccepted,
        string stateHistorySerialized = "[]") =>
        JsonSerializer.Serialize(new
        {
            Input = input,
            Position = position,
            CurrentStateId = currentStateId,
            CurrentStates = (List<int>?)null,
            IsAccepted = isAccepted,
            StateHistorySerialized = stateHistorySerialized,
            StackSerialized = (string?)null
        }, ContentJsonOptions);

    private sealed class ContentPayload
    {
        public AutomatonType Type { get; set; }
        public List<State> States { get; set; } = [];
        public List<Transition> Transitions { get; set; } = [];
    }

    private static SavedAutomaton Saved(string userId, string name, string description, string json) =>
        new()
        {
            UserId = userId,
            Name = name,
            Description = description,
            ContentJson = ConvertToContentJson(json),
            SaveMode = AutomatonSaveMode.Structure,
            CreatedAt = DateTime.UtcNow
        };

    private static SavedAutomaton SavedWithInput(string userId, string name, string description, string json, string input) =>
        new()
        {
            UserId = userId,
            Name = name,
            Description = description,
            ContentJson = ConvertToContentJson(json),
            SaveMode = AutomatonSaveMode.WithInput,
            ExecutionStateJson = MakeInputJson(input),
            CreatedAt = DateTime.UtcNow
        };

    private static SavedAutomaton SavedWithState(
        string userId, string name, string description, string json,
        string input, int position, int currentStateId, bool isAccepted,
        string stateHistorySerialized = "[]") =>
        new()
        {
            UserId = userId,
            Name = name,
            Description = description,
            ContentJson = ConvertToContentJson(json),
            SaveMode = AutomatonSaveMode.WithState,
            ExecutionStateJson = MakeStateJson(input, position, currentStateId, isAccepted, stateHistorySerialized),
            CreatedAt = DateTime.UtcNow
        };

    private static SavedAutomatonGroup SavedGroup(string userId, string name, string description) =>
        new()
        {
            UserId = userId,
            Name = name,
            Description = description,
            MembersCanShare = true
        };

    private static SharedAutomaton Shared(string userId, string name, string description, string json) =>
        new()
        {
            CreatedByUserId = userId,
            Name = name,
            Description = description,
            ContentJson = ConvertToContentJson(json),
            SaveMode = AutomatonSaveMode.Structure,
            CreatedAt = DateTime.UtcNow
        };

    private static SharedAutomatonGroup SharedGroup(string userId, string name, string description) =>
        new()
        {
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            InviteCode = Guid.NewGuid().ToString("N"),
            IsInviteLinkActive = true,
            DefaultRoleForInvite = SharedGroupRole.Viewer
        };

    private static SharedAutomatonGroupMember Member(
        int groupId, string userId, SharedGroupRole role, DateTime now, string? invitedBy = null) =>
        new()
        {
            GroupId = groupId,
            UserId = userId,
            Role = role,
            JoinedAt = now,
            InvitedByUserId = invitedBy
        };

    internal const string DfaEvenAsJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": true},
            {"Id": 1, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts binary strings whose value is divisible by 3.
    // State tracks remainder (0, 1, 2) when reading digits left-to-right.
    internal const string DfaBinaryDiv3Json = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": true},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "0"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "1"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "0"},
            {"FromStateId": 1, "ToStateId": 0, "Symbol": "1"},
            {"FromStateId": 2, "ToStateId": 1, "Symbol": "0"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "1"}
          ]
        }
        """;

    // NFA: accepts strings over {a,b} that contain the substring "ab".
    // q0 stays in q0 on any input; guesses "a" was start of "ab" and moves to q1.
    internal const string NfaContainsAbJson = """
        {
          "Version": 1,
          "Type": "NFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts strings over {a,b} that end with 'b'.
    internal const string DfaEndsInBJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts strings starting with 'a'. Has an unreachable state (q3 ≡ q1)
    // to demonstrate the minimization feature — minimizing yields a 3-state DFA.
    internal const string DfaStartsWithAMinimizableJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": true},
            {"Id": 2, "IsStart": false, "IsAccepting": false},
            {"Id": 3, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "b"}
          ]
        }
        """;

    // ε-NFA: for the regular expression a(b|c).
    // Thompson-style: q0 -a-> q1 -ε-> q2 -b-> q4; q1 -ε-> q3 -c-> q4.
    internal const string EnfaABorCJson = """
        {
          "Version": 1,
          "Type": "EpsilonNFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": false},
            {"Id": 3, "IsStart": false, "IsAccepting": false},
            {"Id": 4, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "ε"},
            {"FromStateId": 1, "ToStateId": 3, "Symbol": "ε"},
            {"FromStateId": 2, "ToStateId": 4, "Symbol": "b"},
            {"FromStateId": 3, "ToStateId": 4, "Symbol": "c"}
          ]
        }
        """;

    // DPDA: accepts {aⁿbⁿ | n ≥ 0}.
    // Uses the same transition pattern as the integration test suite.
    // StackPop "ε" = no specific pop condition (push freely).
    internal const string DpdaAnBnJson = """
        {
          "Version": 1,
          "Type": "DPDA",
          "States": [
            {"Id": 1, "IsStart": true,  "IsAccepting": true},
            {"Id": 2, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "a", "StackPop": "ε", "StackPush": "X"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b", "StackPop": "X"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "b", "StackPop": "X"}
          ]
        }
        """;

    // DPDA: accepts strings with balanced parentheses.
    // Single state; pushes '(' and pops on ')'. Accepts by empty stack.
    internal const string DpdaBalancedParensJson = """
        {
          "Version": 1,
          "Type": "DPDA",
          "States": [
            {"Id": 1, "IsStart": true, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "(", "StackPop": "ε", "StackPush": "("},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": ")", "StackPop": "("}
          ]
        }
        """;

    // NPDA: accepts even-length palindromes over {a,b} (i.e. strings of the form ww^R).
    // q0 pushes input symbols; nondeterministically guesses midpoint (ε-transition) and
    // switches to q1 which pops while matching the second half.
    internal const string NpdaEvenPalindromesJson = """
        {
          "Version": 1,
          "Type": "NPDA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a", "StackPop": "ε", "StackPush": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b", "StackPop": "ε", "StackPush": "b"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "ε"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "a", "StackPop": "a"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "b", "StackPop": "b"}
          ]
        }
        """;

    // DFA: single accepting state — accepts every string over {a,b}.
    internal const string DfaAcceptAllJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"}
          ]
        }
        """;

    // NFA: accepts strings over {a,b} where the second-to-last symbol is 'a'.
    // Classic example of a language requiring exponentially more states as a DFA.
    internal const string NfaSecondToLastAJson = """
        {
          "Version": 1,
          "Type": "NFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"}
          ]
        }
        """;

    // NFA: accepts all strings over {a,b} that begin with the pattern "ab".
    internal const string NfaStartsWithAbJson = """
        {
          "Version": 1,
          "Type": "NFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "b"}
          ]
        }
        """;

    // ε-NFA: for the regular expression a*b (Thompson construction).
    // q0 -ε-> q1 -a-> q1 (loop) -ε-> q2 -b-> q3 (accept).
    internal const string EnfaStarBJson = """
        {
          "Version": 1,
          "Type": "EpsilonNFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": false},
            {"Id": 3, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "ε"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "ε"},
            {"FromStateId": 2, "ToStateId": 3, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts strings where 'a' and 'b' strictly alternate, starting with 'a'.
    // Accepts: a, ab, aba, abab, ababa, ...
    internal const string DfaAlternatingJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": true},
            {"Id": 2, "IsStart": false, "IsAccepting": true},
            {"Id": 3, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 3, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 3, "Symbol": "b"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts strings over {a,b} containing no 'aa' substring.
    // q0 = start/after-b (accept), q1 = just-read-a (accept), q2 = trap after 'aa'.
    internal const string DfaNoConsecAsJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": true},
            {"Id": 1, "IsStart": false, "IsAccepting": true},
            {"Id": 2, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "b"}
          ]
        }
        """;

    // NFA: accepts strings over {a,b} that end with the two-symbol suffix 'ab'.
    // q0 self-loops on both symbols; guesses on 'a' that this is the start of 'ab'.
    internal const string NfaEndsInAbJson = """
        {
          "Version": 1,
          "Type": "NFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts all strings over {a,b} of even length (including the empty string).
    // q0 (start, accept) ↔ q1 on every input symbol.
    internal const string DfaEvenLengthJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": true},
            {"Id": 1, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 0, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts strings over {a,b} containing exactly two 'a' characters.
    // States count 0, 1, 2 (accept), 3+ (dead).
    internal const string DfaExactlyTwoAsJson = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": true},
            {"Id": 3, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "b"}
          ]
        }
        """;

    // DPDA: accepts aⁿbᵐ where n ≥ m ≥ 0 (at least as many a's as b's).
    // Pushes X for each 'a'; pops X for each 'b'. Accepts by final state — leftover X's are fine.
    internal const string DpdaAtLeastAsManyAsJson = """
        {
          "Version": 1,
          "Type": "DPDA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": true},
            {"Id": 1, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a", "StackPop": "ε", "StackPush": "X"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "b", "StackPop": "X"},
            {"FromStateId": 1, "ToStateId": 1, "Symbol": "b", "StackPop": "X"}
          ]
        }
        """;

    // NFA: accepts strings over {a,b} that contain 'aba' as a substring.
    // q0 self-loops; guesses on 'a' that 'aba' starts here; q3 self-loops once matched.
    internal const string NfaContainsAbaJson = """
        {
          "Version": 1,
          "Type": "NFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": false},
            {"Id": 3, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "a"},
            {"FromStateId": 0, "ToStateId": 0, "Symbol": "b"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"},
            {"FromStateId": 2, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "a"},
            {"FromStateId": 3, "ToStateId": 3, "Symbol": "b"}
          ]
        }
        """;

    // DFA: accepts strings over {a} whose length is divisible by 3.
    // Three-state cycle: q0 (start, accept) -> q1 -> q2 -> q0.
    internal const string DfaLengthDivBy3Json = """
        {
          "Version": 1,
          "Type": "DFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": true},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": false}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "a"},
            {"FromStateId": 2, "ToStateId": 0, "Symbol": "a"}
          ]
        }
        """;

    // ε-NFA: for the expression (a|ε)b — accepts 'b' and 'ab'.
    // q0 branches via ε (skip 'a') or reads 'a'; both paths reach q1 which reads 'b'.
    internal const string EnfaOptionalABJson = """
        {
          "Version": 1,
          "Type": "EpsilonNFA",
          "States": [
            {"Id": 0, "IsStart": true,  "IsAccepting": false},
            {"Id": 1, "IsStart": false, "IsAccepting": false},
            {"Id": 2, "IsStart": false, "IsAccepting": true}
          ],
          "Transitions": [
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "ε"},
            {"FromStateId": 0, "ToStateId": 1, "Symbol": "a"},
            {"FromStateId": 1, "ToStateId": 2, "Symbol": "b"}
          ]
        }
        """;
}
