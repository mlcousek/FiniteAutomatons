namespace FiniteAutomatons.Core.Configuration;

/// <summary>
/// Global configuration for PDA execution safety limits.
/// These defaults match the historical values used in the PDA implementation and
/// can be adjusted in tests or by higher-level services to limit resource usage.
/// This is intentionally a simple POCO with static properties to avoid introducing
/// service dependencies into the core domain types (PDA is domain-level code).
/// </summary>
public static class PdaExecutionSettings
{
    /// <summary>
    /// Maximum number of BFS / configuration expansions when searching for a consuming transition.
    /// Historically this was 10_000.
    /// </summary>
    public static int MaxBfsExpansion { get; set; } = 10_000;

    /// <summary>
    /// Maximum number of BFS / closure iterations when exploring epsilon-only moves.
    /// Historically this was 1_000.
    /// </summary>
    public static int MaxEpsilonIterations { get; set; } = 1_000;

    /// <summary>
    /// Soft upper bound for stack growth during epsilon-push loops used in tests/validation.
    /// Default chosen to be slightly larger than MaxEpsilonIterations to account for pushes.
    /// </summary>
    public static int MaxStackGrowthTolerance { get; set; } = 1_100;
}
