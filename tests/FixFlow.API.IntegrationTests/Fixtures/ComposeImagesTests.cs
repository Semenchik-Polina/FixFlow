namespace FixFlow.API.IntegrationTests.Fixtures;

public class ComposeImagesTests
{
    [Theory]
    [InlineData(ComposeImages.Postgres)]
    [InlineData(ComposeImages.Azurite)]
    public void Get_ComposeImage_ReturnsPinnedTag(string repository)
    {
        var tag = ComposeImages.Get(repository)[(repository.Length + 1)..];

        Assert.NotEmpty(tag);
        Assert.NotEqual("latest", tag);
    }
}
