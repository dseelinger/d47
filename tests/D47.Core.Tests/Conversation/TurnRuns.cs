using System.Text;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Runs one typed turn to its end.</summary>
public static class TurnRuns
{
    /// <summary>The result of the turn.</summary>
    public static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        var (result, _) = await RunWithTextAsync(loop, input);
        return result;
    }

    /// <summary>The text the turn spoke.</summary>
    public static async Task<string> SaidAsync(TurnLoop loop, string input)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var text = new StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
        }

        return text.ToString();
    }

    /// <summary>The result of the turn and the text it spoke.</summary>
    public static async Task<(TurnResult Result, string Spoken)> RunWithTextAsync(TurnLoop loop, string input)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var spoken = new StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.TextDelta delta:
                    spoken.Append(delta.Text);
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        return (result, spoken.ToString());
    }
}

/// <summary>A turn loop wired for tests: one attempt, an instant clock, a model only when a provider is given.</summary>
public static class TurnLoops
{
    public static TurnLoop Build(
        CapabilityRegistry registry,
        ILlmProvider? provider,
        KeywordRouter? router = null,
        SettingsService? settings = null,
        OfferWindow? offers = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var loop = new TurnLoop(
            registry,
            router ?? new KeywordRouter(registry),
            new LlmAvailabilityState(provider is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            settings: settings,
            clock: new InstantClock(),
            offers: offers);

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }
}
