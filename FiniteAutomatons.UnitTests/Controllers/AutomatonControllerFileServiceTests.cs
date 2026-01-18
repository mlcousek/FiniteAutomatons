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
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    private static ImportExportController Build()
    {
        var fileSvc = new MockAutomatonFileService();
        var controller = new ImportExportController(fileSvc)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        return controller;
    }
}
