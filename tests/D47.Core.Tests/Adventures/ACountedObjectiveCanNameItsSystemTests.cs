using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A map, signal or organic objective that names a system counts only events in it, and is written only where the system can supply the count.</summary>
public sealed class ACountedObjectiveCanNameItsSystemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private const string NothingObjected = """{"objections": []}""";

    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    public static TheoryData<TriggerKind> Counted => new(TriggerKind.Map, TriggerKind.Signal, TriggerKind.Organic);

    private static AdventureStanding Fold(AdventureTrigger counted, IEnumerable<JournalEvent> events) =>
        events.Aggregate(
            AdventureFold.Start(LanternRoute(Accepted) with
            {
                Beats = [Beat("The Lantern", "setup", ToLantern, "Scoop here."), Beat("The Count", "catalyst", counted, "Done.")],
            }),
            AdventureFold.Apply);

    private static JournalEvent Happening(TriggerKind kind, long address, DateTimeOffset at) => kind switch
    {
        TriggerKind.Map =>
            Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"SAAScanComplete", "BodyName":"A 1", "SystemAddress":{{address}}, "BodyID":4, "ProbesUsed":5, "EfficiencyTarget":6 }"""),
        TriggerKind.Signal =>
            Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"SAASignalsFound", "BodyName":"A 1", "SystemAddress":{{address}}, "BodyID":4, "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2}] }"""),
        _ =>
            Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"ScanOrganic", "ScanType":"Analyse", "Genus":"$Codex_Ent_Aleoids_Genus_Name;", "Species":"$Codex_Ent_Aleoids_01_Name;", "SystemAddress":{{address}}, "Body":4 }"""),
    };

    [Theory]
    [MemberData(nameof(Counted))]
    public void AnEventInTheNamedSystemCounts(TriggerKind kind)
    {
        var counted = new AdventureTrigger { Kind = kind, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(counted, [Jump(Lantern, Accepted.AddMinutes(1)), Happening(kind, Hollow, Accepted.AddMinutes(2))]);

        Assert.True(standing.IsDone);
    }

    [Theory]
    [MemberData(nameof(Counted))]
    public void AnEventElsewhereDoesNotCount(TriggerKind kind)
    {
        var counted = new AdventureTrigger { Kind = kind, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(counted, [Jump(Lantern, Accepted.AddMinutes(1)), Happening(kind, Lantern, Accepted.AddMinutes(2))]);

        Assert.False(standing.IsDone);
    }

    [Theory]
    [MemberData(nameof(Counted))]
    public void AnEventAnywhereCountsWhenNoSystemIsNamed(TriggerKind kind)
    {
        var counted = new AdventureTrigger { Kind = kind, Count = 1 };

        var standing = Fold(counted, [Jump(Lantern, Accepted.AddMinutes(1)), Happening(kind, Hollow, Accepted.AddMinutes(2))]);

        Assert.True(standing.IsDone);
    }

    private static BodySummary Body(string name, string? subtype = null, params (string Kind, int Count)[] signals) =>
        new() { Name = name, SystemName = "Dyson's Hollow", SystemAddress = Hollow, Subtype = subtype ?? "Icy body", Signals = signals };

    private static readonly IReadOnlyList<BodySummary> ThreeBodiesAndAStar =
    [
        Body("Dyson's Hollow A", "G (White-Yellow) Star"),
        Body("Dyson's Hollow 1", signals: ("Biological", 2)),
        Body("Dyson's Hollow 2"),
        Body("Dyson's Hollow 3", signals: ("Biological", 1)),
    ];

    private static readonly IReadOnlyList<SurveyedBody> TwoSpecies =
    [
        new("Dyson's Hollow 1", 1, 1000, [new ExobiologySpecies("Aleoida", "Aleoida Arcus", 1, 1000)]),
        new("Dyson's Hollow 3", 3, 1000, [new ExobiologySpecies("Aleoida", "Aleoida Coronamus", 1, 1000)]),
    ];

    private static string Beats(string kind, int count, string? system, string? filter = null) => $$"""
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Count", "function": "turn", "kind": "{{kind}}", "count": {{count}}, "filter": {{(filter is null ? "null" : $"\"{filter}\"")}}, "system": {{(system is null ? "null" : $"\"{system}\"")}}, "line": "Count them."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider, AdventureGeneratorTests.Galaxy galaxy) =>
        AdventureGeneratorTests.Generator(provider, galaxy)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    private static RoundScriptedLlmProvider Scripted(string beats, params string[] after) =>
        new([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(beats),
            .. after.Select(reply => RoundScriptedLlmProvider.Saying(reply)),
        ]);

    [Theory]
    [InlineData("map", 3, null)]
    [InlineData("signal", 2, "Biological")]
    [InlineData("organic", 2, "Aleoida")]
    public async Task ADraftedObjectiveNamingASystemThatCanSupplyTheCountKeepsItsAddress(string kind, int count, string? filter)
    {
        var galaxy = new AdventureGeneratorTests.Galaxy { SystemBodies = ThreeBodiesAndAStar, SystemBiology = TwoSpecies };

        var outcome = await Write(Scripted(Beats(kind, count, "Dyson's Hollow", filter), NothingObjected), galaxy);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var trigger = outcome.Draft!.Beats[1].Trigger;

        Assert.Equal(Hollow, trigger.SystemAddress);
        Assert.Equal("Dyson's Hollow", trigger.System);
    }

    [Theory]
    [InlineData("map", 4, null, "it has 3 bodies that can be mapped")]
    [InlineData("signal", 3, "Biological", "it has 2 bodies with that signal")]
    [InlineData("organic", 3, "Aleoida", "it has 2 species with biology on record")]
    public async Task AnObjectiveNamingASystemThatCannotSupplyTheCountIsRefused(string kind, int count, string? filter, string shortfall)
    {
        var galaxy = new AdventureGeneratorTests.Galaxy { SystemBodies = ThreeBodiesAndAStar, SystemBiology = TwoSpecies };
        var provider = Scripted(Beats(kind, count, "Dyson's Hollow", filter), Repairs.Replacing(Beats(kind, 2, null, filter), 2), NothingObjected);

        var outcome = await Write(provider, galaxy);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(shortfall, provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AnObjectiveNamingNoSystemKeepsNoAddress()
    {
        var outcome = await Write(Scripted(Beats("map", 2, null), NothingObjected), new AdventureGeneratorTests.Galaxy());

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Beats[1].Trigger.SystemAddress);
    }

    [Fact]
    public async Task AnObjectiveNamingASystemTheSearchDoesNotKnowIsRefused()
    {
        var provider = Scripted(Beats("map", 2, "Nowhere Atall"), Repairs.Replacing(Beats("map", 2, null), 2), NothingObjected);

        var outcome = await Write(provider, new AdventureGeneratorTests.Galaxy());

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("names a system, \"Nowhere Atall\", that the galaxy search does not know", provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public void TheConfirmationSaysWhereWhenTheObjectiveNamesASystem()
    {
        Assert.Contains("in Epsilon Indi", TriggerConfirms.Of(TriggerKind.Map, null, false, "Epsilon Indi"));
        Assert.Contains("in Epsilon Indi", TriggerConfirms.Of(TriggerKind.Signal, null, false, "Epsilon Indi"));
        Assert.Contains("in Epsilon Indi", TriggerConfirms.Of(TriggerKind.Organic, null, false, "Epsilon Indi"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Map), TriggerConfirms.Of(TriggerKind.Map, null, false, null));
    }

    [Fact]
    public void TheHandOffSaysWhereToCount()
    {
        Assert.Equal("map 2 bodies in Epsilon Indi with the surface mapper", new AdventureTrigger { Kind = TriggerKind.Map, Count = 2, System = "Epsilon Indi" }.Describe());
    }
}
