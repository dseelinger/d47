using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Every stock story keeps the format of its length: a Save the Cat genre, a length and a blurb on the card, and a
/// hidden entry with the length's beats, clues and finale lines, one to four options and a cast of local voices.
/// </summary>
public sealed class EveryStoryKeepsTheFormatOfItsLengthGateTests
{
    private static readonly StoryCard Card = StoryFixtures.Card;

    private static readonly StorySecret Secret = StoryFixtures.Secret;

    private static readonly StoryCard WeekCard = Card with { Length = StoryPacing.OneWeek.Key };

    /// <summary>A hidden entry that fits a week: a scan line, two clues, two finale lines and the short sheet's seven beats.</summary>
    private static readonly StorySecret Week = Secret with
    {
        Scan = new("The beacon's light runs over the hull.", StorySpeaker.Narrator),
        Clues = [.. Secret.Clues.Take(2)],
        Finale = [.. Secret.Finale.Take(2)],
        Beats = new StoryBeats
        {
            OpeningImage = Secret.Beats.OpeningImage,
            Catalyst = Secret.Beats.Catalyst,
            BreakIntoTwo = Secret.Beats.BreakIntoTwo,
            Midpoint = Secret.Beats.Midpoint,
            AllIsLost = Secret.Beats.AllIsLost,
            Finale = Secret.Beats.Finale,
            FinalImage = Secret.Beats.FinalImage,
        },
    };

    private static readonly StorySpeaker Speaker = Secret.Cast[0];

    private static (string Named, StoryCard Card, StorySecret? Secret) Broken(string rule) => rule switch
    {
        "genre" => ("genre", Card with { Genre = "Western" }, Secret),
        "no-level" => ("the level", Card with { Level = "" }, Secret),
        "veteran-level" => ("the level", Card with { Level = "veteran" }, Secret),
        "no-length" => ("the length", Card with { Length = null }, Secret),
        "two-day-length" => ("the length", Card with { Length = "2-days" }, Secret),
        "week-with-fourteen-clues" => ("clues has 14 lines, not 2", WeekCard, Week with { Clues = Secret.Clues }),
        "week-with-a-b-story" => ("beats.bStory is not a beat", WeekCard, Week with { Beats = Week.Beats with { BStory = "A dock worker." } }),
        "week-without-a-scan" => ("scan is missing", WeekCard, Week with { Scan = null }),
        "month-with-a-scan" => ("scan is set", Card with { Length = StoryPacing.OneMonth.Key }, Week),
        "scan-by-a-stranger" => ("scan is spoken by stranger", WeekCard, Week with { Scan = Week.Scan! with { Speaker = "stranger" } }),
        "week-without-its-midpoint" => ("beats.midpoint is missing", WeekCard, Week with { Beats = Week.Beats with { Midpoint = null } }),
        "no-core" => ("the core is missing", Card with { Core = "" }, Secret),
        "unknown-core" => ("the core is missing", Card with { Core = "nobody" }, Secret),
        "stock-core" => ("the core is missing", Card with { Core = "covas" }, Secret),
        "heretic-core" => ("the core is missing", Card with { Core = "heretic" }, Secret),
        "blurb" => ("blurb", Card with { Blurb = " " }, Secret),
        "no-hidden-entry" => ("hidden entry", Card, null),
        "missing-beat" => ("beats.midpoint", Card, Secret with { Beats = Secret.Beats with { Midpoint = null } }),
        "thirteen-clues" => ("clues has 13", Card, Secret with { Clues = [.. Secret.Clues.Skip(1)] }),
        "five-finale-lines" => ("finale has 5", Card, Secret with { Finale = [.. Secret.Finale, Secret.Finale[0]] }),
        "no-options" => ("options has 0", Card, Secret with { Options = [] }),
        "five-options" => ("options has 5", Card, Secret with { Options = [.. Enumerable.Repeat(Secret.Options[0], 5)] }),
        "option-without-after" => ("needs an id, a label and an after", Card, Secret with { Options = [Secret.Options[0] with { After = "" }] }),
        "option-adds-a-stranger" => ("not a persona", Card, Secret with { Options = [Secret.Options[0] with { Add = ["nobody"] }] }),
        "hosted-provider" => ("the provider", Card, Secret with { Cast = [Speaker with { Provider = "elevenlabs" }] }),
        "own-voice-on-kokoro" => ("a voice that kokoro does not have", Card, Secret with { Cast = [Speaker with { Voice = StorySpeaker.Own }] }),
        "cast-named-ship" => ("taken", Card, Secret with { Cast = [Speaker with { Id = StorySpeaker.Ship }] }),
        "cast-named-narrator" => ("taken", Card, Secret with { Cast = [Speaker with { Id = StorySpeaker.Narrator }] }),
        "cast-named-for-a-persona" => ("taken", Card, Secret with { Cast = [Speaker with { Id = "warden" }] }),
        "line-without-speaker" => ("no speaker", Card, Secret with { Clues = [Secret.Clues[0] with { Speaker = "" }, .. Secret.Clues.Skip(1)] }),
        "line-by-a-stranger" => ("not the ship, the narrator or in the cast", Card, Secret with { Finale = [Secret.Finale[0] with { Speaker = "stranger" }, .. Secret.Finale.Skip(1)] }),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    [Fact]
    public void TheShippedCatalogKeepsTheFormat() =>
        Assert.Empty(StoryCatalog.Default.Faults());

    [Fact]
    public void TheFixtureStoryKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([Card], () => [Secret]).Faults());

