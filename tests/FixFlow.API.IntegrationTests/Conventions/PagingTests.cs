using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Paging;

namespace FixFlow.API.IntegrationTests.Conventions;

public class PagingTests(FixFlowApiFactory api)
{
    [Theory]
    [InlineData("page=1&pageSize=101", "pageSize")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=0&pageSize=25", "page")]
    public async Task List_OutOfRange_Returns400WithFieldError(string query, string field)
    {
        using var client = api.CreateApiClient();

        using var response = await client.GetAsync(
            new Uri($"{ConventionTestEndpoints.Path}/samples?{query}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal([field], problem.FieldErrors().Keys);
    }

    [Fact]
    public async Task List_MaxPageSize_ReturnsPageWithTotalCount()
    {
        using var client = api.CreateApiClient();

        var page = await client.GetFromJsonAsync<JsonElement>(
            new Uri($"{ConventionTestEndpoints.Path}/samples?page=2&pageSize={PageSizes.Max}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(PageSizes.Max, page.GetProperty("items").GetArrayLength());
        Assert.Equal(PageSizes.Max + 1, page.GetProperty("items")[0].GetInt32());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(PageSizes.Max, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(ListSamples.TotalCount, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_NoPagingParameters_ReturnsFirstPageOfDefaultSize()
    {
        using var client = api.CreateApiClient();

        var page = await client.GetFromJsonAsync<JsonElement>(
            new Uri($"{ConventionTestEndpoints.Path}/samples", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(PageSizes.Default, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(PageSizes.Default, page.GetProperty("items").GetArrayLength());
    }
}
