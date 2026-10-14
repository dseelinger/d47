using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.AStrangerIsIntroducedBeforeTheyFirstSpeakTests;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>An introduction names the version of a cast member that this Commander meets.</summary>
public sealed class AnIntroductionUsesTheVersionTheCommanderMeetsTests
{
    private static readonly StorySecret WithRole = Versioned with
    {
        Cast = [.. Versioned.Cast.Select(member => member.Id == "cray" ? member with { Role = "the engineer who rebuilt the Commander's ship" } : member)],
    };

    [Theory]
    [InlineData(CommanderGender.Man, "Ellie is the engineer who rebuilt the Commander's ship.")]
    [InlineData(CommanderGender.Woman, "Ellis is the engineer who rebuilt the Commander's ship.")]
    [InlineData(null, "Ellie is the engineer who rebuilt the Commander's ship.")]
    public void TheNameIsTheVersionTheCommanderMeets(string? gender, string expected)
    {
        Assert.Equal(expected, StoryIntroductions.Line(Running(), WithRole, gender, "cray"));
    }
}
