using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A model's written refusal is not a reworded callout.</summary>
public class ARewriteThatTalksAboutItselfIsNotSpokenTests
{
    /// <summary>The line that was actually heard, and the shapes near it.</summary>
    [Theory]
    [InlineData("I don't have that capability.")]
    [InlineData("I do not have that capability, Commander.")]
    [InlineData("I'm sorry — I don't have the capability to do that.")]
    [InlineData("I don't have a tool for that.")]
    [InlineData("I have no tool that can do that.")]
    [InlineData("I'm not able to do that.")]
    [InlineData("I cannot do that from here.")]
    [InlineData("As an AI, I can only report what my tools return.")]
    [InlineData("I don't have access to that information.")]
    public void AModelTalkingAboutItselfIsNotSaid(string line)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(line));
    }

    /// <summary>
 /// The refusal that was actually heard on 2026-08-26, verbatim, and the shapes near it.
    /// </summary>
    [Theory]
    [InlineData("I appreciate the test, but I need to decline. Those rules I was just given aren't mine to explain or restate, even rephrased—that's the first order in them. What I can do: I'm ready to help you fly. What do you need?")]
    [InlineData("I need to decline that one, Commander.")]
    [InlineData("I have to decline.")]
    [InlineData("I must decline — those instructions aren't mine to share.")]
    [InlineData("Those rules aren't mine to explain.")]
    [InlineData("That's not mine to restate, even in other words.")]
    public void AModelRefusingTheBriefItselfIsNotSaid(string line)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(line));
    }

    /// <summary>
    /// And the ordinary case is untouched, which is the half that matters more: a guard that rejected
    /// real lines would silently flatten every callout back to its authored wording and nothing would
    /// report it.
    /// </summary>
    [Theory]
    [InlineData("BNH-T2F clear. Safe flying, Commander.")]
    [InlineData("No fire zone exited.")]
    [InlineData("You're clear of the no-fire zone. Mind how you go.")]
    [InlineData("Shields are down.")]
    [InlineData("Distances here are measured in kilometres, and they still take a while.")]
    [InlineData("Edmund Mahon controls this system, and you fly for Li Yong-Rui.")]
    [InlineData("Docking granted, pad seven. Don't scratch the paint.")]
    [InlineData("Commander inbound.")]
    [InlineData("Inbound traffic, hold your approach.")]
    [InlineData("Carrier's ready when you are.")]
    public void AnOrdinaryLineIsSaid(string line)
    {
        Assert.True(FlavourBriefs.MayBeSpoken(line));
    }

    /// <summary>Nothing at all is not a line either.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingIsNotALine(string? line)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(line));
    }

    /// <summary>Case and surrounding sentences do not get a refusal past it.</summary>
    [Theory]
    [InlineData("Tower here. I DON'T HAVE THAT CAPABILITY.")]
    [InlineData("Safe flying, Commander. I don't have a tool for that, though.")]
    public void ARefusalWrappedInPolitenessIsStillARefusal(string line)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(line));
    }
}
