using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A generated adventure does not send the Commander to a permit-locked system they have not visited and whose
/// permit their navy rank does not grant: such a system is left out of the places offered, and a beat that names
/// one is sent back through the turn.
/// </summary>
public sealed class APermitSystemIsNeverAStopTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Founders' Toast", "premise": "A toast is owed.", "want": "To pay it.",
         "stake": "Whether a debt outlives the one owed.", "turn": "It was never theirs.", "ending": "It is drunk."}
        """;

    private const string ToShinrarta = """
        {"opening": "Somebody raised a glass.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Memorial", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Shinrarta Dezhra", "station": "Jameson Memorial", "line": "The bar."},
          {"title": "The Anchorage", "function": "resolution", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Paid."}
        ]}
        """;

    private const string Elsewhere = """
        {"opening": "Somebody raised a glass.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Hollow", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "line": "The bar."},
          {"title": "The Anchorage", "function": "resolution", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Paid."}
        ]}
        """;

    [Fact]
    public async Task ABeatInAPermitSystemGoesBackThroughTheTurn()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(Elsewhere, 2)));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain(outcome.Draft!.Beats, beat => beat.Trigger.System == "Shinrarta Dezhra");
        Assert.Contains(
            "Objective 2 (The Memorial) is in Shinrarta Dezhra, which needs a permit the Commander may not hold",
            provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task APermitSystemTheCommanderIsInIsNoStopEither()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(Elsewhere, 2)));

        var outcome = await AdventureGeneratorTests.Generator(
                provider,
                new AdventureGeneratorTests.Galaxy(),
                1,
                """{ "timestamp":"2026-08-22T11:02:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "StarPos":[55.71875,17.59375,27.15625], "Docked":false }""")
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain(outcome.Draft!.Beats, beat => beat.Trigger.System == "Shinrarta Dezhra");
        Assert.Contains(
            "Objective 2 (The Memorial) is in Shinrarta Dezhra, which needs a permit the Commander may not hold",
            provider.Requests[2].Prompt.History[0].Text);
        Assert.DoesNotContain("Jameson Memorial", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public async Task APermitSystemIsNotAmongThePlacesOffered()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(Elsewhere));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var spine = provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("- Dyson's Hollow (12 ly): stations Maren Anchorage (large pad)", spine);
        Assert.DoesNotContain("Jameson Memorial", spine);
        Assert.DoesNotContain("Jameson Memorial", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AStarterSystemIsNamedAsOneWhenARepairIsAsked()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta.Replace("Shinrarta Dezhra", "HIP 97950").Replace("Jameson Memorial", "Starter Port")),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(Elsewhere, 2)));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(
            "Objective 2 (The Memorial) is in HIP 97950, a starter system the Commander can no longer enter. Move objective 2 to Maren Anchorage in Dyson's Hollow; rewrite its lines for that place.",
            provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ARefusedDockIsMovedToAnOpenStationInReach()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(Elsewhere, 2)));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(
            "Objective 2 (The Memorial) is in Shinrarta Dezhra, which needs a permit the Commander may not hold. Move objective 2 to Maren Anchorage in Dyson's Hollow; rewrite its lines for that place.",
            provider.Requests[2].Prompt.History[0].Text);
        Assert.Contains(outcome.Draft!.Beats, beat => beat.Trigger.Station == "Maren Anchorage");
    }

    [Fact]
    public async Task ARefusalKeepsItsTextWhenTheGalaxySearchIsDown()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(Elsewhere, 2)));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy { WideStationsUnavailable = true })
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(
            "Objective 2 (The Memorial) is in Shinrarta Dezhra, which needs a permit the Commander may not hold; use a system without one.",
            provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ALandIsMovedToTheNearestOpenLandableBody()
    {
        var resolver = new AdventureResolver(new AdventureGeneratorTests.Galaxy());

        var moved = await resolver.NearestOpenAsync(
            TriggerKind.Land, "Shinrarta Dezhra", "Dyson's Hollow", 50, null,
            (system, _) => system == "Shinrarta Dezhra", CancellationToken.None);

        Assert.Equal("Ossen's Lantern 2 a in Ossen's Lantern", moved?.ToString());
    }

    [Fact]
    public async Task APlaceWithNoOpenCandidateInReachIsNotMoved()
    {
        var resolver = new AdventureResolver(new AdventureGeneratorTests.Galaxy());

        Assert.Null(await resolver.NearestOpenAsync(
            TriggerKind.Land, "Shinrarta Dezhra", "Dyson's Hollow", 50, null, (_, _) => true, CancellationToken.None));
        Assert.Null(await resolver.NearestOpenAsync(
            TriggerKind.Arrive, "Shinrarta Dezhra", "Dyson's Hollow", 50, null, (_, _) => false, CancellationToken.None));
    }

    [Fact]
    public async Task AVisitedPermitSystemIsOfferedAndStands()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta));

        const string root = "C:/d47-test/visits";
        var files = new MemoryFileSystem();
        files.WriteBytes(
            Path.Combine(root, "1", VisitedStarsCache.FileName),
            VisitedStarsCacheIsReadFromItsBytesTests.File(512, [(3932277478106, 1, VisitedStarsCacheIsReadFromItsBytesTests.Second)]));

        var state = AdventureGeneratorTests.State(1, []);
        var generator = new AdventureGenerator(
            () => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state,
            () => new AdventureGeneratorTests.Galaxy(), () => null, null, null,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            visited: new VisitedStarsBook(root, files));

        var outcome = await generator.GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("Jameson Memorial", provider.Requests[0].Prompt.History[0].Text);
        Assert.Contains(outcome.Draft!.Beats, beat => beat.Trigger.System == "Shinrarta Dezhra");
    }
}
