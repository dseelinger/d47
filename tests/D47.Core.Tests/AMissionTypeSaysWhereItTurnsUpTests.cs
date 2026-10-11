using System.Text.Json;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

public sealed class AMissionTypeSaysWhereItTurnsUpTests
{
    private static readonly DateTimeOffset At = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoTypeListsEveryGroupWithItsTypes()
    {
        var said = MissionTypes.Describe(null, null);

        Assert.Contains("Operations: ", said, StringComparison.Ordinal);
        Assert.Contains("Transportation: ", said, StringComparison.Ordinal);
        Assert.Contains("planetary scan", said, StringComparison.Ordinal);
        Assert.Contains("courier", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("Disable")]
    [InlineData("Mission_Disable")]
    public void ADisableMissionIsFoundBySpokenWordOrInternalName(string type)
    {
        var said = MissionTypes.Describe(type, null);

        Assert.Contains("Operations group", said, StringComparison.Ordinal);
        Assert.Contains("civil war", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lockdown", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("High Tech", said, StringComparison.Ordinal);
        Assert.Contains("likeliest", said, StringComparison.Ordinal);
    }

    [Fact]
    public void ASpokenTypeOfTwoWordsIsFound()
    {
        Assert.Contains("Planetary scan missions", MissionTypes.Describe("planetary scan", null), StringComparison.Ordinal);
    }

    [Fact]
    public void ACourierMissionNamesTheNavyRankAndSaysWhetherTheCommanderHoldsIt()
    {
        var holding = RankState.Empty.Apply(Event("\"Empire\":0,\"Federation\":3"));
        var said = MissionTypes.Describe("courier", holding);

        Assert.Contains("Some need a Federation navy rank. You hold ", said, StringComparison.Ordinal);
        Assert.Contains("Some need an Empire navy rank. You hold no Empire rank.", said, StringComparison.Ordinal);
        Assert.Contains("engineer bases", said, StringComparison.Ordinal);
    }

    [Fact]
    public void RanksNotYetSeenAreSaidToBeUnknown()
    {
        Assert.Contains("I do not know your ranks yet.", MissionTypes.Describe("courier", RankState.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTypeIsAnsweredWithTheNearestNames()
    {
        var said = MissionTypes.Describe("disabel", null);

        Assert.StartsWith("I do not know a mission type called disabel. The nearest are ", said, StringComparison.Ordinal);
        Assert.Contains("disable", said, StringComparison.Ordinal);
    }

    private static JournalEvent Event(string fields)
    {
        var text = $"{{ \"timestamp\":\"{At.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"Rank\", {fields} }}";

        return new JournalEvent(At, "Rank", JsonDocument.Parse(text).RootElement);
    }
}
