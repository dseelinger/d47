using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Every stock story keeps the year format: a Save the Cat genre and a blurb on the card, and a hidden entry with
/// fifteen beats, fourteen clues, four finale lines, one to four options and a cast of local voices.
/// </summary>
public sealed class EveryStoryKeepsTheYearFormatGateTests
{
    private static readonly StoryCard Card = StoryFixtures.Card;

    private static readonly StorySecret Secret = StoryFixtures.Secret;

    private static readonly StorySpeaker Speaker = Secret.Cast[0];

    private static (string Named, StoryCard Card, StorySecret? Secret) Broken(string rule) => rule switch
    {
        "genre" => ("genre", Card with { Genre = "Western" }, Secret),
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
    public void AChatterboxSpeakerMayUseTheCommandersOwnVoice() =>
        Assert.Empty(new StoryCatalog(
            [Card],
            () => [Secret with { Cast = [Speaker with { Provider = StorySpeaker.Chatterbox, Voice = StorySpeaker.Own }] }]).Faults());

    [Theory]
    [InlineData("genre")]
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
