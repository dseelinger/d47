using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Every stock story keeps the format of its length: a Save the Cat genre, a length and a blurb on the card, and a
/// hidden entry with the length's beats, clues and finale lines, one to four options and a cast of local voices.
/// </summary>
[Trait("Category", "Integration")]
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

    /// <summary>An opening of a call from the cast and a Narrator line naming the Commander.</summary>
    private static readonly IReadOnlyList<StoryLine> Opening =
    [
        new("Mayday, mayday.", Speaker.Id),
        new("{commander} heard {their} own voice, which {they} knew.", StorySpeaker.Narrator),
    ];

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
        "unknown-provider" => ("the provider", Card, Secret with { Cast = [Speaker with { Provider = "nobody" }] }),
        "silent-provider" => ("the provider", Card, Secret with { Cast = [Speaker with { Provider = "none" }] }),
        "billed-provider" => ("which is billed", Card, Secret with { Cast = [Speaker with { Provider = "openai", Voice = "onyx" }] }),
        "hosted-provider-without-a-voice" => ("a voice that edge does not have", Card, Secret with { Cast = [Speaker with { Provider = "edge", Voice = " " }] }),
        "own-voice-on-kokoro" => ("a voice that kokoro does not have", Card, Secret with { Cast = [Speaker with { Voice = StorySpeaker.Own }] }),
        "cast-named-ship" => ("taken", Card, Secret with { Cast = [Speaker with { Id = StorySpeaker.Ship }] }),
        "cast-named-narrator" => ("taken", Card, Secret with { Cast = [Speaker with { Id = StorySpeaker.Narrator }] }),
        "cast-named-for-a-persona" => ("taken", Card, Secret with { Cast = [Speaker with { Id = "warden" }] }),
        "link-above-one" => ("cast[0]", Card, Secret with { Cast = [Speaker with { Link = 1.5 }] }),
        "link-below-zero" => ("cast[0]", Card, Secret with { Cast = [Speaker with { Link = -0.1 }] }),
        "unknown-effect" => ("cast[0]", Card, Secret with { Cast = [Speaker with { Effects = [new("nothing", 5)] }] }),
        "effect-level-zero" => ("cast[0]", Card, Secret with { Cast = [Speaker with { Effects = [new("glitch", 0)] }] }),
        "effect-level-twenty-one" => ("cast[0]", Card, Secret with { Cast = [Speaker with { Effects = [new("glitch", 21)] }] }),
        "effect-listed-twice" => ("twice", Card, Secret with { Cast = [Speaker with { Effects = [new("glitch", 5), new("glitch", 9)] }] }),
        "week-boarding-at-set-up" => ("cast[0].aboard.boards is setUp, which is not a beat of a 1 week story", WeekCard, Week with { Cast = [Speaker with { Aboard = [new("setUp", null)] }] }),
        "leaving-at-no-beat" => ("cast[0].aboard.leaves is climax", Card, Secret with { Cast = [Speaker with { Aboard = [new(null, "climax")] }] }),
        "leaving-before-boarding" => ("cast[0].aboard leaves at or before", Card, Secret with { Cast = [Speaker with { Aboard = [new("midpoint", "catalyst")] }] }),
        "leaving-where-it-boards" => ("cast[0].aboard leaves at or before", Card, Secret with { Cast = [Speaker with { Aboard = [new("midpoint", "midpoint")] }] }),
        "no-spans" => ("cast[0].aboard is not one span or a list of spans", Card, Secret with { Cast = [Speaker with { Aboard = [] }] }),
        "second-span-boarding-at-the-start" => ("cast[0].aboard[1].boards is null", Card, Secret with { Cast = [Speaker with { Aboard = [new("setUp", "catalyst"), new(null, null)] }] }),
        "first-span-leaving-at-the-end" => ("cast[0].aboard[0].leaves is null", Card, Secret with { Cast = [Speaker with { Aboard = [new("setUp", null), new("midpoint", null)] }] }),
        "span-boarding-where-the-last-leaves" => ("cast[0].aboard[1] boards at or before the beat the span before it leaves at", Card, Secret with { Cast = [Speaker with { Aboard = [new("setUp", "midpoint"), new("midpoint", null)] }] }),
        "span-boarding-before-the-last-leaves" => ("cast[0].aboard[1] boards at or before the beat the span before it leaves at", Card, Secret with { Cast = [Speaker with { Aboard = [new("setUp", "midpoint"), new("catalyst", null)] }] }),
        "second-span-leaving-at-no-beat" => ("cast[0].aboard[1].leaves is climax", Card, Secret with { Cast = [Speaker with { Aboard = [new("setUp", "catalyst"), new("midpoint", "climax")] }] }),
        "line-without-speaker" => ("no speaker", Card, Secret with { Clues = [Secret.Clues[0] with { Speaker = "" }, .. Secret.Clues.Skip(1)] }),
        "line-by-a-stranger" => ("not the ship, the narrator or in the cast", Card, Secret with { Finale = [Secret.Finale[0] with { Speaker = "stranger" }, .. Secret.Finale.Skip(1)] }),
        "opening-by-a-stranger" => ("opening[0] is spoken by stranger", Card, Secret with { Opening = [Opening[0] with { Speaker = "stranger" }] }),
        "empty-opening" => ("opening is empty", Card, Secret with { Opening = [] }),
        "opening-without-text" => ("opening[1] has no text", Card, Secret with { Opening = [Opening[0], Opening[1] with { Text = " " }] }),
        "opening-with-another-token" => ("opening[0] has a token", Card, Secret with { Opening = [Opening[0] with { Text = "Mayday on {date}." }] }),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    [Fact]
    public void TheFixtureStoryKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([Card], () => [Secret]).Faults());

    [Fact]
    public void AWeekLongStoryWithAWeeksLinesAndBeatsKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([WeekCard], () => [Week]).Faults());

    [Fact]
    public void AStoryOfAnyLengthMayOpenWithFixedLines()
    {
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Opening = Opening }]).Faults());
        Assert.Empty(new StoryCatalog([WeekCard], () => [Week with { Opening = Opening }]).Faults());
    }

    [Fact]
    public void EveryCardNamesALength() =>
        Assert.All(StoryFixtures.Catalog.Cards, card => Assert.NotNull(StoryPacing.Find(card.Length)));

    [Fact]
    public void ASpeakerWithALinkAndEffectsKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Link = 0.05, Effects = [new("glitch", 20)] }] }]).Faults());

    [Fact]
    public void ASpeakerAboardBetweenTwoOfItsLengthsBeatsKeepsTheFormat()
    {
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Aboard = [new("midpoint", "allIsLost")] }] }]).Faults());
        Assert.Empty(new StoryCatalog([WeekCard], () => [Week with { Cast = [Speaker with { Aboard = [new("breakIntoTwo", null)] }] }]).Faults());
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Aboard = [new(null, null)] }] }]).Faults());
    }

    [Fact]
    public void ASpeakerAboardInSpansInStoryOrderKeepsTheFormat()
    {
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Aboard = [new(null, "catalyst"), new("midpoint", "allIsLost"), new("finale", null)] }] }]).Faults());
        Assert.Empty(new StoryCatalog([WeekCard], () => [Week with { Cast = [Speaker with { Aboard = [new("catalyst", "breakIntoTwo"), new("midpoint", "allIsLost")] }] }]).Faults());
    }

    [Fact]
    public void APinnedSpeakerKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Pinned = true }] }]).Faults());

    [Fact]
    public void PinnedOnAVersionIsRefused()
    {
        var pinned = System.Text.Json.JsonDocument.Parse("true").RootElement;
        var cray = StoryFixtures.Versioned.Cast.Single(member => member.Versions is not null);
        var version = cray.Versions!.ForMan! with { Extra = new() { ["pinned"] = pinned } };
        var secret = StoryFixtures.Versioned with
        {
            Cast = [.. StoryFixtures.Versioned.Cast.Select(member => member == cray
                ? cray with { Versions = new StorySpeakerVersions { ForMan = version, ForWoman = cray.Versions!.ForWoman } }
                : member)],
        };

        using var packed = new MemoryStream();

        using (var deflate = new System.IO.Compression.DeflateStream(packed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            System.Text.Json.JsonSerializer.Serialize(deflate, new[] { secret }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        }

        var loaded = StoryCatalog.Unseal(Convert.ToBase64String(packed.ToArray()));

        Assert.Contains(new StoryCatalog([Card], () => loaded).Faults(), fault => fault.Contains("has pinned; put it on the member", StringComparison.Ordinal));
    }

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
    [InlineData("unknown-provider")]
    [InlineData("silent-provider")]
    [InlineData("billed-provider")]
    [InlineData("hosted-provider-without-a-voice")]
    [InlineData("own-voice-on-kokoro")]
    [InlineData("cast-named-ship")]
    [InlineData("cast-named-narrator")]
    [InlineData("cast-named-for-a-persona")]
    [InlineData("link-above-one")]
    [InlineData("link-below-zero")]
    [InlineData("unknown-effect")]
    [InlineData("effect-level-zero")]
    [InlineData("effect-level-twenty-one")]
    [InlineData("effect-listed-twice")]
    [InlineData("week-boarding-at-set-up")]
    [InlineData("leaving-at-no-beat")]
    [InlineData("leaving-before-boarding")]
    [InlineData("leaving-where-it-boards")]
    [InlineData("no-spans")]
    [InlineData("second-span-boarding-at-the-start")]
    [InlineData("first-span-leaving-at-the-end")]
    [InlineData("span-boarding-where-the-last-leaves")]
    [InlineData("span-boarding-before-the-last-leaves")]
    [InlineData("second-span-leaving-at-no-beat")]
    [InlineData("line-without-speaker")]
    [InlineData("line-by-a-stranger")]
    [InlineData("opening-by-a-stranger")]
    [InlineData("empty-opening")]
    [InlineData("opening-without-text")]
    [InlineData("opening-with-another-token")]
    public void ABrokenRuleIsAFault(string rule)
    {
        var (named, card, secret) = Broken(rule);
        var faults = new StoryCatalog([card], () => secret is null ? [] : [secret]).Faults();

        Assert.Contains(faults, fault => fault.Contains(named, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryCastMemberOfAPublishedStoryHasARole() =>
        Assert.Empty(new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Role = "the dock hand at the Commander's first station" }] }]).RoleFaults());

    [Fact]
    public void AMemberWithNoRoleCannotBePublishedAndStillLoads()
    {
        Assert.Contains(new StoryCatalog([Card], () => [Secret]).RoleFaults(), fault => fault.Contains("cast[0] has no role", StringComparison.Ordinal));
        Assert.Empty(StoryCatalog.Faults(Card, Secret));
    }

    [Theory]
    [InlineData("The dock hand", "starts with a capital letter")]
    [InlineData("the dock hand.", "ends with a full stop")]
    [InlineData("too-long", "more than 160")]
    [InlineData("the dock hand called Ren", "holds a name")]
    [InlineData("the dock hand who knows {name:cray}", "holds a name")]
    public void ARoleThatBreaksTheFormatIsAFault(string role, string named)
    {
        var given = role == "too-long" ? "the " + new string('x', StorySpeaker.MostRoleLength - 3) : role;

        Assert.Contains(
            new StoryCatalog([Card], () => [Secret with { Cast = [Speaker with { Role = given }] }]).RoleFaults(),
            fault => fault.Contains(named, StringComparison.Ordinal));
    }

    [Fact]
    public void ARoleNamingEitherVersionIsAFault()
    {
        var cray = StoryFixtures.Versioned.Cast.Single(member => member.Versions is not null);

        foreach (var version in cray.Versions!.All())
        {
            var secret = StoryFixtures.Versioned with
            {
                Cast = [.. StoryFixtures.Versioned.Cast.Select(member => member with { Role = member == cray ? $"the engineer {version.Version.Name}" : "the dock hand" })],
            };

            Assert.Contains(new StoryCatalog([Card], () => [secret]).RoleFaults(), fault => fault.Contains("holds a name", StringComparison.Ordinal));
        }
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
