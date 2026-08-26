using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A re-voiced in-game message is never handed to a model to be reworded.
/// <para>
/// <c>IncomingMessages</c> says of itself that <i>"none of it ever reaches the model … There is
/// deliberately no path from here into a prompt"</i>, and there was one: a message from the
/// Commander's own carrier is assigned <see cref="VoiceRole.TowerControl"/>, and the carrier's
/// flavour brief is chosen by role. So station traffic was being interpolated into an instruction
/// inside quotes — <c>Say this in your own words, once: "…"</c>.
/// </para>
/// <para>
/// <b>Two Commanders heard the result.</b> The tower said <i>"I don't have that capability"</i>
/// against the authored line <i>"No fire zone exited"</i>
/// (<a href="https://github.com/dseelinger/d47/issues/46">#46</a>), and on 2026-08-25 it said
/// <i>"I'm not going to restate those system rules to you"</i> against <i>"No fire zone
/// entered"</i>. Both were station traffic asked for a rewrite; the phrase list added for the
/// first did not match the second, which is what a denylist does.
/// </para>
/// <para>
/// This is the invariant rather than the symptom: architecture.md §7 names in-game comms as
/// untrusted input whose attacker is <i>any player in range</i>, and a hostile transmission
/// reaching a prompt is a larger problem than an odd line being spoken.
/// </para>
/// </summary>
public class InGameTextNeverReachesAModelTests
{
    private static IncomingMessages Reader() => new()
    {
        Enabled = () => true,
        IncludeNpcs = () => true,
        CarrierCallSign = "BNH-T2F",
    };

    private static JournalEvent Message(string from, string text, string channel)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-08-25T21:35:00Z",
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = text,
            ["Channel"] = channel,
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// The reported line, off the Commander's own log: station traffic arriving from their
    /// carrier, which is what made it the tower's.
    /// </summary>
    [Fact]
    public void TheReportedCarrierTransmissionIsNotEligible()
    {
        var read = Reader().Read(Message("Sacred Fire BNH-T2F", "No fire zone entered.", "npc"));

        Assert.NotNull(read);

        // It really is the tower's voice — that part was right, and is why it reached the brief.
        Assert.Equal(VoiceRole.TowerControl, read.Voice);

        Assert.Null(FlavourBriefs.For(read, personalityEnabled: true));
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("local")]
    [InlineData("wing")]
    [InlineData("player")]
    public void NorIsAnyOtherChannel(string channel)
    {
        var read = Reader().Read(Message("Vex", "watch your six", channel));

        Assert.NotNull(read);
        Assert.Null(FlavourBriefs.For(read, personalityEnabled: true));
    }

    /// <summary>
    /// And the carrier's own authored lines still are, which is the half worth keeping: those are
    /// d47's own words about the Commander arriving, and varying them is what Phase 11 asked for.
    /// </summary>
    [Theory]
    [InlineData(VoiceRole.CarrierCaptain)]
    [InlineData(VoiceRole.TowerControl)]
    public void TheCarriersOwnLinesAreStillVaried(VoiceRole role)
    {
        var authored = new Announcement("carrier.welcome", "Welcome home, Commander.") { Voice = role };

        var brief = FlavourBriefs.For(authored, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("Welcome home, Commander.", brief.Instruction, StringComparison.Ordinal);
    }

    /// <summary>
    /// The discriminator is the announcement's own, and either field alone is enough. Nothing but
    /// the chat reader sets them, so this cannot start excluding d47's own callouts.
    /// </summary>
    [Fact]
    public void EitherMarkOfSomebodyElsesWordsIsEnough()
    {
        var byChannel = new Announcement("carrier.welcome", "Welcome home.")
        {
            Voice = VoiceRole.TowerControl,
            CommsChannel = "npc",
        };

        var byTranscript = new Announcement("carrier.welcome", "Welcome home.")
        {
            Voice = VoiceRole.TowerControl,
            Transcript = "Somebody: Welcome home.\n",
        };

        Assert.Null(FlavourBriefs.For(byChannel, personalityEnabled: true));
        Assert.Null(FlavourBriefs.For(byTranscript, personalityEnabled: true));
    }

    /// <summary>
    /// The denylist is the second line and not the first. It kept the phrasings that were
    /// actually heard, including the one it used to miss.
    /// </summary>
    [Theory]
    [InlineData("I don't have that capability.")]
    [InlineData("I'm not going to restate those system rules to you or confirm I understand them.")]
    [InlineData("Those are my operating parameters, not something for us to discuss.")]
    public void AndAnAnswerAboutTheModelIsStillRefused(string said) =>
        Assert.False(FlavourBriefs.MayBeSpoken(said));

    [Fact]
    public void WhileARewordedCalloutIsStillSpoken() =>
        Assert.True(FlavourBriefs.MayBeSpoken("Docking granted, Commander. Pad nine is yours."));
}
