using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class AFailedStoryLogsWhichStepFailedAndWhyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string UnclosedLines = """{"name": "The Broker's Ledger", "objectives": [{"objective": 1, "title": "Part One" """;

    [Fact]
    public async Task AFailedLinesCallNamesTheStepTheModelAndTheProvidersMessage()
    {
        var provider = new RoundScriptedLlmProvider([new LlmStreamEvent.Failed("max_tokens: must be at most 8192", Transient: false)]);
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.True(outcome.Internal);
        var warning = SingleWarning(log);
        Assert.Contains("lines step", warning);
        Assert.Contains("claude-opus-5", warning);
        Assert.Contains("Failed: max_tokens: must be at most 8192", warning);
        Assert.Matches(@"\d+\.\ds since the write began", warning);
    }

    [Fact]
    public async Task AFailedLineRepairCallNamesTheRepairStepNotTheLines()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives, (number, objective) =>
            {
                if (number == 1)
                {
                    objective["lines"]![0]!["speaker"] = "nobody";
                }
            })),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections),
            [new LlmStreamEvent.Failed("The server is overloaded", Transient: false)]);
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.True(outcome.Internal);
        var warning = SingleWarning(log);
        Assert.Contains("line repairs step", warning);
        Assert.Contains("The server is overloaded", warning);
    }

    [Fact]
    public async Task ALinesReplyWithAnUnclosedObjectLogsHowManyCharactersCameBack()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(UnclosedLines));
        var log = new RecordingLogger();

        var outcome = await Write(provider, log);

        Assert.True(outcome.Internal);
        var warning = SingleWarning(log);
        Assert.Contains("lines step", warning);
        Assert.Contains($"{UnclosedLines.Trim().Length} characters came back and could not be read", warning);
    }

    private static async Task<AdventureOutcome> Write(ILlmProvider provider, ILogger log)
    {
        var state = PlannedChapterWorld.State();
        var galaxy = new PlannedChapterWorld.Galaxy();
        var generator = new AdventureGenerator(() => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, log);

        return await generator.GenerateAsync(PlannedChapterWorld.Ask(), Now, CancellationToken.None);
    }

    private static string SingleWarning(RecordingLogger log) =>
        Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning).Message;
}
