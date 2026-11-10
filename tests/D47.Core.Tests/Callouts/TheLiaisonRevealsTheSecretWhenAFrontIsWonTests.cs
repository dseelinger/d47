using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// When a conflict the faction won enters the chronicle, the Liaison speaks the next piece of the Nemesis arc's secret in
/// its own voice, and the reveal is kept.
/// </summary>
public sealed class TheLiaisonRevealsTheSecretWhenAFrontIsWonTests
{
    private const string Rival = "Lave Purple Gang";

    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly CampaignFrontsSnapshot _snapshot;

    private DateTimeOffset _clock = Now;

    public TheLiaisonRevealsTheSecretWhenAFrontIsWonTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);

        var arc = new CampaignArc(ArcKind.Nemesis, Rival, "Director Varga", Now.AddDays(-10), 0);
        _store.SetArc("F1", arc);
        _store.SetSecret(
            "F1",
            arc,
            new ArcSecret(Rival, SecretKind.Traitor, "Arlen Voss", "Arlen Voss sold the plans.", ["A key was found.", "It was signed out.", "Arlen Voss signed it."], 0, []));

        _snapshot = new CampaignFrontsSnapshot(
            "F1",
            Us,
            Report(Place("Lave", 0.40, others: [new FactionNeighbour(Rival, 0.3)])),
            Now,
            [new Front(FrontKind.Conflict, "Lave", "War in Lave.", SystemIntent.Hold, true, Rival, "", 0.1, Now, false)],
            Now,
            null,
            null);
    }

    private CampaignArcCallout Arcs() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
        LiaisonVoice = faction => new PinnedVoice("edge", "liaison-" + faction),
    };

    private List<Announcement> Examine(CampaignArcCallout arcs, bool priming = false) =>
        [.. arcs.Examine(new CalloutContext(_clock, priming, _state, new GameStatus { Flags = StatusFlags.Docked }, NavRoute.None, []))];

    /// <summary>Every line the arcs speak, a pass every <see cref="CampaignArcCallout.Gap"/>, until a pass says nothing.</summary>
    private List<Announcement> Drain(CampaignArcCallout arcs)
    {
        var said = new List<Announcement>();

        for (var pass = 0; pass < 10 && Examine(arcs) is { Count: > 0 } lines; pass++)
        {
            said.AddRange(lines);
            _clock += CampaignArcCallout.Gap;
        }

        return said;
    }

    [Fact]
    public void AWonConflictHasTheLiaisonSpeakTheFirstPieceAndItIsKept()
    {
        var arcs = Arcs();
        Examine(arcs, priming: true);
        Drain(arcs);

        _store.Choose("F1", FrontKind.Conflict, "Lave", Now.AddDays(-2));
        _store.Resolve("F1", FrontKind.Conflict, "Lave", new ChronicleEntry("Conflict", "Lave", Rival, "Won", [Now.AddDays(-1)], []));

        var reveal = Assert.Single(Drain(arcs), announcement => announcement.Key == CampaignArcCallout.KeyPrefix + "reveal");

        Assert.Equal(CampaignLiaison.Speaker, reveal.Speaker);
        Assert.Equal("liaison-" + Us, reveal.Pinned!.VoiceId);
        Assert.Contains("A key was found.", reveal.Text, StringComparison.Ordinal);
        Assert.Single(_store.For("F1").Secret!.Revealed);
    }

    [Fact]
    public void ALostConflictRevealsNothing()
    {
        var arcs = Arcs();
        Examine(arcs, priming: true);

        _store.Choose("F1", FrontKind.Conflict, "Lave", Now.AddDays(-2));
        _store.Resolve("F1", FrontKind.Conflict, "Lave", new ChronicleEntry("Conflict", "Lave", Rival, "Lost", [Now.AddDays(-1)], []));

        Assert.DoesNotContain(Drain(arcs), announcement => announcement.Key == CampaignArcCallout.KeyPrefix + "reveal");
        Assert.Empty(_store.For("F1").Secret!.Revealed);
    }
}
