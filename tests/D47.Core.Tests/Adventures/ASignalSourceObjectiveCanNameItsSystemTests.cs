using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A signal-source objective that names a system fires only on a drop made while the ship is in it.</summary>
public sealed class ASignalSourceObjectiveCanNameItsSystemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private const string NothingObjected = """{"objections": []}""";

    private static readonly AdventureTrigger ToLantern = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static AdventureStanding Fold(AdventureTrigger uss, IEnumerable<JournalEvent> events) =>
        events.Aggregate(
            AdventureFold.Start(LanternRoute(Accepted) with
            {
                Beats = [Beat("The Lantern", "setup", ToLantern, "Scoop here."), Beat("The Drop", "catalyst", uss, "Done.")],
            }),
            AdventureFold.Apply);

    private static JournalEvent Drop(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"USSDrop", "USSType":"$USS_Type_Salvage;", "USSThreat":0 }""");

    private static string BeatsWithDropIn(string? system) => $$"""
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Drop", "function": "turn", "kind": "uss", "count": 1, "system": {{(system is null ? "null" : $"\"{system}\"")}}, "line": "Drop in."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private static RoundScriptedLlmProvider Scripted(string beats, params string[] after) =>
        new([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(beats),
            .. after.Select(reply => RoundScriptedLlmProvider.Saying(reply)),
        ]);

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    [Fact]
    public void ADropAfterArrivingInTheNamedSystemFires()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Hollow, Accepted.AddMinutes(2)), Drop(Accepted.AddMinutes(3))]);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public void ADropAfterArrivingElsewhereDoesNotFire()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Drop(Accepted.AddMinutes(2))]);

        Assert.False(standing.IsDone);
    }

    [Fact]
    public void ADropAfterLeavingTheNamedSystemDoesNotFire()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1, SystemAddress = Hollow, System = "Dyson's Hollow" };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Hollow, Accepted.AddMinutes(2)), Jump(Lantern, Accepted.AddMinutes(3)), Drop(Accepted.AddMinutes(4))]);

        Assert.False(standing.IsDone);
    }

    [Fact]
    public void ADropWithNoSystemNamedFiresAnywhere()
    {
        var uss = new AdventureTrigger { Kind = TriggerKind.Uss, Count = 1 };

        var standing = Fold(uss, [Jump(Lantern, Accepted.AddMinutes(1)), Jump(Hollow, Accepted.AddMinutes(2)), Drop(Accepted.AddMinutes(3))]);

        Assert.True(standing.IsDone);
    }

    [Fact]
    public async Task ADraftedDropNamingASystemResolvesToThatSystemsAddress()
    {
        var outcome = await Write(Scripted(BeatsWithDropIn("Dyson's Hollow"), NothingObjected));

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var trigger = outcome.Draft!.Beats[1].Trigger;

        Assert.Equal(TriggerKind.Uss, trigger.Kind);
        Assert.Equal(Hollow, trigger.SystemAddress);
        Assert.Equal("Dyson's Hollow", trigger.System);
    }

    [Fact]
    public async Task ADraftedDropNamingNoSystemKeepsNoAddress()
    {
        var outcome = await Write(Scripted(BeatsWithDropIn(null), NothingObjected));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Beats[1].Trigger.SystemAddress);
    }

    [Fact]
    public async Task ADropNamingASystemTheSearchDoesNotKnowIsRefusedWithAReasonToActOn()
    {
        var provider = Scripted(BeatsWithDropIn("Nowhere Atall"), Repairs.Replacing(BeatsWithDropIn("Dyson's Hollow"), 2), NothingObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("names a system, \"Nowhere Atall\", that the galaxy search does not know", provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public void TheConfirmationSaysWhereTheDropWasWhenTheObjectiveNamesASystem()
    {
        Assert.Equal(
            "the ship dropped into signal sources in Epsilon Indi — not who sent the signal or what was found there",
            TriggerConfirms.Of(TriggerKind.Uss, null, false, "Epsilon Indi"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Uss), TriggerConfirms.Of(TriggerKind.Uss, null, false, null));
    }

    [Fact]
    public void TheHandOffSaysWhereToDrop()
    {
        Assert.Equal("drop into 2 signal sources in Epsilon Indi", new AdventureTrigger { Kind = TriggerKind.Uss, Count = 2, System = "Epsilon Indi" }.Describe());
    }
}
