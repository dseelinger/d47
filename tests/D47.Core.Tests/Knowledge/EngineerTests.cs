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

    // ---- The chain, and where the Commander stands on it -------------------------------------

    /// <summary>
    /// Selene Jean at rank 2 — one short of the grade 3 her referral of Bill Turner needs, which
    /// is the case worth reporting: a path started and not finished.
    /// </summary>
    private static CapabilityRegistry OnThePath()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        gameState.Apply(Event(
            """
            {"timestamp":"3311-01-01T00:01:00Z","event":"EngineerProgress","Engineers":[
              {"Engineer":"Selene Jean","EngineerID":300210,"Progress":"Unlocked","Rank":2}]}
            """));

        return CapabilityRegistry.Build([EngineerCapability.Create(() => gameState.Active)]);
    }

    private static async Task<string> Ask(CapabilityRegistry registry, params (string Name, string Value)[] values)
    {
        var result = await registry.InvokeAsync("find_engineer", Args(values), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    [Fact]
    public async Task AReferredEngineerNamesWhoRefersThemAndAtWhatGrade()
    {
        var answer = await Ask(Registry(), ("engineer", "Bill Turner"));

        // Asserted from the table rather than inferred from the journal. Before the chain shipped
        // this could only be answered once the invitation had already arrived, which is the one
        // moment the Commander no longer needs telling.
        Assert.Contains("Reached through Selene Jean at grade 3.", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheGapOnThatPathIsPricedRatherThanLeftAsAGrade()
    {
        var answer = await Ask(OnThePath(), ("engineer", "Bill Turner"));

        Assert.Contains("The Commander is grade 2 with Selene Jean", answer, StringComparison.Ordinal);
        Assert.Contains("the referral needs grade 3", answer, StringComparison.Ordinal);

        // The published price of that rank, and the half of the requirement nobody published as a
        // number said in words rather than invented as one.
        Assert.Contains("compounds", answer, StringComparison.Ordinal);
        Assert.Contains("roughly half the bar", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReferralAlreadyEarnedIsSaidToBeEarned()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        gameState.Apply(Event(
            """
            {"timestamp":"3311-01-01T00:01:00Z","event":"EngineerProgress","Engineers":[
              {"Engineer":"Selene Jean","EngineerID":300210,"Progress":"Unlocked","Rank":4}]}
            """));

        var answer = await Ask(
            CapabilityRegistry.Build([EngineerCapability.Create(() => gameState.Active)]),
            ("engineer", "Bill Turner"));

        Assert.Contains("so that referral is earned", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("cr of profit", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThreeReferrersMeanAnyOfThemRatherThanAllOfThem()
    {
        var answer = await Ask(Registry(), ("engineer", "Yi Shen"));

        // Reading three referrers as three requirements would describe a wall where there is a
        // door.
        Assert.Contains("Reached through any of Baltanos, Eleanor Bresa or Rosa Dayette.", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOnFootReferralSaysNoGradeIsStatedRatherThanInventingOne()
    {
        var answer = await Ask(Registry(), ("engineer", "Yi Shen"));

        // Defaulting to the ship chain's grade 3 would state a requirement the game does not have:
        // the Odyssey engineers unlock on a count of modifications instead.
        Assert.Contains("No grade is stated for that referral", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("at grade 3", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEngineerNobodyHasToRecommendSaysSo()
    {
        var answer = await Ask(Registry(), ("engineer", "Farseer"));

        // The useful half of the answer for the eleven who need nobody. Silence here reads as a
        // missing referral rather than as an absent one.
        Assert.Contains("Nobody has to recommend them", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatEarnsTheInvitationIsSaidAsWellAsWhatItAsksFor()
    {
        var answer = await Ask(Registry(), ("engineer", "Bill Turner"));

        Assert.Contains("Earning the invitation: ", answer, StringComparison.Ordinal);
        Assert.Contains("Their invitation asks for Bromellite ×50.", answer, StringComparison.Ordinal);
        Assert.Contains("Reputation with them rises fastest by: ", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUngradedSpecialityIsNamedWithoutAGradeRatherThanAsGradeZero()
    {
        // Odyssey suit and weapon blueprints are ungraded in the game, and the table carries that
        // as a zero. Nine engineers read "Suit to 0" before this, which is a defect on the face of
        // it rather than a fact.
        //
        // Phase 20 then went further: an on-foot speciality is the **modification's name** rather
        // than the word "Suit". "Grades: Suit, Weapon" was true and said nothing — every one of the
        // thirteen said exactly that — where the names are what a Commander asks by, and what makes
        // the four Colonia engineers expressible at all.
        var answer = await Ask(Registry(), ("engineer", "Domino Green"));

        Assert.Contains("Enhanced tracking", answer, StringComparison.Ordinal);
        Assert.Contains("Extra backpack capacity", answer, StringComparison.Ordinal);
        Assert.DoesNotContain(" to 0", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhoGradesSomethingNamesThePathToTheOnesNotMet()
    {
        var answer = await Ask(Registry(), ("grades", "thrusters"));

        // A list of people the Commander cannot use becomes a list of paths they can start.
        Assert.Contains("not met, reached through Marco Qwent", answer, StringComparison.Ordinal);
    }
}
