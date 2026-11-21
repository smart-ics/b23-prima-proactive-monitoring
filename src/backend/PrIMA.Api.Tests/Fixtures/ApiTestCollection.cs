using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true, MaxParallelThreads = 1)]

namespace PrIMA.Api.Tests.Fixtures;

[CollectionDefinition("ApiTestCollection", DisableParallelization = true)]
public class ApiTestCollection : ICollectionFixture<PrIMAApiFactory>
{
}
