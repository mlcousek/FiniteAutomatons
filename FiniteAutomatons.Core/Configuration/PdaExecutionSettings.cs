namespace FiniteAutomatons.Core.Configuration;

public static class PdaExecutionSettings
{
    public static int MaxBfsExpansion { get; set; } = 10_000;

    public static int MaxEpsilonIterations { get; set; } = 1_000;

    public static int MaxStackGrowthTolerance { get; set; } = 1_100;
}
