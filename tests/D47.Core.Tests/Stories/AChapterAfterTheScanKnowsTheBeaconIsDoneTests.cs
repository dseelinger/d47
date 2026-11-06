using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter written after the beacon scan is told, in the call that writes its words, that the beacon trip is over.</summary>
public sealed class AChapterAfterTheScanKnowsTheBeaconIsDoneTests
{
    private const string Done = "The Guardian beacon scan has already happened, and the Guardian cores came aboard with it.";

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static async Task<IReadOnlyList<string>> Prompts(int? daysSinceBeacon)
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(8)));
        var ask = PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { DaysSinceBeacon = daysSinceBeacon });

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        return [.. provider.Requests.Select(request => request.Prompt.History[0].Text).Where(prompt => !prompt.StartsWith("You are checking", StringComparison.Ordinal))];
    }

    [Fact]
    public async Task TheChaptersWriterIsToldTheScanHappened()
    {
        var prompt = Assert.Single(await Prompts(daysSinceBeacon: 0));

        Assert.Contains(Done, prompt);
    }

    [Fact]
    public async Task AChapterBeforeTheScanIsNotTold()
    {
        var prompts = await Prompts(daysSinceBeacon: null);

        Assert.All(prompts, prompt => Assert.DoesNotContain(Done, prompt));
    }
}
