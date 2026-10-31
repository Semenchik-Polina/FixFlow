using FixFlow.Domain.Organizations;

namespace FixFlow.API.IntegrationTests.Fixtures;

public static partial class TestData
{
    /// <summary>
    /// Each of the fixed roles once, for theories over roles:
    /// <c>[MemberData(nameof(TestData.EveryRole), MemberType = typeof(TestData))]</c>.
    /// </summary>
    public static TheoryData<Role> EveryRole => [.. Enum.GetValues<Role>()];
}
