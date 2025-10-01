using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonConversionServiceAuditorDecorator : IAutomatonConversionService
{
    private readonly AutomatonConversionService _inner;
    private readonly IAuditService _audit;

    public AutomatonConversionServiceAuditorDecorator(AutomatonConversionService inner, IAuditService audit)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonConversionService.ConvertAutomatonType", () =>
            Task.FromResult(_inner.ConvertAutomatonType(model, newType))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ConvertToDFA(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonConversionService.ConvertToDFA", () =>
            Task.FromResult(_inner.ConvertToDFA(model))).GetAwaiter().GetResult();
    }
}
