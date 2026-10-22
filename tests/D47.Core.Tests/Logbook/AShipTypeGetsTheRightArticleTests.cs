using D47.Core.Logbook;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

public sealed class AShipTypeGetsTheRightArticleTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();

    [Theory]
    [InlineData("Anaconda Mk II", "an Anaconda Mk II")]
    [InlineData("Imperial Clipper", "an Imperial Clipper")]
    [InlineData("Python", "a Python")]
    [InlineData("python", "a python")]
    [InlineData(null, "a ship")]
    [InlineData("", "a ship")]
    public void TheArticleFollowsTheFirstLetter(string? name, string expected) =>
        Assert.Equal(expected, IndefiniteArticle.With(name));

    [Fact]
    public void AShipBoughtIsNamedWithItsArticle()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Event(
                Evening.AddMinutes(1),
                "ShipyardBuy",
                "\"ShipType\":\"empire_trader\",\"ShipType_Localised\":\"Imperial Clipper\",\"ShipPrice\":21000000"));

        var rendered = new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance)
            .Build(_corpus.Files, new LogRange
            {
                Span = LogSpan.Session,
                From = Evening.AddMinutes(-1),
                To = Evening.AddDays(1),
                Label = "the last session",
            })
            .Render();

        Assert.Contains("Bought an Imperial Clipper for", rendered, StringComparison.Ordinal);
    }
}
