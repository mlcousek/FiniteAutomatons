using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace FiniteAutomatons.Observability;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AuditAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _eventType;

    public AuditAttribute(string eventType)
    {
        _eventType = eventType;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auditService = context.HttpContext.RequestServices.GetService(typeof(IAuditService)) as IAuditService;
        var route = context.ActionDescriptor.DisplayName ?? context.HttpContext.Request.Path;
        var data = new Dictionary<string, string?>();
        foreach (var kv in context.ActionArguments)
        {
            data[kv.Key] = kv.Value?.ToString();
        }

        string? traceId = Activity.Current?.TraceId.ToString();
        if (traceId != null) data["TraceId"] = traceId;

        if (auditService != null)
        {
            await auditService.AuditAsync("ActionStart", $"Starting {route}", data);
        }

        var executed = await next();

        var result = executed.Result?.ToString();
        var endData = new Dictionary<string, string?>();
        if (traceId != null) endData["TraceId"] = traceId;
        if (auditService != null)
        {
            await auditService.AuditAsync("ActionEnd", $"Finished {route} - {result}", endData);
        }
    }
}
