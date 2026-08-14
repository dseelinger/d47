using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The local filter vocabulary, which exists because the search service ignores keys it does not
/// recognise rather than rejecting them (<see cref="GalaxyFilters"/>).
/// </summary>
public class GalaxyQueryTests
{
    private static bool Parse(
        IReadOnlyDictionary<string, string> requested,
        out GalaxyQuery query,
        out string failure) =>
        GalaxyQuery.TryParse("Sol", requested, size: 5, out query, out failure);

    [Fact]
    public void AFilterTheServiceDoesNotHaveIsRefusedRatherThanPassedThrough()
    {
        // The whole point. Passed through, this returns 200 and an unfiltered result that reads
        // like a filtered one.
        var ok = Parse(new Dictionary<string, string> { ["not_a_real_filter"] = "Federation" }, out _, out var failure);

        Assert.False(ok);
        Assert.Contains("no 'not_a_real_filter' filter", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AMisspelledRealFilterIsRefusedToo()
    {
        // Measured against the live service: 'allegience' returned the same 108 systems as no
        // filter at all, while 'allegiance' returned 60. Nothing downstream can tell those apart.
        var ok = Parse(new Dictionary<string, string> { ["allegience"] = "Federation" }, out _, out var failure);

        Assert.False(ok);
        Assert.Contains("allegience", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRefusalNamesWhatIsActuallyAvailable()
    {
        // A tool error saying only "invalid filter" invites the model to guess again out of the
        // same vocabulary it just invented.
        Parse(new Dictionary<string, string> { ["nonsense"] = "x" }, out _, out var failure);

        Assert.Contains("allegiance", failure, StringComparison.Ordinal);
        Assert.Contains("primary_economy", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueOutsideAClosedVocabularyIsRefused()
    {
        var ok = Parse(new Dictionary<string, string> { ["allegiance"] = "Klingon" }, out _, out var failure);

        Assert.False(ok);
        Assert.Contains("Klingon", failure, StringComparison.Ordinal);
        Assert.Contains("Federation", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AChoiceIsCanonicalisedToTheServicesOwnCasing()
    {
        // Forgiving on the way in because the model is speaking; exact on the way out because
        // the service is not.
        Assert.True(Parse(new Dictionary<string, string> { ["allegiance"] = "federation" }, out var query, out _));

        var criterion = Assert.Single(query.Criteria);
        Assert.Equal("Federation", Assert.Single(criterion.Choices));
    }

    [Fact]
    public void SeveralChoicesCanBeGivenAtOnce()
    {
        Assert.True(Parse(
            new Dictionary<string, string> { ["allegiance"] = "Federation, Empire" },
            out var query,
            out _));

        Assert.Equal(["Federation", "Empire"], Assert.Single(query.Criteria).Choices);
    }

    [Theory]
    [InlineData("20", null, 20d)]
    [InlineData("10-50", 10d, 50d)]
    [InlineData("1000000-", 1000000d, null)]
    [InlineData("-30", null, 30d)]
    public void ARangeReadsInAllFourShapes(string given, double? min, double? max)
    {
        Assert.True(Parse(new Dictionary<string, string> { ["distance"] = given }, out var query, out _));

        var criterion = Assert.Single(query.Criteria);
        Assert.Equal(min, criterion.Min);
        Assert.Equal(max, criterion.Max);
    }

    [Fact]
    public void ABareNumberIsAnUpperBound()
    {
        // "Systems within 20 light years" is the question people ask. "Systems exactly 20 light
        // years away" is not.
        Assert.True(Parse(new Dictionary<string, string> { ["distance"] = "20" }, out var query, out _));

        var criterion = Assert.Single(query.Criteria);
        Assert.Null(criterion.Min);
        Assert.Equal(20d, criterion.Max);
    }

    [Fact]
    public void ARangeThatIsNotANumberIsRefused()
    {
        var ok = Parse(new Dictionary<string, string> { ["distance"] = "quite near" }, out _, out var failure);

        Assert.False(ok);
        Assert.Contains("quite near", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void TheResultCountIsClampedRatherThanRefused()
    {
        // A model asking for fifty is not making an error worth an error message; it is asking
        // for more than can be read aloud in a cockpit.
        GalaxyQuery.TryParse("Sol", new Dictionary<string, string>(), size: 500, out var big, out _);
        GalaxyQuery.TryParse("Sol", new Dictionary<string, string>(), size: 0, out var none, out _);

        Assert.Equal(20, big.Size);
        Assert.Equal(5, none.Size);
    }
}
