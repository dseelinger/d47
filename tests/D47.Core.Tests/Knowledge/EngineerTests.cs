using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The engineer directory and the Commander's standing with it. Like the specification tests,
/// these run against the real shipped table: a table is only worth having if it is right.
/// </summary>
public class EngineerTests
{
    [Fact]
    public void EveryEngineerHasSomewhereToBeFound()
    {
        Assert.True(EngineerDirectory.All.Count > 30, $"{EngineerDirectory.All.Count} engineers");

        // The whole point of resolving the two ids at generation time. An engineer with no
        // location is a row that cannot answer the question it exists for.
        Assert.All(EngineerDirectory.All, engineer => Assert.NotNull(engineer.System));
    }

    [Fact]
    public void AnEngineerIsFoundBySurnameAndDespiteANicknameInTheMiddle()
    {
        Assert.Equal("Felicity Farseer", EngineerDirectory.ByName("Farseer")?.Name);

        // The id list writes "Tod 'The Blaster' McQuinn" and the blueprint list writes "Tod
        // McQuinn". Without the join that normalises them, his entire speciality list is lost —
        // which reads as an engineer who grades nothing.
        var mcQuinn = EngineerDirectory.ByName("Tod McQuinn");

        Assert.NotNull(mcQuinn);
        Assert.NotEmpty(mcQuinn.Specialities);
    }

    [Fact]
    public void TheEngineerEverybodyStartsWithIsWhereAndWhatSheShouldBe()
    {
        var farseer = EngineerDirectory.ByName("Felicity Farseer");

        Assert.NotNull(farseer);
        Assert.Equal("Deciat", farseer.System);
        Assert.Equal(5, farseer.Specialities.First(s => s.Kind == "Frame Shift Drive").MaxGrade);
        Assert.NotNull(farseer.UnlockCost);
    }

    [Fact]
    public void WhoGradesSomethingIsAnsweredBestGradeFirst()
    {
        var grading = EngineerDirectory.Grading("frame shift drive");

        Assert.NotEmpty(grading);
        Assert.Equal(5, grading[0].Speciality.MaxGrade);

        // The grade is what decides who is worth flying to, so it orders the answer.
        Assert.Equal(
            grading.Select(match => match.Speciality.MaxGrade),
            grading.Select(match => match.Speciality.MaxGrade).OrderDescending());
    }

    [Fact]
    public void AModificationNobodyOffersIsNotAnEmptyList()
    {
        Assert.Empty(EngineerDirectory.Grading("Warp Core"));
        Assert.Empty(EngineerDirectory.Grading(""));
    }

    // ---- The Commander's own standing ------------------------------------------------------

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private const string Snapshot =
        """
        {"timestamp":"3311-01-01T00:01:00Z","event":"EngineerProgress","Engineers":[
          {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","RankProgress":40,"Rank":5},
          {"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Invited"},
          {"Engineer":"The Dweller","EngineerID":300180,"Progress":"Known"}]}
        """;

    private static EngineerProgressState Fold(params string[] lines)
    {
        var state = EngineerProgressState.Empty;

        foreach (var line in lines)
        {
            state = state.Apply(Event(line));
        }

        return state;
    }

    [Fact]
    public void TheStartupSnapshotEstablishesEverybodyAtOnce()
    {
        var state = Fold(Snapshot);

        Assert.True(state.IsKnown);
        Assert.Single(state.Unlocked);
        Assert.Single(state.Invited);
        Assert.Equal(5, state.For(300100)?.Rank);
    }

    [Fact]
    public void ASingleEngineerEventMergesRatherThanReplacingTheRest()
    {
        // The two shapes mean different things. Treating this one as a snapshot would wipe the
        // other thirty-seven the first time somebody ranked up.
        var state = Fold(
            Snapshot,
            """{"timestamp":"3311-01-01T02:00:00Z","event":"EngineerProgress","Engineer":"The Dweller","EngineerID":300180,"Progress":"Unlocked","Rank":3}""");

        Assert.Equal(3, state.Standings.Count);
        Assert.Equal(2, state.Unlocked.Count);
        Assert.Equal(3, state.For(300180)?.Rank);
        Assert.Equal(5, state.For(300100)?.Rank);
    }

