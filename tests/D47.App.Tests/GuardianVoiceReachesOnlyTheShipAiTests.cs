using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Which roles the Guardian voice treatment reaches, and which it never does (#225).</summary>
public class GuardianVoiceReachesOnlyTheShipAiTests
{
    private static (VoicePipeline Voice, List<AudioClip> Played) Build()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new OneToneProvider(),
            CuesEnabled = false,
            BedEnabled = false,

            // A marker rather than the real chain, since the chain itself is GuardianVoiceTests' job.
            GuardianColour = clip => clip with { Name = $"{clip.Name} (guardian)" },
            AwayColour = clip => clip with { Name = $"{clip.Name} (guardian) (away)" },
        };

        return (voice, sink.Played);
    }

    internal static async Task<string> NameOfWhatWasPlayed(VoiceRole role)
    {
        var (voice, played) = Build();

        await voice.AnnounceAsync(new Announcement($"line.{role}", "Scanning.") { Voice = role });

        return Assert.Single(played).Name;
    }

    [Fact]
    public async Task TheShipsAiIsGuardianTreatedWhenAnyTreatmentIsOn() =>
        Assert.EndsWith("(guardian)", await NameOfWhatWasPlayed(VoiceRole.ShipAi), StringComparison.Ordinal);

    [Fact]
    public async Task TheCrewIsNeverGuardianTreated() =>
        Assert.Equal("Scanning.", await NameOfWhatWasPlayed(VoiceRole.Crew));

    [Fact]
    public async Task TheOverTheAirRolesGetTheRadioNotTheGuardian()
    {
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.Comms), StringComparison.Ordinal);
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.CarrierCaptain), StringComparison.Ordinal);
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.TowerControl), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoTreatmentSwitchedOnLeavesTheShipsAiUnchanged()
    {
        var (voice, played) = Build();
        voice.GuardianColour = null;

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        Assert.Equal("Scanning.", Assert.Single(played).Name);
    }

    private static GameStatus OnFoot() =>
        new() { Flags = StatusFlags.InMainShip, Flags2 = (uint)StatusFlags2.OnFoot, ReadAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task TheShipsAiComesOverTheLinkWhenTheCommanderIsOnFoot()
    {
        var (voice, played) = Build();
        voice.Status = OnFoot;

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        Assert.EndsWith("(away)", Assert.Single(played).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATurnReplyComesOverTheLinkWhenTheCommanderIsOnFoot()
    {
        var (voice, played) = Build();
        voice.Status = OnFoot;

        await voice.RunAsync(Reply("Scanning."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.EndsWith("(away)", Assert.Single(played).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheShipsAiIsUnchangedInTheShipOrWithNoStatus()
    {
        var (voice, played) = Build();

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));
        voice.Status = () => new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UtcNow };
        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        Assert.All(played, clip => Assert.EndsWith("(guardian)", clip.Name, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(VoiceRole.Crew, "Scanning.")]
    [InlineData(VoiceRole.Comms, "Scanning. (radio)")]
    public async Task TheCrewAndTheRadioAreUnchangedWhenTheCommanderIsOnFoot(VoiceRole role, string expected)
    {
        var (voice, played) = Build();
        voice.Status = OnFoot;

        await voice.AnnounceAsync(new Announcement($"line.{role}", "Scanning.") { Voice = role });

        Assert.Equal(expected, Assert.Single(played).Name);
    }

    /// <summary>A turn reply is the ship AI too, unless it answers as a crew member.</summary>
    [Fact]
    public async Task ATurnReplyIsGuardianTreatedUnlessItIsSpokenAsCrew()
    {
        var (voice, played) = Build();

        await voice.RunAsync(Reply("Scanning."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.EndsWith("(guardian)", Assert.Single(played).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATurnReplySpokenAsCrewIsNot()
    {
        var (voice, played) = Build();
        voice.SpeakingAs = VoiceRole.Crew;

        await voice.RunAsync(Reply("Scanning."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Scanning.", Assert.Single(played).Name);
    }

    private static async IAsyncEnumerable<TurnEvent> Reply(string text)
    {
        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(TurnOutcome.Answered, TurnRoute.Model, text, null, null));

        await Task.CompletedTask;
    }
}
