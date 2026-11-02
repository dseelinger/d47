using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>{parent} and {parent:cast-id} resolve wherever {name:cast-id} does, by the Commander's gender.</summary>
public sealed class AParentTokenIsTheWordForTheCommanderTests
{
    private static StorySecret Parented => Versioned with
    {
        Parent = new StoryParent { Man = "Dad", Woman = "Mum" },
        End = "{parent} stays. {parent:cray} waves.",
        Beats = Versioned.Beats with { Midpoint = "{parent:cray} remembers." },
        Clues = [new("Hello, {parent}.", "cray"), .. Versioned.Clues.Skip(1)],
        Finale = [Versioned.Finale[0] with { Text = "Come home, {parent:cray}." }, .. Versioned.Finale.Skip(1)],
        Options = [Versioned.Options[0] with { Label = "Keep {parent}", After = "{parent} stays aboard." }],
        Cast = [.. Versioned.Cast.Select(speaker => speaker.Id != "cray" ? speaker : speaker with
        {
            Versions = new StorySpeakerVersions
            {
                ForMan = speaker.Versions!.ForMan! with { Parent = "Pop" },
                ForWoman = speaker.Versions.ForWoman! with { Parent = "Mom" },
            },
        })],
    };

    [Theory]
    [InlineData(CommanderGender.Man, "Dad", "Pop")]
    [InlineData(CommanderGender.Woman, "Mum", "Mom")]
    public void EveryTextTakesTheWordForTheGender(string gender, string parent, string member)
    {
        var resolved = Parented.For(gender);

        Assert.All(resolved.Texts(), text => Assert.DoesNotContain("{parent", text.Text));
        Assert.Equal($"{parent} stays. {member} waves.", resolved.End);
        Assert.Equal($"Hello, {parent}.", resolved.Clues[0].Text);
        Assert.Equal($"Come home, {member}.", resolved.Finale[0].Text);
        Assert.Equal($"Keep {parent}", resolved.Options[0].Label);
    }

    [Fact]
    public void AnUnsetGenderTakesTheManWord()
    {
        Assert.Equal("Hello, Dad.", Parented.For(null).Clues[0].Text);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AStoryWithAParentMapNeedsTheGender()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(), Secret with { Parent = new StoryParent { Man = "Dad", Woman = "Mum" } });

        Assert.True(fixtures.Director.NeedsGenderFor(Id));
    }

    [Fact]
    public void BothTokensAreKnownInAnOpeningLine()
    {
        Assert.Empty(StorySecret.UnknownTokens("{parent} and {parent:cray}"));
        Assert.Equal(["{parent:}"], StorySecret.UnknownTokens("{parent:}"));
    }

    [Theory]
    [InlineData("map-missing", "end has the token {parent}")]
    [InlineData("map-without-woman", "end has the token {parent}")]
    [InlineData("member-without-versions", "end has the token {parent:dock-hand}, and dock-hand has no versions")]
    [InlineData("version-without-parent", "cast.cray.for-woman has no parent")]
    public void AHalfMadeParentIsRefused(string rule, string named)
    {
        var secret = rule switch
        {
            "map-missing" => Parented with { Parent = null },
            "map-without-woman" => Parented with { Parent = new StoryParent { Man = "Dad" } },
            "member-without-versions" => Parented with { End = "{parent:dock-hand} decides." },
            _ => Parented with
            {
                Cast = [.. Parented.Cast.Select(speaker => speaker.Id != "cray" ? speaker : speaker with
                {
                    Versions = speaker.Versions! with { ForWoman = speaker.Versions.ForWoman! with { Parent = null } },
                })],
            },
        };

        var faults = new StoryCatalog([Card], () => [secret]).Faults();

        Assert.Contains(faults, fault => fault.Contains(named, StringComparison.Ordinal));
    }
}
