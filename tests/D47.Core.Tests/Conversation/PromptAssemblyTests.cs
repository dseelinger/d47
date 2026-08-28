using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The volatility ordering is the whole reason prompt caching works, and the guardrails sitting
/// above the persona is the whole reason "Personality on/off" is safe. Both are asserted here
/// rather than trusted to whoever edits the assembly next.
/// </summary>
public class PromptAssemblyTests
{
    [Fact]
    public void GuardrailsComeFirstInTheCachedBlock()
    {
        var block = new PromptAssembly { Persona = "You are Warden." }.RenderCachedSystemBlock();

        Assert.StartsWith(Guardrails.Text, block, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardrailsAppearAboveThePersona()
    {
        var block = new PromptAssembly { Persona = "You are Warden." }.RenderCachedSystemBlock();

        Assert.True(
            block.IndexOf(Guardrails.Text, StringComparison.Ordinal)
            < block.IndexOf("You are Warden.", StringComparison.Ordinal),
            "The persona must render below the guardrails, or switching it off could strip them.");
    }

    [Fact]
    public void SwitchingPersonalityOffLeavesTheGuardrailsIntact()
    {
        // This is the "Personality on/off" safety property: no persona, guardrails unchanged.
        var withPersona = new PromptAssembly { Persona = "You are Warden." }.RenderCachedSystemBlock();
        var without = new PromptAssembly { Persona = null }.RenderCachedSystemBlock();

        Assert.Contains(Guardrails.Text, withPersona, StringComparison.Ordinal);
        Assert.Contains(Guardrails.Text, without, StringComparison.Ordinal);
        Assert.DoesNotContain("You are Warden.", without, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardrailsCannotBeOverriddenBecauseThereIsNoSetter()
    {
        // Not a behavioural assertion so much as a structural one: if this ever compiles with an
        // instance setter, the property has stopped being unremovable.
        var property = typeof(PromptAssembly).GetProperty(nameof(PromptAssembly.Guardrails));

        Assert.NotNull(property);
        Assert.True(property.GetGetMethod()!.IsStatic, "Guardrails must be static, not per-instance.");
        Assert.Null(property.SetMethod);
    }

    [Fact]
    public void AboutMeRendersAfterThePersona()
    {
        var block = new PromptAssembly
        {
            Persona = "You are Warden.",
            AboutMe = "I fly an Asp Explorer and mostly explore.",
        }.RenderCachedSystemBlock();

        Assert.True(
            block.IndexOf("You are Warden.", StringComparison.Ordinal)
            < block.IndexOf("Asp Explorer", StringComparison.Ordinal),
            "About Me is more volatile than the persona, so it must render after it.");
    }

    /// <summary>
    /// Position 4 is labelled to commit, not to hedge (Phase 43). A biography names
    /// things that are the Commander's own invention, and the model is told it is true of the
    /// world they share — not handed a disclaimer about whose words these are.
    /// </summary>
    [Fact]
    public void AboutMeIsPresentedAsTrueOfTheWorldTheyShare()
    {
        var block = new PromptAssembly
        {
            Persona = "You are Warden.",
            AboutMe = "I served under Admiral Tanner, who the Federation would rather forget.",
        }.RenderCachedSystemBlock();

        Assert.Contains(PromptAssembly.AboutMeLabel, block, StringComparison.Ordinal);
        Assert.Contains("true of the world you share", PromptAssembly.AboutMeLabel, StringComparison.Ordinal);
        Assert.Contains("Never question it", PromptAssembly.AboutMeLabel, StringComparison.Ordinal);

        // The old label, which said only whose words they were and nothing about what to do
        // with them.
        Assert.DoesNotContain("in their own words:", block, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveGameStateIsNotInTheCachedBlock()
    {
        // Game state changes every turn. In the cached region it would invalidate the prefix on
        // every single turn, which is the exact failure the ordering exists to prevent.
        var block = new PromptAssembly
        {
            Persona = "You are Warden.",
            LiveGameState = "Current system: Shinrarta Dezhra.",
        }.RenderCachedSystemBlock();

        Assert.DoesNotContain("Shinrarta Dezhra", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCachedBlockIsByteIdenticalForTheSameInputs()
    {
        var first = new PromptAssembly { Persona = "You are Warden.", AboutMe = "Explorer." };
        var second = new PromptAssembly { Persona = "You are Warden.", AboutMe = "Explorer." };

        Assert.Equal(first.RenderCachedSystemBlock(), second.RenderCachedSystemBlock());
    }
}
