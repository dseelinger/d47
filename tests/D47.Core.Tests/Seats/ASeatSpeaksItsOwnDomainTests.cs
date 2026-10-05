using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Seats;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Seats;

public class ASeatSpeaksItsOwnDomainTests
{
    private static readonly DateTimeOffset Start = new(3311, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CrewSeat Ilo = new("0000000c", CrewRole.Navigation, null, "Ilo Varga");

    private static readonly CrewSeat Rhea = new("0000000d", CrewRole.SecurityOfficer, null, "Rhea Okafor");

    private sealed class FixedCallout(string id, Announcement announcement) : ICallout
    {
        public string Id => id;

        public IEnumerable<Announcement> Examine(CalloutContext context) => [announcement];
    }

    private static ShipSeats Ship(params CrewSeat[] seats) => new("F1", 7, "python", seats);

    private static CalloutContext Context() =>
        new(Start, false, null, GameStatus.Unknown, NavRoute.None, []);

    private static Announcement Spoken(string id, Announcement line, ShipSeats? seats)
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance) { SeatsFlown = () => seats }
            .Add(new FixedCallout(id, line));

        engine.Tick(Context());

        return Assert.Single(engine.Drain());
    }

    [Fact]
    public void ARouteCalloutIsSpokenByTheNavigationSeat()
    {
        var said = Spoken("route", new Announcement("route.progress", "Three jumps left."), Ship(Ilo));

        Assert.Equal(VoiceRole.Crew, said.Voice);
        Assert.Equal("Ilo Varga", said.Speaker);
        Assert.Equal(Ilo.Id, said.Seat);
        Assert.Equal("Three jumps left.", said.Text);
    }

    [Fact]
    public void ARecastWarningKeepsItsUrgencyCueAndCooldown()
    {
        var warning = new Announcement("danger.shields", "Shields down.", CalloutUrgency.Urgent)
        {
            Cue = AlertCue.UnderFire,
            Cooldown = TimeSpan.FromSeconds(30),
        };

        var said = Spoken("danger", warning, Ship(Rhea));

        Assert.Equal("Rhea Okafor", said.Speaker);
        Assert.Equal(CalloutUrgency.Urgent, said.Urgency);
        Assert.Equal(AlertCue.UnderFire, said.Cue);
        Assert.Equal(TimeSpan.FromSeconds(30), said.Cooldown);
        Assert.Equal("danger.shields", said.Key);
    }

    [Fact]
    public void ARecastLineIsSpokenInTheSeatsVoice()
    {
        var cast = new VoiceCast
        {
            DefaultVoice = "af_heart",
            Pool = ["am_adam", "af_heart", "bf_emma"],
            SeatVoice = id => id == Ilo.Id ? "bm_george" : null,
        };

        var said = Spoken("route", new Announcement("route.progress", "Three jumps left."), Ship(Ilo));

        Assert.Equal("bm_george", SpeakerAccent.VoiceOf(cast, said).VoiceId);
    }

    [Fact]
    public void ALineAnotherRoleSpeaksIsNeverRecast()
    {
        var carrier = new Announcement("carrier.jump", "Jump locked in.")
        {
            Voice = VoiceRole.CarrierCaptain,
            Speaker = "Captain Hale",
        };

        var narrated = new Announcement("route.story", "The stars thinned.") { Voice = VoiceRole.Narrator };

        Assert.Same(carrier, CrewDomains.Recast("route", carrier, Ship(Ilo)));
        Assert.Same(narrated, CrewDomains.Recast("route", narrated, Ship(Ilo)));
    }

    [Fact]
    public void ACustomSeatTakesNoDomain()
    {
        var cargo = new CrewSeat("0000000e", CrewRole.Custom, "Navigation", "Ines Roy");
        var line = new Announcement("route.progress", "Three jumps left.");

        Assert.Same(line, CrewDomains.Recast("route", line, Ship(cargo)));
    }

    [Fact]
    public void AKeptCalloutStaysWithTheCoreWhateverIsFilled()
    {
        var line = new Announcement("session.welcome", "Welcome back.");

        Assert.Same(line, CrewDomains.Recast("session", line, Ship(Ilo, Rhea)));
    }
}