    [Fact]
    public void AWeekLongStoryWithAWeeksLinesAndBeatsKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([WeekCard], () => [Week]).Faults());

    [Fact]
    public void EveryShippedCardIsAYear() =>
        Assert.All(StoryCatalog.Default.Cards, card => Assert.Equal(StoryPacing.OneYear.Key, card.Length));

    [Fact]
    public void AChatterboxSpeakerMayUseTheCommandersOwnVoice() =>
        Assert.Empty(new StoryCatalog(
            [Card],
            () => [Secret with { Cast = [Speaker with { Provider = StorySpeaker.Chatterbox, Voice = StorySpeaker.Own }] }]).Faults());

    [Theory]
    [InlineData("genre")]
    [InlineData("no-level")]
    [InlineData("veteran-level")]
    [InlineData("no-length")]
    [InlineData("two-day-length")]
    [InlineData("week-with-fourteen-clues")]
    [InlineData("week-with-a-b-story")]
    [InlineData("week-without-a-scan")]
    [InlineData("month-with-a-scan")]
    [InlineData("scan-by-a-stranger")]
    [InlineData("week-without-its-midpoint")]
    [InlineData("no-core")]
    [InlineData("unknown-core")]
    [InlineData("stock-core")]
    [InlineData("heretic-core")]
    [InlineData("blurb")]
    [InlineData("no-hidden-entry")]
    [InlineData("missing-beat")]
    [InlineData("thirteen-clues")]
    [InlineData("five-finale-lines")]
    [InlineData("no-options")]
    [InlineData("five-options")]
    [InlineData("option-without-after")]
    [InlineData("option-adds-a-stranger")]
    [InlineData("hosted-provider")]
    [InlineData("own-voice-on-kokoro")]
    [InlineData("cast-named-ship")]
    [InlineData("cast-named-narrator")]
    [InlineData("cast-named-for-a-persona")]
    [InlineData("line-without-speaker")]
    [InlineData("line-by-a-stranger")]
    public void ABrokenRuleIsAFault(string rule)
    {
        var (named, card, secret) = Broken(rule);
        var faults = new StoryCatalog([card], () => secret is null ? [] : [secret]).Faults();

        Assert.Contains(faults, fault => fault.Contains(named, StringComparison.Ordinal));
    }

    [Fact]
    public void AFaultQuotesNoHiddenText()
    {
        var broken = Secret with
        {
            Clues = [.. Secret.Clues.Select(line => line with { Speaker = "stranger" })],
            Options = [Secret.Options[0] with { After = "" }],
        };

        var faults = string.Join("\n", new StoryCatalog([Card], () => [broken]).Faults());

        Assert.All(broken.Texts().Where(text => text.Text.Length > 0), text => Assert.DoesNotContain(text.Text, faults, StringComparison.Ordinal));
    }
}
