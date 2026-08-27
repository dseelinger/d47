using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A model's written refusal is not a reworded callout (GitHub issue 46).
/// <para>
/// Reported 2026-08-25. A Commander heard their carrier's tower say <em>"I don't have that
/// capability"</em> while they were departing. The authored line was <em>"No fire zone
/// exited"</em>, and the log held that — because <c>CalloutEngine</c> records what it wrote and
/// the model's rewrite of it was never recorded anywhere.
/// </para>
/// <para>
/// <b>The model had not refused.</b> <c>FlavourTurn</c> already rejects a protocol refusal and an
/// empty answer; this completed normally, and the content of the completion was a refusal, which
/// is a non-empty string and therefore won. The guardrails were working as written — <em>asked
/// for something you have no tool for, say so plainly</em> — and a cheap model read a rewrite
/// brief as a request. <c>llm.model</c> was <c>claude-haiku-4-5</c>.
/// </para>
/// </summary>
public class ARewriteThatTalksAboutItselfIsNotSpokenTests
{
    /// <summary>
    /// The line that was actually heard, and the shapes near it. Every one of these is the model
    /// answering a rewording brief by describing itself, which is never what a rewording brief
    /// asked for.
    /// </summary>
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
    /// <b>The refusal that was actually heard on 2026-08-26</b>, verbatim, and the shapes near it
    /// (<a href="https://github.com/dseelinger/d47/issues/88">#88</a>). The carrier captain was
    /// handed *"Commander inbound."* to put in its own words and refused on the guardrails' own
    /// authority instead.
    /// <para>
    /// <b>A different failure from the 2026-08-25 pair, not a new wording of it.</b> Those were
    /// the model <em>describing</em> its instructions. This is the model reading a rewording brief
    /// as an <em>attempt to extract</em> them — so the guardrails were working exactly as
    /// written, and the thing being refused was d47's own brief.
    /// </para>
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
    /// And the ordinary case is untouched, which is the half that matters more: a guard that
    /// rejected real lines would silently flatten every callout back to its authored wording and
    /// nothing would report it.
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

    /// <summary>
    /// Nothing at all is not a line either, which is what the call site used to test for on its
    /// own. Folded in here so there is one question rather than two, and one place to change.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingIsNotALine(string? line)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(line));
    }

    /// <summary>
    /// Case and surrounding sentences do not get a refusal past it. The phrases are matched
    /// inside the line because a refusal is a sentence rather than a token — a model that
    /// apologises first and refuses second is the common shape, not the exotic one.
    /// </summary>
    [Theory]
    [InlineData("Tower here. I DON'T HAVE THAT CAPABILITY.")]
    [InlineData("Safe flying, Commander. I don't have a tool for that, though.")]
    public void ARefusalWrappedInPolitenessIsStillARefusal(string line)
    {
        Assert.False(FlavourBriefs.MayBeSpoken(line));
    }
}