    [Fact]
    public void AFreshSnapshotReplacesWhateverHadAccumulated()
    {
        var state = Fold(
            Snapshot,
            """{"timestamp":"3311-01-02T00:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""");

        Assert.Single(state.Standings);
    }

    [Fact]
    public void NoEngineerProgressEventMeansNotSeenRatherThanNobodyUnlocked()
    {
        Assert.False(EngineerProgressState.Empty.IsKnown);
    }

    // ---- What the model sees ---------------------------------------------------------------

    private static CapabilityRegistry Registry(bool withProgress = true)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        if (withProgress)
        {
            gameState.Apply(Event(Snapshot));
        }

        return CapabilityRegistry.Build([EngineerCapability.Create(() => gameState.Active)]);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task ProgressSeparatesUnlockedFromInvitedFromNeverMet()
    {
        var result = await Registry().InvokeAsync(
            "get_engineer_progress",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("1 engineer unlocked of", result.Content, StringComparison.Ordinal);

        // "Invited and not yet unlocked" is the chain of unlocks as observed rather than
        // asserted, which is the only form d47 has a source for.
        Assert.Contains("Invited and not yet unlocked: Elvira Martuuk", result.Content, StringComparison.Ordinal);
        Assert.Contains("Heard of, no invitation yet: The Dweller", result.Content, StringComparison.Ordinal);
        Assert.Contains("Not met at all:", result.Content, StringComparison.Ordinal);

        // The four buckets have to account for everybody. Without the third line The Dweller
        // belonged to none of them and vanished from a report whose whole job is to add up.
        Assert.Equal(
            EngineerDirectory.All.Count,
            EngineerDirectory.All.Count(engineer =>
                result.Content.Contains(engineer.Name, StringComparison.Ordinal)));

        // The directory supplies where they are; the journal only knows the name and the rank.
        Assert.Contains("Deciat", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoProgressEventItNamesWhatItIsWaitingFor()
    {
        var result = await Registry(withProgress: false).InvokeAsync(
            "get_engineer_progress",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.Contains("when you enter the game", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookingUpAnEngineerSaysWhereWhatAndHowFarAlong()
    {
        var result = await Registry().InvokeAsync(
            "find_engineer",
            Args(("engineer", "Farseer")),
            TestContext.Current.CancellationToken);

        Assert.Contains("Deciat", result.Content, StringComparison.Ordinal);
        Assert.Contains("Frame Shift Drive to 5", result.Content, StringComparison.Ordinal);
        Assert.Contains("invitation asks for", result.Content, StringComparison.Ordinal);
        Assert.Contains("unlocked at grade 5", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhoGradesSomethingCarriesTheCommandersOwnStandingBesideEachOne()
    {
        // "Who grades this" is nearly always asked as "who can grade this for me", and the two
        // answers can differ completely.
        var result = await Registry().InvokeAsync(
            "find_engineer",
            Args(("grades", "frame shift drive")),
            TestContext.Current.CancellationToken);

        Assert.Contains("Felicity Farseer — to grade 5", result.Content, StringComparison.Ordinal);
        Assert.Contains("unlocked at grade 5", result.Content, StringComparison.Ordinal);
        Assert.Contains("not met", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEngineerNobodyHasHeardOfGetsSuggestions()
    {
        var result = await Registry().InvokeAsync(
            "find_engineer",
            Args(("engineer", "Felicty Farsear")),
            TestContext.Current.CancellationToken);

        Assert.Contains("Did you mean", result.Content, StringComparison.Ordinal);
        Assert.Contains("Farseer", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskingForNeitherIsRefusedRatherThanAnswered()
    {
        var result = await Registry().InvokeAsync(
            "find_engineer",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.Contains("Name an engineer", result.Content, StringComparison.Ordinal);
    }
}
