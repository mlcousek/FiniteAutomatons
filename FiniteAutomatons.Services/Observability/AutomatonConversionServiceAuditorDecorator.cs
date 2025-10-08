using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonConversionServiceAuditorDecorator(AutomatonConversionService inner, IAuditService audit) : IAutomatonConversionService
{
    private readonly AutomatonConversionService inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IAuditService audit = audit ?? throw new ArgumentNullException(nameof(audit));

    public (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonConversionService.ConvertAutomatonType", () =>
            Task.FromResult(inner.ConvertAutomatonType(model, newType))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ConvertToDFA(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonConversionService.ConvertToDFA", () =>
            Task.FromResult(inner.ConvertToDFA(model))).GetAwaiter().GetResult();
    }
}
