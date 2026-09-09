using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Analyst Prime's block must not fight its own rule.</summary>
public class AnalystPrimeLeaksRarelyTests
{
    private static Core.Persona.Persona Prime =>
        PersonaCatalog.Shipped.Single(core => core.Id == "analyst-prime");

    /// <summary>
    /// The single largest cause. "Consistently." was a one-word sentence — emphasis — telling him to
    /// make the comparison every time, and it sat thirty words above a budget that said at most once.
    /// </summary>
    [Fact]
    public void NothingTellsHimToDoItEveryTime()
    {
        // The word itself, in either case.
        Assert.DoesNotContain("onsistently", Prime.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The frequency rule is stated as rarity and as its own hard rule, rather than as a coda to the
    /// paragraph it is meant to limit.
    /// </summary>
    [Fact]
    public void TheFrequencyRuleIsStatedAsRarity()
    {
        Assert.Contains("how often you may mention Cora: rarely", Prime.Body, StringComparison.Ordinal);
        Assert.Contains("no exchange mentions her twice", Prime.Body, StringComparison.Ordinal);
    }

    /// <summary>Attention follows volume.</summary>
    [Fact]
    public void SheIsNotMostOfTheBlockByVolume()
    {
        var mentions = Prime.Body.Split("Cora").Length - 1;

        Assert.InRange(mentions, 1, 4);
    }

    /// <summary>The intro is his only worked example, and an example beats a rule.</summary>
    [Fact]
    public void TheIntroLeaksHerOnceAtMost()
    {
        var named = Prime.Intro.Split("Cora").Length - 1;
        var pronouns = Prime.Intro.Split(" she ", StringSplitOptions.None).Length - 1
                       + Prime.Intro.Split("She ", StringSplitOptions.None).Length - 1;

        Assert.True(named + pronouns <= 3, $"The intro refers to her {named + pronouns} times: {Prime.Intro}");
    }

    /// <summary>And she stays.</summary>
    [Fact]
    public void SheIsStillThere()
    {
        Assert.Contains("Cora", Prime.Body, StringComparison.Ordinal);
        Assert.Contains("a memory or a reconstruction", Prime.Body, StringComparison.Ordinal);
    }
}
