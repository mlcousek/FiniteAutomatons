using System.Diagnostics;

namespace FiniteAutomatons.Observability;

public static class MethodAuditor
{
    public static async Task<T> AuditAsync<T>(IAuditService audit, string name, Func<Task<T>> func)
    {
        var sw = Stopwatch.StartNew();
        await audit.AuditAsync("MethodStart", name);
        try
        {
            var res = await func();
            sw.Stop();
            await audit.AuditAsync("MethodEnd", name, new Dictionary<string, string?> { ["DurationMs"] = sw.ElapsedMilliseconds.ToString() });
            return res;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await audit.AuditAsync("MethodError", name, new Dictionary<string, string?> { ["Exception"] = ex.ToString(), ["DurationMs"] = sw.ElapsedMilliseconds.ToString() });
            throw;
        }
    }

    public static async Task AuditAsync(IAuditService audit, string name, Func<Task> func)
    {
        var sw = Stopwatch.StartNew();
        await audit.AuditAsync("MethodStart", name);
        try
        {
            await func();
            sw.Stop();
            await audit.AuditAsync("MethodEnd", name, new Dictionary<string, string?> { ["DurationMs"] = sw.ElapsedMilliseconds.ToString() });
        }
        catch (Exception ex)
        {
            sw.Stop();
            await audit.AuditAsync("MethodError", name, new Dictionary<string, string?> { ["Exception"] = ex.ToString(), ["DurationMs"] = sw.ElapsedMilliseconds.ToString() });
            throw;
        }
    }
}
