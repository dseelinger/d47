using System.Text.Json;
using D47.Core.Tests;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

public sealed class ADraftNotWrittenListsEachRoundsRefusalsTests
{
    private static (string?, string?, string?) Read(JsonElement round) =>
        (round.GetProperty("round").GetString(), round.GetProperty("step").GetString(), round.GetProperty("refusal").GetString());

    [Fact]
    public void EveryLoggedRefusalIsListedInOrderWithItsRoundAndStep()
    {
        var log = new RecordingLogger();
        log.LogInformation("Draft {Draft}, {Round} round, {Step} step refused: {Refusal}", 1, "first", "beats", "Line two is unspoken.");
        log.LogInformation("Something else {Value}", 3);
        log.LogInformation("Draft {Draft}, {Round} round, {Step} step refused: {Refusal}", 1, "repair 1", "replacements", "Line two\nis still unspoken.");

        var rounds = JsonSerializer.SerializeToElement(StoryComparisonTests.RefusalRounds(log)).EnumerateArray().ToList();

        Assert.Equal(2, rounds.Count);
        Assert.Equal(("first", "beats", "Line two is unspoken."), Read(rounds[0]));
        Assert.Equal(("repair 1", "replacements", "Line two\nis still unspoken."), Read(rounds[1]));
    }

    [Fact]
    public void EveryPlainSpineRoundIsListedWithItsObjections()
    {
        var log = new RecordingLogger();
        log.LogInformation("Draft {Draft}, plain spine round {Round}: {Objections}", 1, 1, "The goal is vague.");
        log.LogInformation("Draft {Draft}, plain spine round {Round}: {Objections}", 1, 2, "The goal is\nstill vague.");

        var rounds = JsonSerializer.SerializeToElement(StoryComparisonTests.RefusalRounds(log)).EnumerateArray().ToList();

        Assert.Equal(2, rounds.Count);
        Assert.Equal(("1", "plain", "The goal is vague."), Read(rounds[0]));
        Assert.Equal(("2", "plain", "The goal is\nstill vague."), Read(rounds[1]));
    }
}
