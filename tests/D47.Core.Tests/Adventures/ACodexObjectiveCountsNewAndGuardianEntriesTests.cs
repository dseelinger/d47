using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class ACodexObjectiveCountsNewAndGuardianEntriesTests
{
    private static JournalEvent Entry(string category, string? subCategory, bool isNew) => Event(
        $$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"CodexEntry", "Category":"{{category}}", "SubCategory":"{{subCategory}}", "IsNewEntry":{{(isNew ? "true" : "false")}} }""");

    private static AdventureTrigger Codex(string? filter, bool? isNew = null) =>
        new() { Kind = TriggerKind.Codex, Count = 3, Filter = filter, New = isNew };

    private static readonly JournalEvent Beacon = Entry("$Codex_Category_Civilisations;", "$Codex_SubCategory_Guardian;", true);

    [Theory]
    [InlineData("Civilisations")]
    [InlineData("Guardian")]
    [InlineData(null)]
    public void AGuardianBeaconEntryCountsTowardCivilisationsGuardianAndAny(string? filter) =>
        Assert.True(AdventureFold.Matches(Codex(filter), Beacon));

    [Fact]
    public void AGuardianBeaconEntryCountsTowardNewEntries() =>
        Assert.True(AdventureFold.Matches(Codex(null, true), Beacon));

    [Fact]
    public void AnEntryLoggedBeforeDoesNotCountTowardNewEntries()
    {
        var seen = Entry("$Codex_Category_Civilisations;", "$Codex_SubCategory_Guardian;", false);

        Assert.False(AdventureFold.Matches(Codex(null, true), seen));
        Assert.True(AdventureFold.Matches(Codex(null, false), seen));
        Assert.True(AdventureFold.Matches(Codex("Guardian"), seen));
    }

    [Fact]
    public void ABiologyEntryDoesNotCountTowardGuardian() =>
        Assert.False(AdventureFold.Matches(Codex("Guardian"), Entry("$Codex_Category_Biology;", "$Codex_SubCategory_Organic_Structures;", true)));

    [Theory]
    [InlineData("Biology", false)]
    [InlineData("Guardian", false)]
    [InlineData("Thargoid", true)]
    [InlineData("Geology", true)]
    public void OnlyTheOfferedFiltersPassValidation(string filter, bool refused) =>
        Assert.Equal(refused, AdventureValidation.CountedProblems("beat 1", Codex(filter)).Any());

    [Fact]
    public void TheObjectiveSaysWhatItWaitsFor()
    {
        Assert.Equal("log 3 new guardian codex entries", Codex("Guardian", true).Describe());
        Assert.Equal("New guardian codex entries: 1 of 3", Codex("Guardian", true).Progress(1));
        Assert.Equal("log 3 codex entries", Codex(null).Describe());
    }
}
