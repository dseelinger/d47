using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A model answer about one engineer carries a note inviting a remark on the tribute, once per session (#26).</summary>
public class AnEngineersTributeEarnsOneRemarkPerSessionTests
{
    private const string Persona = "You are a dry ship's AI.";

    private static TurnLoop Build(ILlmProvider provider, string? persona)
    {
        var store = new GameStateStore();
        var registry = CapabilityRegistry.Build([EngineerCapability.Create(() => store.Active)]);

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
            Persona = persona,
        };
    }

    private static async Task<string> AskAsync(TurnLoop loop, RoundScriptedLlmProvider provider, string engineer)
    {
        await foreach (var _ in loop.RunAsync(
            $"what does {engineer} want",
            cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        var last = provider.Requests[^1];

        return string.Join(
            "\n",
            last.Prompt.History
                .SelectMany(message => message.Content)
                .OfType<ConversationContent.ToolResult>()
                .Select(result => result.Content));
    }

    private static RoundScriptedLlmProvider Provider(string engineer, int turns) =>
        new([.. Enumerable.Range(0, turns).SelectMany(_ => new[]
        {
            RoundScriptedLlmProvider.Calling("call", "find_engineer", $$"""{"engineer":"{{engineer}}"}"""),
            RoundScriptedLlmProvider.Saying("There."),
        })]);

    [Fact]
    public async Task ThePersonaIsInvitedToRemarkAndTheTributeIsStatedAsWritten()
    {
        var provider = Provider("Liz Ryder", 1);
        var seen = await AskAsync(Build(provider, Persona), provider, "Liz Ryder");

        Assert.Contains("Landmines", seen, StringComparison.Ordinal);
        Assert.Contains("one short remark", seen, StringComparison.Ordinal);
        Assert.Contains("exactly as written", seen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersonalityOffInvitesNoRemark()
    {
        var provider = Provider("Liz Ryder", 1);
        var seen = await AskAsync(Build(provider, persona: null), provider, "Liz Ryder");

        Assert.Contains("Landmines", seen, StringComparison.Ordinal);
        Assert.DoesNotContain("remark", seen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSameEngineerAskedTwiceIsInvitedOnce()
    {
        var provider = Provider("Liz Ryder", 2);
        var loop = Build(provider, Persona);

        await AskAsync(loop, provider, "Liz Ryder");
        await AskAsync(loop, provider, "Liz Ryder");

        // The last request carries both turns' results: the first turn's note, and none for the second.
        var notes = provider.Requests[^1].Prompt.History
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .Where(result => result.Content.Contains("one short remark", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(notes);
    }
}
