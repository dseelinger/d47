using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Memory;
using Xunit;

namespace D47.Core.Tests.Memory;

/// <summary>
/// What reaches a prompt, and what it costs to put it there (Phase 31, "Recall arrives above
/// the cache breakpoint").
/// <para>
/// Two properties, and the second is the expensive one. The set has to be <b>bounded and honest
/// about being a sample</b>, and the rendered bytes have to <b>stand still</b> — because recall sits
/// above the cache breakpoint, so a block that moved per turn would take 39,000-odd bytes of tool
/// schema cold with it every time.
/// </para>
/// </summary>
public class MemoryRecallTests
{
    private static readonly DateTimeOffset Now = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private static MemoryEntry Entry(
        string key,
        string fact,
        MemoryTier tier = MemoryTier.Stated,
        params string[] about) =>
        new(key, fact) { Tier = tier, About = about, AddedAt = Now };

    private static MemorySituation In(string system, string ship = "Krait_MkII") =>
        new(system, ship, AmbientSituation.Supercruise);

    [Fact]
    public void AnEmptyStoreRendersNothingRatherThanAnEmptySection()
    {
        Assert.Null(MemoryRecall.Render([], MemorySituation.Unknown));
    }

    [Fact]
    public void TheBlockIsBoundedByCount()
    {
        var many = Enumerable.Range(1, MemoryRecall.MaxEntries + 12)
            .Select(n => Entry($"told-{n}", $"Fact number {n}."))
            .ToArray();

        var selection = MemoryRecall.Select(many, MemorySituation.Unknown);

        Assert.Equal(MemoryRecall.MaxEntries, selection.Shown.Count);
        Assert.Equal(many.Length, selection.Held);
        Assert.True(selection.IsSample);
    }

    [Fact]
    public void TheBlockIsBoundedByCharactersAsWell()
    {
        // Four facts at the store's own ceiling would be 1,600 characters, over the budget, so the
        // character bound has to bind before the count one does.
        var long_ = new string('x', MemoryBook.MaxFact);

        var many = Enumerable.Range(1, 6).Select(n => Entry($"told-{n}", long_)).ToArray();

        var selection = MemoryRecall.Select(many, MemorySituation.Unknown);

        Assert.True(selection.Shown.Count < MemoryRecall.MaxEntries);
        Assert.True(selection.Shown.Sum(entry => entry.Spoken().Length) <= MemoryRecall.MaxCharacters);
    }

