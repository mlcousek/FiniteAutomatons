using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonFileServiceGroupTests
{
    private static AutomatonFileService Create() => new(new NullLogger<AutomatonFileService>());

    private static FormFile CreateFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };
    }

    #region ExportGroup Tests

    [Fact]
    public void ExportGroup_ValidGroup_CreatesCorrectStructure()
    {
        var svc = Create();
        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "DFA Example",
                Description = "Test DFA",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
                }),
                SaveMode = AutomatonSaveMode.Structure,
                ExecutionStateJson = null
            }
        };

        var (fileName, content) = svc.ExportGroup("Test Group", "Group Description", automatons);

        fileName.ShouldNotBeNullOrEmpty();
        fileName.ShouldContain("Test");
        fileName.ShouldContain("Group");
        fileName.ShouldContain("export");
        fileName.ShouldEndWith(".json");

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported.ShouldNotBeNull();
        exported!.GroupName.ShouldBe("Test Group");
        exported.GroupDescription.ShouldBe("Group Description");
        exported.Automatons.Count.ShouldBe(1);
        exported.Automatons[0].Name.ShouldBe("DFA Example");
    }

    [Fact]
    public void ExportGroup_NullGroupName_ThrowsArgumentNullException()
    {
        var svc = Create();
        var automatons = new List<SavedAutomaton>();

        Should.Throw<ArgumentNullException>(() => svc.ExportGroup(null!, "desc", automatons));
    }

    [Fact]
    public void ExportGroup_NullAutomatonsList_ThrowsArgumentNullException()
    {
        var svc = Create();

        Should.Throw<ArgumentNullException>(() => svc.ExportGroup("Group", "desc", null!));
    }

    [Fact]
    public void ExportGroup_EmptyAutomatonsList_ExportsSuccessfully()
    {
        var svc = Create();

        var (fileName, content) = svc.ExportGroup("Empty Group", null, []);

        fileName.ShouldNotBeNullOrEmpty();
        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.ShouldBeEmpty();
    }

    [Fact]
    public void ExportGroup_MultipleAutomatons_ExportsAll()
    {
        var svc = Create();
        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "Auto1",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.Structure
            },
            new()
            {
                Id = 2,
                Name = "Auto2",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.NFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.Structure
            }
        };

        var (_, content) = svc.ExportGroup("Multi Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.Count.ShouldBe(2);
        exported.Automatons[0].Name.ShouldBe("Auto1");
        exported.Automatons[1].Name.ShouldBe("Auto2");
    }

    [Fact]
    public void ExportGroup_WithExecutionState_IncludesExecutionState()
    {
        var svc = Create();
        var execState = new SavedExecutionStateDto
        {
            Input = "test",
            Position = 2,
            CurrentStateId = 1,
            IsAccepted = null
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "With State",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithState,
                ExecutionStateJson = JsonSerializer.Serialize(execState)
            }
        };

        var (_, content) = svc.ExportGroup("State Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons[0].HasExecutionState.ShouldBeTrue();
        exported.Automatons[0].ExecutionState.ShouldNotBeNull();
        exported.Automatons[0].ExecutionState!.Input.ShouldBe("test");
        exported.Automatons[0].ExecutionState!.Position.ShouldBe(2);
    }

    [Fact]
    public void ExportGroup_WithInvalidContentJson_ContinuesWithEmptyPayload()
    {
        var svc = Create();
        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "Invalid",
                ContentJson = "not valid json",
                SaveMode = AutomatonSaveMode.Structure
            }
        };

        var (_, content) = svc.ExportGroup("Invalid Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.Count.ShouldBe(1);
        exported.Automatons[0].Content.ShouldNotBeNull();
    }

    [Fact]
    public void ExportGroup_SpecialCharactersInGroupName_SanitizesFileName()
    {
        var svc = Create();

        var (fileName, _) = svc.ExportGroup("Test/Group:Name*", null, []);

        fileName.ShouldNotContain("/");
        fileName.ShouldNotContain(":");
        fileName.ShouldNotContain("*");
        fileName.ShouldContain("_");
    }

    [Fact]
    public void ExportGroup_NullDescription_ExportsSuccessfully()
    {
        var svc = Create();
        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "Test",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto()),
                SaveMode = AutomatonSaveMode.Structure
            }
        };

        var (_, content) = svc.ExportGroup("No Description", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.GroupDescription.ShouldBeNull();
    }

    [Fact]
    public void ExportGroup_AllAutomatonTypes_ExportsCorrectly()
    {
        var svc = Create();
        var automatons = new List<SavedAutomaton>
        {
            CreateSavedAutomaton(1, "DFA", AutomatonType.DFA),
            CreateSavedAutomaton(2, "NFA", AutomatonType.NFA),
            CreateSavedAutomaton(3, "EpsilonNFA", AutomatonType.EpsilonNFA),
            CreateSavedAutomaton(4, "PDA", AutomatonType.PDA)
        };

        var (_, content) = svc.ExportGroup("All Types", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.Count.ShouldBe(4);
        exported.Automatons[0].Content.Type.ShouldBe(AutomatonType.DFA);
        exported.Automatons[1].Content.Type.ShouldBe(AutomatonType.NFA);
        exported.Automatons[2].Content.Type.ShouldBe(AutomatonType.EpsilonNFA);
        exported.Automatons[3].Content.Type.ShouldBe(AutomatonType.PDA);
    }

    #endregion

    #region ImportGroupAsync Tests

    [Fact]
    public async Task ImportGroupAsync_ValidFile_ParsesCorrectly()
    {
        var svc = Create();
        var groupData = new GroupExportDto
        {
            GroupName = "Imported Group",
            GroupDescription = "Test import",
            ExportedAt = DateTime.UtcNow,
            Automatons = [
                new AutomatonExportItemDto
                {
                    Name = "Import Auto",
                    Description = "Test",
                    HasExecutionState = false,
                    Content = new AutomatonPayloadDto
                    {
                        Type = AutomatonType.DFA,
                        States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                        Transitions = []
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(groupData);
        var file = CreateFormFile("import.json", json);

        var (ok, imported, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeTrue(error);
        imported.ShouldNotBeNull();
        imported!.GroupName.ShouldBe("Imported Group");
        imported.Automatons.Count.ShouldBe(1);
        imported.Automatons[0].Name.ShouldBe("Import Auto");
    }

    [Fact]
    public async Task ImportGroupAsync_NullFile_ReturnsError()
    {
        var svc = Create();

        var (ok, data, error) = await svc.ImportGroupAsync(null!);

        ok.ShouldBeFalse();
        data.ShouldBeNull();
        error.ShouldBe("No file uploaded.");
    }

    [Fact]
    public async Task ImportGroupAsync_EmptyFile_ReturnsError()
    {
        var svc = Create();
        var file = CreateFormFile("empty.json", "");

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeFalse();
        data.ShouldBeNull();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ImportGroupAsync_InvalidJson_ReturnsError()
    {
        var svc = Create();
        var file = CreateFormFile("invalid.json", "not valid json");

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeFalse();
        data.ShouldBeNull();
        error.ShouldNotBeNullOrEmpty();
        error!.ShouldContain("JSON");
    }

    [Fact]
    public async Task ImportGroupAsync_EmptyAutomatonsList_ReturnsError()
    {
        var svc = Create();
        var groupData = new GroupExportDto
        {
            GroupName = "Empty",
            Automatons = []
        };

        var json = JsonSerializer.Serialize(groupData);
        var file = CreateFormFile("empty.json", json);

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeFalse();
        data.ShouldBeNull();
        error.ShouldNotBeNullOrEmpty();
        error!.ShouldContain("no automatons");
    }

    [Fact]
    public async Task ImportGroupAsync_NullAutomatonsList_ReturnsError()
    {
        var svc = Create();
        var json = """{"GroupName": "Test", "Automatons": null}""";
        var file = CreateFormFile("null.json", json);

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeFalse();
        data.ShouldBeNull();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ImportGroupAsync_CaseInsensitivePropertyNames_ParsesCorrectly()
    {
        var svc = Create();
        var json = """
        {
            "groupname": "Test",
            "groupdescription": "Desc",
            "exportedat": "2025-01-01T00:00:00Z",
            "automatons": [
                {
                    "name": "Auto",
                    "hasexecutionstate": false,
                    "content": {
                        "type": 0,
                        "states": [{"id": 1, "isStart": true, "isAccepting": true}],
                        "transitions": []
                    }
                }
            ]
        }
        """;
        var file = CreateFormFile("case.json", json);

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeTrue(error);
        data!.GroupName.ShouldBe("Test");
    }

    [Fact]
    public async Task ImportGroupAsync_WithExecutionState_PreservesState()
    {
        var svc = Create();
        var groupData = new GroupExportDto
        {
            GroupName = "With State",
            Automatons = [
                new AutomatonExportItemDto
                {
                    Name = "Stateful",
                    HasExecutionState = true,
                    Content = new AutomatonPayloadDto
                    {
                        Type = AutomatonType.DFA,
                        States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                        Transitions = []
                    },
                    ExecutionState = new SavedExecutionStateDto
                    {
                        Input = "abc",
                        Position = 2,
                        CurrentStateId = 1
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(groupData);
        var file = CreateFormFile("state.json", json);

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeTrue(error);
        data!.Automatons[0].HasExecutionState.ShouldBeTrue();
        data.Automatons[0].ExecutionState!.Input.ShouldBe("abc");
        data.Automatons[0].ExecutionState!.Position.ShouldBe(2);
    }

    [Fact]
    public async Task ImportGroupAsync_MultipleAutomatons_ParsesAll()
    {
        var svc = Create();
        var groupData = new GroupExportDto
        {
            GroupName = "Multiple",
            Automatons = [
                new AutomatonExportItemDto { Name = "First", Content = new AutomatonPayloadDto { Type = AutomatonType.DFA, States = [], Transitions = [] } },
                new AutomatonExportItemDto { Name = "Second", Content = new AutomatonPayloadDto { Type = AutomatonType.NFA, States = [], Transitions = [] } },
                new AutomatonExportItemDto { Name = "Third", Content = new AutomatonPayloadDto { Type = AutomatonType.PDA, States = [], Transitions = [] } }
            ]
        };

        var json = JsonSerializer.Serialize(groupData);
        var file = CreateFormFile("multi.json", json);

        var (ok, data, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeTrue(error);
        data!.Automatons.Count.ShouldBe(3);
        data.Automatons[0].Name.ShouldBe("First");
        data.Automatons[1].Name.ShouldBe("Second");
        data.Automatons[2].Name.ShouldBe("Third");
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public async Task RoundTrip_ExportAndImport_PreservesData()
    {
        var svc = Create();
        var original = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "Round Trip",
                Description = "Test description",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.NFA,
                    States = [
                        new() { Id = 1, IsStart = true, IsAccepting = false },
                        new() { Id = 2, IsStart = false, IsAccepting = true }
                    ],
                    Transitions = [
                        new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
                    ]
                }),
                SaveMode = AutomatonSaveMode.Structure
            }
        };

        var (_, exported) = svc.ExportGroup("Round Trip", "Test round trip", original);
        var file = CreateFormFile("roundtrip.json", exported);
        var (ok, imported, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeTrue(error);
        imported!.GroupName.ShouldBe("Round Trip");
        imported.GroupDescription.ShouldBe("Test round trip");
        imported.Automatons.Count.ShouldBe(1);
        imported.Automatons[0].Name.ShouldBe("Round Trip");
        imported.Automatons[0].Content.Type.ShouldBe(AutomatonType.NFA);
        imported.Automatons[0].Content.States!.Count.ShouldBe(2);
        imported.Automatons[0].Content.Transitions!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RoundTrip_WithExecutionState_PreservesEverything()
    {
        var svc = Create();
        var execState = new SavedExecutionStateDto
        {
            Input = "test123",
            Position = 3,
            CurrentStateId = 2,
            IsAccepted = null,
            StateHistorySerialized = "[1,2]"
        };

        var original = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "With Exec State",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
                    Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 't' }]
                }),
                SaveMode = AutomatonSaveMode.WithState,
                ExecutionStateJson = JsonSerializer.Serialize(execState)
            }
        };

        var (_, exported) = svc.ExportGroup("State Round Trip", null, original);
        var file = CreateFormFile("state-rt.json", exported);
        var (ok, imported, error) = await svc.ImportGroupAsync(file);

        ok.ShouldBeTrue(error);
        imported!.Automatons[0].HasExecutionState.ShouldBeTrue();
        imported.Automatons[0].ExecutionState!.Input.ShouldBe("test123");
        imported.Automatons[0].ExecutionState!.Position.ShouldBe(3);
        imported.Automatons[0].ExecutionState!.StateHistorySerialized.ShouldBe("[1,2]");
    }

    #endregion

    #region Helper Methods

    private static SavedAutomaton CreateSavedAutomaton(int id, string name, AutomatonType type)
    {
        return new SavedAutomaton
        {
            Id = id,
            Name = name,
            ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
            {
                Type = type,
                States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                Transitions = []
            }),
            SaveMode = AutomatonSaveMode.Structure
        };
    }

    #endregion

    #region ExportGroup SaveMode Integration Tests

    [Fact]
    public void ExportGroup_MixedSaveModes_ExportsCorrectly()
    {
        var svc = Create();

        var execState1 = new SavedExecutionStateDto
        {
            Input = "test1",
            Position = 2,
            CurrentStateId = 1,
            IsAccepted = true,
            StateHistorySerialized = "[1]"
        };

        var execState2 = new SavedExecutionStateDto
        {
            Input = "test2",
            Position = 0,
            CurrentStateId = null,
            IsAccepted = null,
            StateHistorySerialized = string.Empty
        };

        var automatons = new List<SavedAutomaton>
        {
            // Structure only
            new()
            {
                Id = 1,
                Name = "StructureOnly",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.Structure,
                ExecutionStateJson = null
            },
            // With input
            new()
            {
                Id = 2,
                Name = "WithInput",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithInput,
                ExecutionStateJson = JsonSerializer.Serialize(execState2)
            },
            // With state
            new()
            {
                Id = 3,
                Name = "WithState",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithState,
                ExecutionStateJson = JsonSerializer.Serialize(execState1)
            }
        };

        var (_, content) = svc.ExportGroup("Mixed Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported.ShouldNotBeNull();
        exported!.Automatons.Count.ShouldBe(3);

        // Structure only - no execution state
        exported.Automatons[0].Name.ShouldBe("StructureOnly");
        exported.Automatons[0].HasExecutionState.ShouldBeFalse();
        exported.Automatons[0].ExecutionState.ShouldBeNull();

        // With input - has execution state with only input
        exported.Automatons[1].Name.ShouldBe("WithInput");
        exported.Automatons[1].HasExecutionState.ShouldBeFalse();
        exported.Automatons[1].ExecutionState.ShouldNotBeNull();
        exported.Automatons[1].ExecutionState!.Input.ShouldBe("test2");
        exported.Automatons[1].ExecutionState!.Position.ShouldBe(0);
        exported.Automatons[1].ExecutionState!.CurrentStateId.ShouldBeNull();
        exported.Automatons[1].ExecutionState!.IsAccepted.ShouldBeNull();

        // With state - has full execution state
        exported.Automatons[2].Name.ShouldBe("WithState");
        exported.Automatons[2].HasExecutionState.ShouldBeTrue();
        exported.Automatons[2].ExecutionState.ShouldNotBeNull();
        exported.Automatons[2].ExecutionState!.Input.ShouldBe("test1");
        exported.Automatons[2].ExecutionState!.Position.ShouldBe(2);
        exported.Automatons[2].ExecutionState!.CurrentStateId.ShouldBe(1);
        exported.Automatons[2].ExecutionState!.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void ExportGroup_AllStructureMode_NoExecutionState()
    {
        var svc = Create();

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "Auto1",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.Structure
            },
            new()
            {
                Id = 2,
                Name = "Auto2",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.NFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.Structure
            }
        };

        var (_, content) = svc.ExportGroup("Structure Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.All(a => !a.HasExecutionState).ShouldBeTrue();
        exported.Automatons.All(a => a.ExecutionState == null).ShouldBeTrue();
    }

    [Fact]
    public void ExportGroup_AllWithInputMode_ExportsOnlyInput()
    {
        var svc = Create();

        var execState = new SavedExecutionStateDto
        {
            Input = "abc",
            Position = 3,
            CurrentStateId = 1,
            IsAccepted = false,
            StateHistorySerialized = "[1,1,1]"
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "Input1",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithInput,
                ExecutionStateJson = JsonSerializer.Serialize(execState)
            }
        };

        var (_, content) = svc.ExportGroup("Input Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons[0].HasExecutionState.ShouldBeFalse();
        exported.Automatons[0].ExecutionState.ShouldNotBeNull();
        exported.Automatons[0].ExecutionState!.Input.ShouldBe("abc");
        exported.Automatons[0].ExecutionState!.Position.ShouldBe(0);
        exported.Automatons[0].ExecutionState!.CurrentStateId.ShouldBeNull();
        exported.Automatons[0].ExecutionState!.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExportGroup_AllWithStateMode_ExportsFullState()
    {
        var svc = Create();

        var execState1 = new SavedExecutionStateDto
        {
            Input = "test1",
            Position = 1,
            CurrentStateId = 1,
            IsAccepted = true,
            StateHistorySerialized = "[1]",
            StackSerialized = null
        };

        var execState2 = new SavedExecutionStateDto
        {
            Input = "test2",
            Position = 2,
            CurrentStateId = 2,
            IsAccepted = false,
            StateHistorySerialized = "[1,2]",
            StackSerialized = "#AB"
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "State1",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithState,
                ExecutionStateJson = JsonSerializer.Serialize(execState1)
            },
            new()
            {
                Id = 2,
                Name = "State2",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.PDA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithState,
                ExecutionStateJson = JsonSerializer.Serialize(execState2)
            }
        };

        var (_, content) = svc.ExportGroup("State Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.All(a => a.HasExecutionState).ShouldBeTrue();

        exported.Automatons[0].ExecutionState!.Input.ShouldBe("test1");
        exported.Automatons[0].ExecutionState!.Position.ShouldBe(1);
        exported.Automatons[0].ExecutionState!.CurrentStateId.ShouldBe(1);
        exported.Automatons[0].ExecutionState!.IsAccepted.ShouldBe(true);

        exported.Automatons[1].ExecutionState!.Input.ShouldBe("test2");
        exported.Automatons[1].ExecutionState!.Position.ShouldBe(2);
        exported.Automatons[1].ExecutionState!.StackSerialized.ShouldBe("#AB");
    }

    [Fact]
    public void ExportGroup_WithInputMode_ClearsExecutionFields()
    {
        var svc = Create();

        var fullExecState = new SavedExecutionStateDto
        {
            Input = "input123",
            Position = 5,
            CurrentStateId = 3,
            CurrentStates = [1, 2, 3],
            IsAccepted = true,
            StateHistorySerialized = "[1,2,3]",
            StackSerialized = "#XYZ"
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "ClearState",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.PDA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithInput,
                ExecutionStateJson = JsonSerializer.Serialize(fullExecState)
            }
        };

        var (_, content) = svc.ExportGroup("Clear Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        var execState = exported!.Automatons[0].ExecutionState!;

        execState.Input.ShouldBe("input123");
        execState.Position.ShouldBe(0);
        execState.CurrentStateId.ShouldBeNull();
        execState.CurrentStates.ShouldBeNull();
        execState.IsAccepted.ShouldBeNull();
        execState.StateHistorySerialized.ShouldBe(string.Empty);
        execState.StackSerialized.ShouldBeNull();
    }

    [Fact]
    public void ExportGroup_StructureMode_WithExecutionStateJson_IgnoresIt()
    {
        var svc = Create();

        var execState = new SavedExecutionStateDto
        {
            Input = "shouldnotappear",
            Position = 1,
            CurrentStateId = 1
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "IgnoreExec",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.Structure,
                ExecutionStateJson = JsonSerializer.Serialize(execState)
            }
        };

        var (_, content) = svc.ExportGroup("Ignore Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons[0].ExecutionState.ShouldBeNull();
    }

    [Fact]
    public void ExportGroup_EmptyInput_WithInputMode_ExportsEmptyString()
    {
        var svc = Create();

        var execState = new SavedExecutionStateDto
        {
            Input = string.Empty,
            Position = 0,
            CurrentStateId = null
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "EmptyInput",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithInput,
                ExecutionStateJson = JsonSerializer.Serialize(execState)
            }
        };

        var (_, content) = svc.ExportGroup("Empty Input Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons[0].ExecutionState.ShouldNotBeNull();
        exported.Automatons[0].ExecutionState!.Input.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExportGroup_InvalidExecutionStateJson_HandlesGracefully()
    {
        var svc = Create();

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "InvalidJson",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithInput,
                ExecutionStateJson = "invalid json {"
            }
        };

        var (_, content) = svc.ExportGroup("Invalid Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons[0].ExecutionState.ShouldBeNull();
    }

    [Fact]
    public void ExportGroup_NullCurrentStates_WithStateMode_ExportsCorrectly()
    {
        var svc = Create();

        var execState = new SavedExecutionStateDto
        {
            Input = "test",
            Position = 1,
            CurrentStateId = 1,
            CurrentStates = null,
            IsAccepted = false,
            StateHistorySerialized = "[1]"
        };

        var automatons = new List<SavedAutomaton>
        {
            new()
            {
                Id = 1,
                Name = "NullStates",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = AutomatonSaveMode.WithState,
                ExecutionStateJson = JsonSerializer.Serialize(execState)
            }
        };

        var (_, content) = svc.ExportGroup("Null States Group", null, automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons[0].ExecutionState.ShouldNotBeNull();
        exported.Automatons[0].ExecutionState!.CurrentStates.ShouldBeNull();
        exported.Automatons[0].ExecutionState!.Position.ShouldBe(1);
    }

    [Fact]
    public void ExportGroup_LargeGroup_AllSaveModes_ProcessesCorrectly()
    {
        var svc = Create();

        var automatons = new List<SavedAutomaton>();
        for (int i = 0; i < 10; i++)
        {
            var saveMode = (AutomatonSaveMode)(i % 3);
            var execState = saveMode >= AutomatonSaveMode.WithInput
                ? JsonSerializer.Serialize(new SavedExecutionStateDto
                {
                    Input = $"input{i}",
                    Position = i,
                    CurrentStateId = 1,
                    IsAccepted = i % 2 == 0
                })
                : null;

            automatons.Add(new SavedAutomaton
            {
                Id = i,
                Name = $"Auto{i}",
                ContentJson = JsonSerializer.Serialize(new AutomatonPayloadDto
                {
                    Type = AutomatonType.DFA,
                    States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                    Transitions = []
                }),
                SaveMode = saveMode,
                ExecutionStateJson = execState
            });
        }

        var (_, content) = svc.ExportGroup("Large Group", "Testing large export", automatons);

        var exported = JsonSerializer.Deserialize<GroupExportDto>(content);
        exported!.Automatons.Count.ShouldBe(10);

        // Verify structure automatons
        foreach (var auto in exported.Automatons.Where(a => a.Name!.Contains("0") || a.Name!.Contains("3") || a.Name!.Contains("6") || a.Name!.Contains("9")))
        {
            auto.ExecutionState.ShouldBeNull();
        }

        // Verify input automatons
        foreach (var auto in exported.Automatons.Where(a => a.Name!.Contains("1") || a.Name!.Contains("4") || a.Name!.Contains("7")))
        {
            auto.ExecutionState.ShouldNotBeNull();
            auto.ExecutionState!.Position.ShouldBe(0);
            auto.ExecutionState!.CurrentStateId.ShouldBeNull();
        }

        // Verify state automatons  
        foreach (var auto in exported.Automatons.Where(a => a.Name!.Contains("2") || a.Name!.Contains("5") || a.Name!.Contains("8")))
        {
            auto.HasExecutionState.ShouldBeTrue();
            auto.ExecutionState.ShouldNotBeNull();
            auto.ExecutionState!.Position.ShouldBeGreaterThan(0);
        }
    }

    #endregion
}

