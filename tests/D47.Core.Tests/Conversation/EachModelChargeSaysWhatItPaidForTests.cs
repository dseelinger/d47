using System.Reflection;
using D47.Core.Conversation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Each model row in the spend ledger records what the call was for (#2552).</summary>
public class EachModelChargeSaysWhatItPaidForTests
{
    private const string Path = @"C:\d47\data\spend.jsonl";

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly SpendPeriod Today = new("today", Now.AddHours(-12), Now.AddHours(12));

    private sealed class StoppedClock : IWallClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private readonly MemoryFileSystem _files = new();

    private SpendLedger Ledger() => new(Path, _files, new StoppedClock(), NullLogger.Instance);

    private static string Row(string? purpose) =>
        "{\"at\":\"2026-11-17T11:00:00+00:00\",\"kind\":\"Model\",\"providerId\":\"anthropic\",\"model\":\"claude-opus-5\","
        + "\"dollars\":0.25,\"priced\":true,\"inputTokens\":1000"
        + (purpose is null ? string.Empty : $",\"purpose\":\"{purpose}\"")
        + "}\n";

    [Fact]
    public void ARowKeepsItsPurposeAndStepWhenReadBack()
    {
        Ledger().Append(new SpendEntry
        {
            At = Now.AddHours(-1),
            Kind = SpendKind.Model,
            ProviderId = "anthropic",
            Model = "claude-opus-5",
            Dollars = 0.25m,
            Priced = true,
            Purpose = SpendPurposes.StoryChapter,
            Step = "fidelity",
        });

        var row = Assert.Single(SpendFile.Entries(_files, Path));

        Assert.Equal(SpendPurposes.StoryChapter, row.Purpose);
        Assert.Equal("fidelity", row.Step);
        Assert.Equal(0.25m, Ledger().Total(Today).ModelDollars);
    }

    [Fact]
    public void ARowWrittenWithoutAPurposeReadsBackNullAndStillCounts()
    {
        _files.WriteText(Path, Row(purpose: null));

        var totals = Ledger().Total(Today);

        Assert.Null(Assert.Single(SpendFile.Entries(_files, Path)).Purpose);
        Assert.Equal(0.25m, totals.ModelDollars);
        Assert.Equal(1, totals.Turns);
        Assert.True(totals.Complete);
    }

    [Fact]
    public void ARowWhosePurposeThisBuildDoesNotListIsReadAndCounted()
    {
        _files.WriteText(Path, Row("galaxy-weather") + Row(SpendPurposes.Conversation));

        var totals = Ledger().Total(Today);

        Assert.Equal(0.50m, totals.ModelDollars);
        Assert.Equal(2, totals.Turns);
        Assert.Equal("galaxy-weather", SpendPurposes.NameOf("galaxy-weather"));
    }

    [Fact]
    public void ARowWithNoPurposeIsNamedNotRecorded()
    {
        Assert.Equal("Not recorded", SpendPurposes.NameOf(null));
        Assert.Equal("Story chapters", SpendPurposes.NameOf(SpendPurposes.StoryChapter));
    }

    [Fact]
    public void EveryPurposeIdIsUniqueAndNamed()
    {
        Assert.Equal(SpendPurposes.All.Count, SpendPurposes.All.Select(purpose => purpose.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(SpendPurposes.All, purpose =>
        {
            Assert.False(string.IsNullOrWhiteSpace(purpose.Id));
            Assert.False(string.IsNullOrWhiteSpace(purpose.Name));
        });

        var constants = typeof(SpendPurposes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        Assert.Equal(constants.Order(StringComparer.Ordinal), SpendPurposes.All.Select(purpose => purpose.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AFlavourLineRecordsThePurposeAndStepItWasGiven()
    {
        var spend = new SpendTracker(Ledger());

        await FlavourTurn.AskForAsync(
            FakeLlmProvider.Answering("A detour.", new LlmUsage(1000, 50, 0, 0)),
            model: null,
            persona: null,
            aboutMe: null,
            instruction: "Propose a detour.",
            gameState: null,
            spend,
            PriceTable.Default,
            logger: null,
            purpose: SpendPurposes.StoryDetour,
            TestContext.Current.CancellationToken,
            step: "heading");

        var row = Assert.Single(SpendFile.Entries(_files, Path));

        Assert.Equal(SpendPurposes.StoryDetour, row.Purpose);
        Assert.Equal("heading", row.Step);
    }

    [Fact]
    public async Task ACommandersTurnRecordsConversation()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(Ledger()),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            FakeLlmProvider.Answering("Hyperspace is folded space.", new LlmUsage(1000, 50, 0, 0)));

        await TurnRuns.RunAsync(loop, "tell me about hyperspace physics");

        var row = Assert.Single(SpendFile.Entries(_files, Path));

        Assert.Equal(SpendPurposes.Conversation, row.Purpose);
        Assert.Null(row.Step);
    }

    /// <summary>A recording seam with an overload that takes no purpose would let a call site record nothing.</summary>
    [Fact]
    public void NoRecordingSeamCanBeCalledWithoutAPurpose()
    {
        var seams = typeof(SpendTracker).GetMethods().Where(method => method.Name == nameof(SpendTracker.Record))
            .Concat(typeof(FlavourTurn).GetMethods().Where(method => method.Name is nameof(FlavourTurn.AskAsync) or nameof(FlavourTurn.AskForAsync)))
            .ToList();

        Assert.Equal(3, seams.Count);
        Assert.All(seams, method =>
        {
            var purpose = Assert.Single(method.GetParameters(), parameter => parameter.Name == "purpose");

            Assert.Equal(typeof(string), purpose.ParameterType);
            Assert.False(purpose.IsOptional, $"{method.DeclaringType!.Name}.{method.Name} gives the purpose a default.");
        });
    }
}
