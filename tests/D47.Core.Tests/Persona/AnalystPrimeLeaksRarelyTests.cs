using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// Analyst Prime's block used to fight its own rule, and lose in a way a Commander could hear
/// (#81). Reported from watching one fly with him: <em>"Every other message had something about
/// Cora not approving, or doing something differently."</em>
/// <para>
/// <b>These are a source gate, not a proof.</b> A prompt defect is not provable by reading the
/// prompt — the old text already contained the rule it was breaking, which is exactly why nothing
/// caught it. What is provable here is that the four causes stay fixed: the word that instructed
/// it every time is gone, the budget is stated as rarity rather than as a maximum, and Cora no
/// longer dominates the block by volume. Whether the model actually leaks less is a run, and it
/// belongs to the manual pass.
/// </para>
/// </summary>
public class AnalystPrimeLeaksRarelyTests
{
    private static Core.Persona.Persona Prime =>
        PersonaCatalog.Shipped.Single(core => core.Id == "analyst-prime");

    /// <summary>
    /// <b>The single largest cause.</b> "Consistently." was a one-word sentence — emphasis — telling
    /// him to make the comparison every time, and it sat thirty words above a budget that said at
    /// most once. Faced with a concrete emphatic instruction and an abstract limit, a model follows
    /// the instruction.
    /// </summary>
    [Fact]
    public void NothingTellsHimToDoItEveryTime()
    {
        // The word itself, in either case. Deliberately not a search for phrases like "every
        // time": the block now *prohibits* a comparison made every time, and a gate that could
        // not tell an instruction from its prohibition would fire on the fix.
        Assert.DoesNotContain("onsistently", Prime.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The frequency rule is stated as rarity and as its own hard rule, rather than as a coda to
    /// the paragraph it is meant to limit.
    /// </summary>
    [Fact]
    public void TheFrequencyRuleIsStatedAsRarity()
    {
        Assert.Contains("how often you may mention Cora: rarely", Prime.Body, StringComparison.Ordinal);
        Assert.Contains("no exchange mentions her twice", Prime.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Attention follows volume.</b> Cora was in the ship line, the whole "on the dead"
    /// paragraph, the entire hard-rule paragraph and the intro, while the reading that is supposed
    /// to dominate — <em>optimization is demonstration</em> — got two lines. A model reading that
    /// would reasonably conclude the subject of this character is Cora, because by wordcount it
    /// was.
    /// <para>
    /// Four is a ceiling with room in it rather than a target; the point is that she cannot quietly
    /// become the bulk of the block again.
    /// </para>
    /// </summary>
    [Fact]
    public void SheIsNotMostOfTheBlockByVolume()
    {
        var mentions = Prime.Body.Split("Cora").Length - 1;

        Assert.InRange(mentions, 1, 4);
    }

    /// <summary>
    /// <b>The intro is his only worked example, and an example beats a rule.</b> It was roughly
    /// forty per cent Cora — she was named and referred to five times — so a first message half
    /// about her primed every continuation toward her, even with the budget fixed. It demonstrates
    /// the condescension now and leaks her once.
    /// </summary>
    [Fact]
    public void TheIntroLeaksHerOnceAtMost()
    {
        var named = Prime.Intro.Split("Cora").Length - 1;
        var pronouns = Prime.Intro.Split(" she ", StringSplitOptions.None).Length - 1
                       + Prime.Intro.Split("She ", StringSplitOptions.None).Length - 1;

        Assert.True(named + pronouns <= 3, $"The intro refers to her {named + pronouns} times: {Prime.Intro}");
    }

    /// <summary>
    /// And she stays. This is a frequency defect, not a case for removing her — the rarest beat in
    /// the character is the one where he cannot tell a memory of her from a reconstruction, and it
    /// is the best thing he has.
    /// </summary>
    [Fact]
    public void SheIsStillThere()
    {
        Assert.Contains("Cora", Prime.Body, StringComparison.Ordinal);
        Assert.Contains("a memory or a reconstruction", Prime.Body, StringComparison.Ordinal);
    }
}
