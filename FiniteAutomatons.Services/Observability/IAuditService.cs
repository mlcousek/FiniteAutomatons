namespace FiniteAutomatons.Observability;

public interface IAuditService
{
    Task AuditAsync(string eventType, string message, IDictionary<string, string?>? data = null);
}
