using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerFileServiceTests
{
    [Fact]
    public async Task ImportAutomaton_NoFile_ShowsError()
    {
        var controller = Build();
        var result = await controller.ImportAutomaton(null!);
        result.ShouldBeOfType<ViewResult>();
    }

    private static AutomatonController Build()
    {
        var controller = new AutomatonController(new TestLogger<AutomatonController>(), new MockAutomatonGeneratorService(), new MockAutomatonTempDataService(), new MockAutomatonValidationService(), new MockAutomatonConversionService(), new MockAutomatonExecutionService(), new AutomatonEditingService(new MockAutomatonValidationService(), new TestLogger<AutomatonEditingService>()), new MockAutomatonFileService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        return controller;
    }
}
