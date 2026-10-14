using FixFlow.API.IntegrationTests.Fixtures;

// One API host over one PostgreSQL and one Azurite container for the whole run
[assembly: AssemblyFixture(typeof(FixFlowApiFactory))]
