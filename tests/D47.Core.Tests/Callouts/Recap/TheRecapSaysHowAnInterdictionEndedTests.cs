using D47.Core.Callouts;
using D47.Core.Logbook;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

public class TheRecapSaysHowAnInterdictionEndedTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();

    private string Recap(string submitted, string ending)
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Event(
                Evening.AddMinutes(1),
                "Interdicted",
                $"\"Interdictor\":\"Dax Ferron\",\"IsPlayer\":false,\"Submitted\":{submitted}"),
            ending switch
            {
                "kill" => JournalCorpus.Event(
                    Evening.AddMinutes(2),
                    "Bounty",
                    "\"PilotName_Localised\":\"Dax Ferron\",\"Target\":\"viper_mkiv\",\"TotalReward\":5000"),
                "death" => JournalCorpus.Event(Evening.AddMinutes(2), "Died", "\"KillerName\":\"Dax Ferron\""),
                _ => JournalCorpus.Event(Evening.AddMinutes(2), "SupercruiseEntry", "\"StarSystem\":\"Deciat\""),
            });

        var range = new LogRange
        {
            Span = LogSpan.Session,
            From = Evening.AddMinutes(-1),
            To = Evening.AddDays(1),
            Label = "the last session",
        };

        var digest = new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance).Build(_corpus.Files, range);

        return RecapCallout.Compose(digest)!;
    }

    [Theory]
    [InlineData("true", "kill", "you were interdicted by Dax Ferron, and submitted, then destroyed their Viper Mk IV")]
    [InlineData("true", "death", "you were interdicted by Dax Ferron, and submitted, then died")]
    [InlineData("true", "away", "you were interdicted by Dax Ferron, and submitted, then got away")]
    [InlineData("false", "kill", "you were interdicted by Dax Ferron, and ran for it, then destroyed their Viper Mk IV")]
    [InlineData("false", "death", "you were interdicted by Dax Ferron, and ran for it, then died")]
    [InlineData("false", "away", "you were interdicted by Dax Ferron, and ran for it, then got away")]
    public void TheEndingContinuesTheSentenceTheRecapSays(string submitted, string ending, string expected)
    {
        var line = Recap(submitted, ending);

        Assert.Contains(expected, line, StringComparison.Ordinal);
        Assert.EndsWith(".", line, StringComparison.Ordinal);
    }
}
