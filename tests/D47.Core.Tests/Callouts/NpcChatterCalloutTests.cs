using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Invented background chatter (#244): a marker now and then, from a callout that follows the
/// ambient timing rules — and never a line of text, because chatter is model-written or it is
/// nothing (#245).
/// </summary>
public class NpcChatterCalloutTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static NpcChatterCallout Callout() => new()
    {
        Interval = TimeSpan.FromMinutes(20),
        Settle = TimeSpan.Zero,
    };

    private static GameStatus In(StatusFlags flags) => GameStatus.Unknown with { Flags = flags };

    private static CalloutContext Context(
        DateTimeOffset now,
        StatusFlags flags = StatusFlags.Docked | StatusFlags.InMainShip,
        bool priming = false) =>
        new(now, priming, State: null, In(flags), NavRoute.None, []);

    [Fact]
    public void TheFirstTickSeedsAndTheIntervalHoldsAfterIt()
    {
        var callout = Callout();

        // The first tick seeds the clock: silence for one whole interval after launch, exactly
        // the ambient rule.
        Assert.Empty(callout.Examine(Context(T0)));
        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromMinutes(19))));

        var emitted = callout.Examine(Context(T0 + TimeSpan.FromMinutes(21))).ToList();

        var marker = Assert.Single(emitted);
        Assert.StartsWith(NpcChatter.KeyPrefix, marker.Key, StringComparison.Ordinal);

        // A marker, never a line: empty text is what makes a missed road speak nothing rather
        // than something (#245).
        Assert.Equal(string.Empty, marker.Text);

        // And the interval holds again after it.
        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromMinutes(22))));
    }

    [Fact]
    public void OffOrZeroOrUnknownIsSilence()
    {
        var off = Callout();
        off.Enabled = () => false;
        Assert.Empty(off.Examine(Context(T0)).ToArray());
        Assert.Empty(off.Examine(Context(T0 + TimeSpan.FromHours(2))));

        var zero = Callout();
        zero.Interval = TimeSpan.Zero;
        Assert.Empty(zero.Examine(Context(T0 + TimeSpan.FromHours(2))));

        var nowhere = Callout();
        Assert.Empty(nowhere.Examine(Context(T0)).ToArray());
        Assert.Empty(nowhere.Examine(Context(T0 + TimeSpan.FromHours(2), StatusFlags.None)));
    }

    [Fact]
    public void PrimingFoldsTheBacklog()
    {
        var callout = Callout();

        // Seeded by a real first tick, so what priming folds is an exchange that would
        // otherwise be due.
        _ = callout.Examine(Context(T0)).ToArray();

        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromHours(2), priming: true)));
    }

    /// <summary>
    /// The pairing ladder is deterministic off the pick counter — no Core component reads a
    /// clock or a seed. Every fourth exchange addresses the Commander; the controller only
    /// exists somewhere to be docked at.
    /// </summary>
    [Fact]
    public void TheControllerOnlySpeaksWhereThereIsADock()
    {
        for (var pick = 0; pick < 12; pick++)
        {
            Assert.NotEqual(NpcChatterKind.Controller, NpcChatterCallout.KindFor(pick, docked: false));
        }

        Assert.Equal(NpcChatterKind.Controller, NpcChatterCallout.KindFor(0, docked: true));
        Assert.Equal(NpcChatterKind.Hail, NpcChatterCallout.KindFor(3, docked: true));
        Assert.Equal(NpcChatterKind.Hail, NpcChatterCallout.KindFor(7, docked: false));
    }
}

/// <summary>
/// The exchange itself: what the model is asked, and how strictly the reply is read.
/// </summary>
public class NpcChatterScriptTests
{
    [Fact]
    public void TheKindTravelsOnTheKeyLikeTheAmbientSituation()
    {
        Assert.Equal(NpcChatterKind.Controller, NpcChatter.KindOf("npc.chatter.controller"));
        Assert.Equal(NpcChatterKind.Hail, NpcChatter.KindOf("npc.chatter.hail"));
        Assert.Equal(NpcChatterKind.Passersby, NpcChatter.KindOf("npc.chatter.passersby"));

        // A key nothing recognises composes the harmless kind rather than failing.
        Assert.Equal(NpcChatterKind.Passersby, NpcChatter.KindOf("npc.chatter.spelunking"));
    }

    [Fact]
    public void EveryKindForbidsQuestionsToTheCommanderAndRealPeople()
    {
        foreach (var kind in Enum.GetValues<NpcChatterKind>())
        {
            var instruction = NpcChatter.Instruction(kind);

            Assert.Contains("nobody asks the Commander a question", instruction, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Never name or imitate a real person", instruction, StringComparison.Ordinal);
            Assert.Contains("Name: words", instruction, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AScriptIsReadStrictlyAndCapped()
    {
        var lines = NpcChatter.Parse(
            "Vera Kolt: Pad nine again. Third time today.\n"
            + "Dock Control: Take it up with scheduling, Kolt.\n"
            + "not a line at all\n"
            + "Vera Kolt: One day I will.\n"
            + "Dock Control: One day you will still be on pad nine.\n"
            + "Extra Voice: This one is past the cap.",
            NpcChatterKind.Controller);

        Assert.Equal(NpcChatter.MostLines, lines.Count);
        Assert.Equal("Vera Kolt", lines[0].Name);
        Assert.Equal("Pad nine again. Third time today.", lines[0].Text);
        Assert.DoesNotContain(lines, line => line.Name == "Extra Voice");
    }

    [Fact]
    public void AFragmentIsSilenceRatherThanHalfAScene()
    {
        // One surviving line of a two-person exchange is a fragment, and silence beats a
        // fragment — the same judgement the ambient drop makes (#245).
        Assert.Empty(NpcChatter.Parse("Dock Control: Cleared.", NpcChatterKind.Controller));
        Assert.Empty(NpcChatter.Parse("nothing parseable here", NpcChatterKind.Passersby));
        Assert.Empty(NpcChatter.Parse(null, NpcChatterKind.Passersby));

        // A hail is one person saying a line or two, so one line is whole.
        Assert.Single(NpcChatter.Parse("Old Hand: Fine ship. Keep her polished.", NpcChatterKind.Hail));
    }

    [Fact]
    public void ALineAboutBeingAModelDoesNotSurvive()
    {
        var lines = NpcChatter.Parse(
            "Vera Kolt: As an AI language model I cannot discuss pad assignments.\n"
            + "Dock Control: Quiet night out here.\n"
            + "Vera Kolt: Too quiet, the drives hum louder than the bar.",
            NpcChatterKind.Passersby);

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain(lines, line => line.Text.Contains("language model", StringComparison.OrdinalIgnoreCase));
    }
}
