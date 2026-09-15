using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A distant carrier's captain loses words on the way, and only the Commander's side of it loses them.</summary>
public class AFaintCaptainIsHeardInPiecesTests
{
    private const string Answer =
        "Seven hundred ninety-two tonnes of tritium in the depot, Commander, and the market is buying at a "
        + "good price this week, so we can sell some if you like.";

    private static readonly CarrierState Owned = new()
    {
        CallSign = "BNH-T2F",
        Name = "Sacred Fire",
        StarSystem = "Wolf 359",
    };

    private static TurnLoop Build(TestSurface surface, ILlmProvider provider, double lightYears)
    {
        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI aboard this vessel.",
        };

        loop.Lines.Add(new CaptainLine(
            () => Owned,
            () => "Sol",
            () => "Warden",
            (_, _, _) => Task.FromResult<double?>(lightYears)));

        return loop;
    }

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    private static string Heard(IEnumerable<TurnEvent> events) =>
        string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text));

    private static string Text(ConversationMessage message) =>
        string.Join(' ', message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));

    [Fact]
    public async Task WithinClearRangeEveryWordArrives()
    {
        using var install = new TempInstall();
        var loop = Build(TestSurface.For(install), FakeLlmProvider.Answering(Answer), lightYears: 200);

        var events = await RunAsync(loop, "Captain, how much fuel have we got");

        Assert.Equal(1, Assert.Single(events.OfType<TurnEvent.Addressed>()).Signal);
        Assert.Equal(Answer, Heard(events));
    }

    [Fact]
    public async Task FarOutTheCaptainLosesWords()
    {
        using var install = new TempInstall();
        var loop = Build(TestSurface.For(install), FakeLlmProvider.Answering(Answer), lightYears: 426);

        var events = await RunAsync(loop, "Captain, how much fuel have we got");
        var heard = Heard(events);

        Assert.Equal(LinkSignal.Strength(426), Assert.Single(events.OfType<TurnEvent.Addressed>()).Signal);
        Assert.True(heard.Length < Answer.Length, $"\"{heard}\" is no shorter than the answer");
        Assert.Contains(LinkSignal.Lost, heard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCaptainRemembersTheFullAnswerAndTheShipAiOverhearsThePieces()
    {
        using var install = new TempInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Answer),
            RoundScriptedLlmProvider.Saying("Noted."),
            RoundScriptedLlmProvider.Saying("Five hundred forty tonnes, Commander."));
        var loop = Build(TestSurface.For(install), provider, lightYears: 426);

        var heard = Heard(await RunAsync(loop, "Captain, how much fuel have we got"));

        await RunAsync(loop, "Warden, summarise that exchange for me");
        await RunAsync(loop, "Captain, and how much cargo");

        var overheard = Text(provider.Requests[1].Prompt.History[0]);

        Assert.Contains($"Captain answered \"{heard}\"", overheard, StringComparison.Ordinal);
        Assert.DoesNotContain(Answer, overheard, StringComparison.Ordinal);

        Assert.Contains(
            provider.Requests[2].Prompt.History,
            message => message.Role == ConversationRole.Assistant && Text(message) == Answer);
    }
}
