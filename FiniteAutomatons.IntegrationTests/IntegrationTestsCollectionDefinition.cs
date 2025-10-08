namespace FiniteAutomatons.IntegrationTests;

[CollectionDefinition(CollectionName, DisableParallelization = false)]
public class IntegrationTestCollectionDefinition : ICollectionFixture<IntegrationTestsFixture>
{
    public const string CollectionName = "Integration Tests";
}
