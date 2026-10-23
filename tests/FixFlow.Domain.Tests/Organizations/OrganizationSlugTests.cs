using System.Text.RegularExpressions;
using FixFlow.Domain.Organizations;

namespace FixFlow.Domain.Tests.Organizations;

public partial class OrganizationSlugTests
{
    public static TheoryData<string> ReservedWords => [.. OrganizationSlug.Reserved];

    [Theory]
    [InlineData("abc")]
    [InlineData("acme")]
    [InlineData("acme-2")]
    [InlineData("9lives")]
    [InlineData("a-b-c")]
    public void IsValid_WellFormedSlug_IsValid(string slug)
    {
        Assert.True(OrganizationSlug.IsValid(slug, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(OrganizationSlug.MinLength)]
    [InlineData(OrganizationSlug.MaxLength)]
    public void IsValid_LengthAtLimit_IsValid(int length) =>
        Assert.True(OrganizationSlug.IsValid(new string('a', length), out _));

    [Theory]
    [InlineData(0)]
    [InlineData(OrganizationSlug.MinLength - 1)]
    [InlineData(OrganizationSlug.MaxLength + 1)]
    public void IsValid_LengthOutsideLimits_ExplainsLength(int length)
    {
        Assert.False(OrganizationSlug.IsValid(new string('a', length), out var error));
        Assert.Equal("A slug must be 3 to 40 characters long.", error);
    }

    [Theory]
    [InlineData("Acme")]
    [InlineData("ACME")]
    [InlineData("acme.io")]
    [InlineData("acme_co")]
    [InlineData("ac me")]
    [InlineData(" acme")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("ac--me")]
    [InlineData("acmé")]
    [InlineData("acme\n")]
    [InlineData("аcme")] // Cyrillic а
    public void IsValid_MalformedSlug_ExplainsFormat(string slug)
    {
        Assert.False(OrganizationSlug.IsValid(slug, out var error));
        Assert.Equal(
            "A slug may contain only lowercase letters, digits and single hyphens, and must start and end with a letter or digit.",
            error);
    }

    // Length and format are checked first, so this also shows that each reserved word would otherwise be a valid slug
    [Theory]
    [MemberData(nameof(ReservedWords))]
    public void IsValid_ReservedWord_ExplainsReserved(string slug)
    {
        Assert.False(OrganizationSlug.IsValid(slug, out var error));
        Assert.Equal("This slug is reserved.", error);
    }

    [Fact]
    public void IsValid_Null_Throws() => Assert.Throws<ArgumentNullException>(() => OrganizationSlug.IsValid(null!, out _));

    [Fact]
    public void Reserved_IncludesEverySpaGlobalPath()
    {
        var paths = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Spa", "paths.ts"));
        var globalPaths = GlobalPathsObject().Match(paths);
        Assert.True(globalPaths.Success, "paths.ts no longer declares `export const globalPaths = { ... }`");

        var segments = FirstSegment().Matches(globalPaths.Groups["body"].Value)
            .Select(match => match.Groups["segment"].Value)
            .ToList();

        Assert.Contains("login", segments);
        Assert.All(segments, segment => Assert.Contains(segment, OrganizationSlug.Reserved));
    }

    [GeneratedRegex(@"export const globalPaths = \{(?<body>[^}]*)\}")]
    private static partial Regex GlobalPathsObject();

    // The first segment of each path literal ('/login', '/organizations/new')
    [GeneratedRegex(@"'/(?<segment>[^'/]+)")]
    private static partial Regex FirstSegment();
}
