using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The Ideology arc and the council speak over comms once each, in their own voices, wait as the other arcs do, are kept in
/// the campaign record, and are cleared when another faction is named.
/// </summary>
public sealed class TheCouncilAndTheIdeologySpeakOverCommsTests
{
    private const string Rival = "Lave Purple Gang";

    private readonly MemoryFileSystem _files = new();

    private readonly CampaignStore _store;

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private CampaignFrontsSnapshot? _snapshot;

    private DateTimeOffset _clock = Now;

    public TheCouncilAndTheIdeologySpeakOverCommsTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
    }

    private static Front Of(FrontKind kind, string system) =>
        new(kind, system, $"{kind} in {system}.", SystemIntent.Hold, true, kind == FrontKind.Coup ? Rival : null, "", 0.1, Now, false);

    private void Hold(params Front[] fronts) =>
        _snapshot = new CampaignFrontsSnapshot(
            "F1",
            Us,
            Report(Place("Lave", 0.40, others: [new FactionNeighbour(Rival, 0.5) { Government = "Dictatorship" }])),
            Now,
            fronts,
            Now,
            null,
            null);

    private CampaignArcCallout Arcs() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
        PoliticsVoice = (name, _, before) => new PinnedVoice("edge", name + before.Count),
    };

    private List<Announcement> Examine(CampaignArcCallout arcs, bool priming = false, StatusFlags flags = StatusFlags.Docked) =>
        [.. arcs.Examine(new CalloutContext(_clock, priming, _state, new GameStatus { Flags = flags }, NavRoute.None, []))];

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
    public void TheArgumentIsSpokenOnceByEachCouncillorAndKept()
    {
        var arcs = Arcs();
        Hold(Of(FrontKind.Retreat, "Leesti"));
        Examine(arcs, priming: true);
        Hold(Of(FrontKind.Coup, "Lave"), Of(FrontKind.Retreat, "Leesti"));

        var said = Drain(arcs).Where(announcement => announcement.Key.EndsWith("argument", StringComparison.Ordinal)).ToList();

        var council = _store.For("F1").Council;
        Assert.NotNull(council);
        Assert.Equal([council.Hawk, council.Dove], said.Select(announcement => announcement.Speaker));
        Assert.Equal(council.Hawk + "0", said[0].Pinned!.VoiceId);
        Assert.Equal(council.Dove + "1", said[1].Pinned!.VoiceId);
        Assert.Equal(VoiceRole.Comms, said[0].Voice);

        Hold(Of(FrontKind.Coup, "Lave"), Of(FrontKind.Retreat, "Leesti"));
        Assert.DoesNotContain(Drain(arcs), announcement => announcement.Key.EndsWith("argument", StringComparison.Ordinal));
    }

    [Fact]
    public void TheIdeologyOpensWithAPropagandaBroadcastOverTheNemesisLink()
    {
        var arcs = Arcs();
        Hold(Of(FrontKind.Retreat, "Leesti"));
        Examine(arcs, priming: true);
        Hold(Of(FrontKind.Coup, "Lave"));

        var propaganda = Assert.Single(Drain(arcs), announcement => announcement.Key.EndsWith("propaganda", StringComparison.Ordinal));

        Assert.NotNull(_store.For("F1").Ideology);
        Assert.Equal(Rival, propaganda.Arc!.Rival);
    }

    [Fact]
    public void NothingIsSaidWhileInSupercruiseAndTheOpeningIsNotLostByIt()
    {
        var arcs = Arcs();
        Hold(Of(FrontKind.Retreat, "Leesti"));
        Examine(arcs, priming: true);
        Hold(Of(FrontKind.Coup, "Lave"), Of(FrontKind.Retreat, "Leesti"));

        Assert.Empty(Examine(arcs, flags: StatusFlags.Supercruise));
        Assert.Null(_store.For("F1").Council);
        Assert.NotEmpty(Examine(arcs));
    }

    [Fact]
    public void ChoosingAFrontAfterTheCouncilOpenedHasTheOtherSideSpeak()
    {
        var arcs = Arcs();
        Hold(Of(FrontKind.Retreat, "Leesti"));
        Examine(arcs, priming: true);
        Hold(Of(FrontKind.Coup, "Lave"), Of(FrontKind.Retreat, "Leesti"));
        Drain(arcs);

        _store.Choose("F1", FrontKind.Coup, "Lave", Now.AddHours(1));

        var sided = Assert.Single(Drain(arcs), announcement => announcement.Key.EndsWith("sided", StringComparison.Ordinal));
        Assert.Equal(_store.For("F1").Council!.Dove, sided.Speaker);
        Assert.Single(_store.For("F1").Council!.Sidings);
    }

    [Fact]
    public void NamingAnotherFactionClearsThemAndBothSurviveAReload()
    {
        var ideology = new IdeologyArc(Rival, "Cooperative", "Dictatorship", Now, 1, ["Lave"], ["Lave|active"]);
        var council = new CampaignCouncil("Director Varga", "Envoy Quist", Now, Now, [new CouncilSiding("Lave", CouncilSide.Hawk, Now)]);
        _store.SetIdeology("F1", ideology);
        _store.SetCouncil("F1", council);

        var reloaded = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        Assert.Equal(ideology.Rival, reloaded.For("F1").Ideology!.Rival);
        Assert.Equal(ideology.Elections, reloaded.For("F1").Ideology!.Elections);
        Assert.Equal(council.Sidings, reloaded.For("F1").Council!.Sidings);
        Assert.Equal(council.Noted, reloaded.For("F1").Council!.Noted);

        _store.SetFaction("F1", "Another Faction", clearIntents: false);

        Assert.Null(_store.For("F1").Ideology);
        Assert.Null(_store.For("F1").Council);
    }
}
