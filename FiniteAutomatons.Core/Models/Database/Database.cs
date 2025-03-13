namespace FiniteAutomatons.Core.Models.Database;

public class DatabaseSettings
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool TrustServerCertificate { get; set; } = true;

    public string GetConnectionString()
    {
        return $"Server={Server};Database={Database};User Id={UserId};Password={Password};Trust Server Certificate={TrustServerCertificate}";
    }
}