    /// <summary>
    /// The sentence that keeps the model honest. Without it, a model handed three of forty facts
    /// answers "what do you remember about me" with three facts and no hedge.
    /// </summary>
    [Fact]
    public void ASampleSaysHowMuchItIsLeavingOut()
    {
        var many = Enumerable.Range(1, 20).Select(n => Entry($"told-{n}", $"Fact {n}.")).ToArray();

        var block = MemoryRecall.Render(many, MemorySituation.Unknown)!;

        Assert.Contains($"{MemoryRecall.MaxEntries} of 20 things", block, StringComparison.Ordinal);
        Assert.Contains("Do not claim this is everything", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWholeStoreDoesNotPretendToBeASample()
    {
        var block = MemoryRecall.Render([Entry("told-1", "One thing.")], MemorySituation.Unknown)!;

        Assert.Contains("all 1 of it", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Do not claim this is everything", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The property the placement above the breakpoint rests on. Twenty jumps through systems with
    /// nothing attached must not produce twenty different blocks, or every jump costs a cold prefix.
    /// </summary>
    [Fact]
    public void FlyingSomewhereWithNoHistoryRendersTheSameBytes()
    {
        MemoryEntry[] entries =
        [
            Entry("told-1", "I keep a fuel scoop on everything."),
            Entry("seen-where", "you were last aboard in Deciat.", MemoryTier.Observed, "system:deciat"),
        ];

        var first = MemoryRecall.Render(entries, In("Shinrarta Dezhra"));
        var second = MemoryRecall.Render(entries, In("Colonia"));
        var third = MemoryRecall.Render(entries, In("LHS 3447", ship: "Anaconda"));

        Assert.Equal(first, second);
        Assert.Equal(first, third);

        // And no situation leaks into the text, which is what makes the equality above structural
        // rather than a coincidence of these three names.
        Assert.DoesNotContain("Shinrarta", first!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArrivingSomewhereWithHistoryPutsItFirst()
    {
        MemoryEntry[] entries =
        [
            .. Enumerable.Range(1, MemoryRecall.MaxEntries).Select(n => Entry($"told-{n}", $"Fact {n}.")),
            Entry("told-99", "Farseer is two jumps from here.", MemoryTier.Stated, "system:deciat"),
        ];

        // Crowded out entirely when it is not relevant — the budget is full of the others.
        Assert.DoesNotContain(
            "Farseer",
            MemoryRecall.Render(entries, In("Colonia"))!,
            StringComparison.Ordinal);

        // And first when it is. This is the one turn where a cold prefix is worth paying for.
        var here = MemoryRecall.Render(entries, In("Deciat"))!;

        Assert.Contains("Farseer", here, StringComparison.Ordinal);
        Assert.Contains("- You told me: Farseer is two jumps from here.", here, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommandersOwnWordOutranksAnInference()
    {
        MemoryEntry[] entries =
        [
            Entry("noted-1", "They seem to like mining.", MemoryTier.Inferred),
            Entry("seen-ship", "you were flying a Krait Phantom.", MemoryTier.Observed),
            Entry("told-1", "I hate mining.", MemoryTier.Stated),
        ];

        var order = MemoryRecall.Select(entries, MemorySituation.Unknown).Shown.Select(e => e.Key).ToArray();

        Assert.Equal(["told-1", "seen-ship", "noted-1"], order);
    }

    [Fact]
    public void TheSameStoreAndSituationAlwaysRenderIdentically()
    {
        // The determinism the whole quantization rests on, asserted the way ToolProfileTests
        // asserts it for the tool set.
        MemoryEntry[] entries =
        [
            Entry("told-2", "Second.", MemoryTier.Stated, "system:deciat"),
            Entry("told-1", "First.", MemoryTier.Stated, "system:deciat"),
            Entry("noted-1", "Third.", MemoryTier.Inferred),
        ];

        Assert.Equal(
            MemoryRecall.Render(entries, In("Deciat")),
            MemoryRecall.Render(entries.Reverse().ToArray(), In("Deciat")));
    }

    [Fact]
    public void AskingWhatIsRememberedGetsTheStoreRatherThanTheSample()
    {
        var many = Enumerable.Range(1, 20).Select(n => Entry($"told-{n}", $"Fact {n}.")).ToArray();

        var described = MemoryRecall.Describe(many);

        Assert.Contains("20 things", described, StringComparison.Ordinal);
        Assert.Equal(20, described.Split("\n- ").Length - 1);
    }

    [Fact]
    public void RecallSitsInsideTheCachedBlockBelowAboutMeAndBelowTheGuardrails()
    {
        var prompt = new PromptAssembly
        {
            Persona = "PERSONA",
            AboutMe = "ABOUT",
            Recall = "RECALL",
        };

        var block = prompt.RenderCachedSystemBlock();

        // Order is the type's contract: guardrails can never be pushed down by anything, and
        // recall is d47's account of the Commander so it reads after their own.
        Assert.StartsWith(PromptAssembly.Guardrails, block, StringComparison.Ordinal);
        Assert.True(block.IndexOf("PERSONA", StringComparison.Ordinal) < block.IndexOf("ABOUT", StringComparison.Ordinal));
        Assert.True(block.IndexOf("ABOUT", StringComparison.Ordinal) < block.IndexOf("RECALL", StringComparison.Ordinal));
    }

    [Fact]
    public void NoRecallLeavesTheCachedBlockExactlyAsItWasBeforeThePhase()
    {
        var without = new PromptAssembly { Persona = "PERSONA", AboutMe = "ABOUT" };
        var withNothing = without with { Recall = "   " };

        // A Commander who has never had anything written down must pay nothing for the section
        // existing — including not paying a blank line for it.
        Assert.Equal(without.RenderCachedSystemBlock(), withNothing.RenderCachedSystemBlock());
    }
}
