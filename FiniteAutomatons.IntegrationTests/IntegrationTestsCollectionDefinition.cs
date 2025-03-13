using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiniteAutomatons.IntegrationTests;

[CollectionDefinition(CollectionName, DisableParallelization = false)]
public class IntegrationTestCollectionDefinition : ICollectionFixture<IntegrationTestsFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
    public const string CollectionName = "Integration Tests";
}
