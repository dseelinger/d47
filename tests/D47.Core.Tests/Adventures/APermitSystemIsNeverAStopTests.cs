using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A generated adventure never sends the Commander to a permit-locked system, because the journal does not say
/// which permits they hold: such a system is left out of the places offered, and a beat that names one is sent
/// back through the turn.
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
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Memorial", "function": "turn", "kind": "dock", "system": "Shinrarta Dezhra", "station": "Jameson Memorial", "line": "The bar."},
          {"title": "The Anchorage", "function": "resolution", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Paid."}
        ]}
        """;

    private const string Elsewhere = """
        {"opening": "Somebody raised a glass.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Hollow", "function": "turn", "kind": "arrive", "system": "Dyson's Hollow", "line": "The bar."},
          {"title": "The Anchorage", "function": "resolution", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Paid."}
        ]}
        """;

    [Fact]
    public async Task ABeatInAPermitSystemGoesBackThroughTheTurn()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(ToShinrarta),
            RoundScriptedLlmProvider.Saying(Elsewhere));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain(outcome.Draft!.Beats, beat => beat.Trigger.System == "Shinrarta Dezhra");
        Assert.Contains(
            "Objective 2 (The Memorial) is in Shinrarta Dezhra, which needs a permit the Commander may not hold",
            provider.Requests[2].Prompt.History[0].Text);
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
}
