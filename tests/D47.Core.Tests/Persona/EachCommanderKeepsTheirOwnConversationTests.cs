using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Picking a Commander hands the turn loop that Commander's conversation (#1109).</summary>
public class EachCommanderKeepsTheirOwnConversationTests
{
    private string? _shown = "F1";

    private PersonaHost Host() => new(PersonaCatalog.Warden, commander: () => _shown);

    private static TurnLoop Loop(ILlmProvider provider)
    {
        var registry = CapabilityRegistry.Build([]);

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task AskAsync(TurnLoop loop, string input)
    {
        await foreach (var _ in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
        }
    }

    /// <summary>What the loop is handed on a pick, as <c>AppHost.OnCommanderChanged</c> hands it.</summary>
    private void Pick(string frontierId, PersonaHost host, TurnLoop loop)
    {
        _shown = frontierId;
        loop.UseTranscript(host.Transcript);
    }

    [Fact]
    public void APickedCommanderStartsFromTheirOwnConversationAndPickingBackResumesTheFirst()
    {
        var host = Host();
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said by Commander A"));

        _shown = "F2";
        Assert.Empty(host.Transcript);
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said by Commander B"));

        _shown = "F1";
        var resumed = Assert.Single(host.Transcript);
        Assert.Equal("said by Commander A", resumed.Text);
    }

    [Fact]
    public async Task TheTurnAfterAPickCarriesNoneOfThePreviousCommandersMessages()
    {
        var host = Host();
        var provider = FakeLlmProvider.Answering("Understood.");
        var loop = Loop(provider);
        loop.UseTranscript(host.Transcript);

        await AskAsync(loop, "remember the Colonia run");
        Assert.Equal(2, host.Transcript.Count);

        Pick("F2", host, loop);
        await AskAsync(loop, "where is my carrier");

        Assert.DoesNotContain(provider.LastRequest!.Prompt.History, message => message.Text.Contains("Colonia", StringComparison.Ordinal));
        Assert.DoesNotContain(provider.LastRequest.Prompt.History, message => message.Text == "Understood.");

        Pick("F1", host, loop);
        await AskAsync(loop, "what did I just ask");

        Assert.Contains(provider.LastRequest.Prompt.History, message => message.Text.Contains("remember the Colonia run", StringComparison.Ordinal));
        Assert.DoesNotContain(provider.LastRequest.Prompt.History, message => message.Text.Contains("where is my carrier", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATurnInFlightDuringAPickFinishesIntoTheConversationItStartedIn()
    {
        var host = Host();
        var provider = FakeLlmProvider.Answering("Plotted.");
        var loop = Loop(provider);
        loop.UseTranscript(host.Transcript);
        var commanderAs = host.Transcript;
        var routed = false;

        await using var turn = loop.RunAsync("plot a route to Colonia", cancellationToken: TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        while (await turn.MoveNextAsync())
        {
            if (turn.Current is TurnEvent.Routed)
            {
                routed = true;
                break;
            }
        }

        Assert.True(routed);
        Assert.Equal(0, provider.CallCount);

        Pick("F2", host, loop);

        while (await turn.MoveNextAsync())
        {
        }

        Assert.Equal(2, commanderAs.Count);
        Assert.Empty(host.Transcript);
        Assert.Same(host.Transcript, loop.History);
    }

    [Fact]
    public void APickRaisedByTheGameStateAlreadyShowsThePickedCommander()
    {
        var store = new GameStateStore();
        store.Pick(new CommanderIdentity("F1", "Alice"));

        var host = new PersonaHost(PersonaCatalog.Warden, commander: () => store.Active?.Identity.FrontierId);
        var loop = Loop(FakeLlmProvider.Answering("Understood."));
        loop.UseTranscript(host.Transcript);
        var alices = host.Transcript;
        alices.Add(new ConversationMessage(ConversationRole.User, "said by Alice"));

        store.CommanderChanged += _ => loop.UseTranscript(host.Transcript);
        store.Pick(new CommanderIdentity("F2", "Bob"));

        Assert.Empty(loop.History);
        Assert.Same(host.Transcript, loop.History);

        store.Pick(new CommanderIdentity("F1", "Alice"));

        Assert.Same(alices, loop.History);
    }

    [Fact]
    public void TheConversationHeldBeforeAnyCommanderIsShownGoesToTheFirstShownAsTheSameList()
    {
        _shown = null;
        var host = Host();
        var held = host.Transcript;
        held.Add(new ConversationMessage(ConversationRole.User, "said before the journal named anybody"));

        _shown = "F1";
        Assert.Same(held, host.Transcript);

        _shown = "F2";
        Assert.Empty(host.Transcript);

        _shown = "F1";
        Assert.Same(held, host.Transcript);
    }

    [Fact]
    public void ACoreSwitchPicksThatCommandersConversationWithTheNewCore()
    {
        var host = Host();
        host.Apply(new PersonaSettings { Id = "cora" });
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said to Cora by Commander A"));

        _shown = "F2";
        host.Apply(new PersonaSettings { Id = "warden" });
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said to Warden by Commander B"));

        host.Apply(new PersonaSettings { Id = "cora" });
        Assert.Empty(host.Transcript);

        _shown = "F1";
        Assert.Equal("said to Cora by Commander A", Assert.Single(host.Transcript).Text);
    }

    [Fact]
    public void ALogoutForgetsEveryCommandersConversation()
    {
        var host = Host();
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said by Commander A"));
        _shown = "F2";
        host.Transcript.Add(new ConversationMessage(ConversationRole.User, "said by Commander B"));

        host.ForgetTranscripts();

        Assert.Empty(host.Transcript);
        _shown = "F1";
        Assert.Empty(host.Transcript);
    }
}
