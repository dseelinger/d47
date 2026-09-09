using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class ARouterAnsweredTurnTellsTheModelWhatWasAskedTests
{
    /// <summary>The phrase for the action route.</summary>
    private const string ActionPhrase = "run the pre-flight";

    [Fact]
    public async Task TheKeywordRoutersOwnAnswerCarriesWhatWasAskedForIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, surface.Router, provider);

        var (routed, _) = await RunAsync(loop, "what's your status");
        Assert.Equal(TurnRoute.KeywordRouter, routed.Route);

        await RunAsync(loop, "no, the other one");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains("what's your status", carried, StringComparison.Ordinal);
        Assert.Contains("The Commander said", carried, StringComparison.Ordinal);

        // The sentence that made the old block actively misleading: it is false of a line the Commander asked
        // for, and it steers the model away from treating it as the subject.
        Assert.DoesNotContain("without being asked", carried, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnActionPhrasesOwnAnswerCarriesWhatWasAskedForIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, ActionRouter(surface.Registry), provider);

        var (routed, _) = await RunAsync(loop, ActionPhrase);
        Assert.Equal(TurnRoute.ActionCommand, routed.Route);

        await RunAsync(loop, "no, the other one");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains(ActionPhrase, carried, StringComparison.Ordinal);
        Assert.DoesNotContain("without being asked", carried, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASettingsPhrasesOwnAnswerCarriesWhatWasAskedForIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, surface.Router, provider, surface.Settings);

        var (routed, _) = await RunAsync(loop, "stop checking for updates");
        Assert.Equal(TurnRoute.SettingCommand, routed.Route);

        // The issue's second road: *"stop calling things out"* answered by the router, then *"why did you do
        // that?"* — a follow-up that needs the request as much as a course does.
        await RunAsync(loop, "why did you do that?");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains("stop checking for updates", carried, StringComparison.Ordinal);
        Assert.DoesNotContain("without being asked", carried, StringComparison.Ordinal);
    }

    /// <summary>
    /// The three-utterance flight, condensed: the second request is the one the correction is about,
    /// and the block says so rather than leaving the model to guess between two.
    /// </summary>
    [Fact]
    public async Task TheMostRecentAskedForExchangeIsNamedAsTheSubject()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, ActionRouter(surface.Registry), provider);

        await RunAsync(loop, "what's your status");
        await RunAsync(loop, ActionPhrase);
        await RunAsync(loop, "no, I meant the other way");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains($"The last of those — \"{ActionPhrase}\"", carried, StringComparison.Ordinal);
        Assert.Contains("what's your status", carried, StringComparison.Ordinal);
    }

    /// <summary>
    /// The naming sentence is committed to history along with the message it is composed into, so it is
    /// still being read turns later.
    /// </summary>
    [Fact]
    public async Task TheNamedSubjectDoesNotKeepAssertingItselfOnLaterTurns()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, ActionRouter(surface.Registry), provider);

        await RunAsync(loop, ActionPhrase);
        await RunAsync(loop, "no, I meant the other way");
        await RunAsync(loop, "how far is Sol?");

        var carried = string.Join(
            '\n',
            provider.LastRequest!.Prompt.History.Select(Text));

        // Still there, because history is written once and never edited.
        Assert.Contains(ActionPhrase, carried, StringComparison.Ordinal);
        Assert.DoesNotContain("is the current subject", carried, StringComparison.Ordinal);
        Assert.DoesNotContain("says below is a follow-up", carried, StringComparison.Ordinal);
        Assert.Contains(
            "the words that follow in this message",
            carried,
            StringComparison.Ordinal);
    }

    /// <summary>The carried-lines cap is per kind.</summary>
    [Fact]
    public async Task AmbientLinesCannotEvictTheExchangeBeforeTheModelReadsIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, ActionRouter(surface.Registry), provider);

        await RunAsync(loop, ActionPhrase);

        for (var line = 1; line <= 20; line++)
        {
            loop.Said($"Ambient line {line}.");
        }

        await RunAsync(loop, "no, I meant the other way");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains(ActionPhrase, carried, StringComparison.Ordinal);

        // And the ambient half is still capped, so a quiet evening does not pay for a loud one.
        Assert.DoesNotContain("Ambient line 1.", carried, StringComparison.Ordinal);
        Assert.Contains("Ambient line 20.", carried, StringComparison.Ordinal);
    }

    /// <summary>
    /// A carried answer is dated, not left standing as a fact — found driving this very change,
    /// 2026-09-08.
    /// </summary>
    [Fact]
    public async Task ACarriedAnswerIsMarkedAsPastRatherThanStanding()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, surface.Router, provider);

        var (routed, _) = await RunAsync(loop, "what's your status");
        Assert.Equal(TurnRoute.KeywordRouter, routed.Route);

        await RunAsync(loop, "no, the other one");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains("what was true when you said it", carried, StringComparison.Ordinal);
        Assert.Contains("is what is true now and replaces anything", carried, StringComparison.Ordinal);

        // The refusal is named, because it is the shape that bit: a tool's own "no" describes one attempt,
        // and the model read it as a description of d47.
        Assert.Contains("including a refusal", carried, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half of the fix, and the one that keeps the blast radius on model-free turns: a line
    /// d47 spoke unprompted is still described as unprompted, in today's wording, and a block holding
    /// nothing else is byte-identical to the one that shipped.
    /// </summary>
    [Fact]
    public async Task AnAmbientLineIsStillDescribedAsUnprompted()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, surface.Router, provider);

        loop.Said("Commander. Elvira Martuuk is one stop away.");

        await RunAsync(loop, "why would I care about that?");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Equal(
            "<said-aloud>\nSince the last exchange you spoke these lines to the Commander without "
            + "being asked. They are your own words, and the Commander heard them.\n"
            + "- Commander. Elvira Martuuk is one stop away.\n</said-aloud>\n\n"
            + "why would I care about that?",
            carried);
    }

    /// <summary>Both kinds in one block.</summary>
    [Fact]
    public async Task AnUnpromptedLineAndAnAskedForOneAreLabelledApart()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("Answered.");
        var loop = Build(surface, surface.Router, provider);

        loop.Said("Commander. Elvira Martuuk is one stop away.");
        await RunAsync(loop, "what's your status");

        await RunAsync(loop, "and now?");

        var carried = Text(provider.LastRequest!.Prompt.History[0]);

        Assert.Contains("without being asked", carried, StringComparison.Ordinal);
        Assert.Contains("Elvira Martuuk", carried, StringComparison.Ordinal);
        Assert.Contains("The Commander said \"what's your status\"", carried, StringComparison.Ordinal);
    }

    /// <summary>
    /// A router pointed at a real builtin tool by a phrase the Commander could have named, which is the
    /// shape a macro takes.
    /// </summary>
    private static KeywordRouter ActionRouter(CapabilityRegistry registry) =>
        new(
            registry,
            () =>
            [
                new DynamicCommand(
                    ActionPhrase,
                    "diagnostics",
                    "get_app_status",
                    new Dictionary<string, string>(StringComparer.Ordinal)),
            ]);

    private static TurnLoop Build(
        TestSurface surface,
        KeywordRouter router,
        ILlmProvider provider,
        SettingsService? settings = null)
    {
        var loop = new TurnLoop(
            surface.Registry,
            router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            settings: settings,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<(TurnResult Result, string Text)> RunAsync(TurnLoop loop, string input)
    {
        var text = new System.Text.StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.TextDelta delta:
                    text.Append(delta.Text);
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        return (result, text.ToString());
    }

    private static string Text(ConversationMessage message) =>
        string.Join(
            ' ',
            message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));
}
