using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class BgsWorkKeepsTheFactionsInEachSystemTests
{
    private static BgsWork Fold(BgsWork start, string time, long address, params string[] factions)
    {
        var list = string.Join(",", factions.Select(name => "{\"Name\":\"" + name + "\",\"Influence\":0.5}"));
        var json = "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"S" + address
            + "\",\"SystemAddress\":" + address + ",\"Factions\":[" + list + "]}";
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return start.Apply(parsed!);
    }

    [Fact]
    public void AJumpRecordsTheFactionsPresent()
    {
        var work = Fold(BgsWork.None, "2026-10-10T10:00:00Z", 1, "Alpha Mining", "Beta Union");

        Assert.Equal(["Alpha Mining", "Beta Union"], work.FactionsIn(1));
    }

    [Fact]
    public void ALaterJumpToTheSameSystemReplacesThem()
    {
        var work = Fold(BgsWork.None, "2026-10-10T10:00:00Z", 1, "Alpha Mining", "Beta Union");
        work = Fold(work, "2026-10-11T10:00:00Z", 1, "Gamma Party");

        Assert.Equal(["Gamma Party"], work.FactionsIn(1));
    }

    [Fact]
    public void TheLiveListWinsWhenLaidOverTheRecoveredOne()
    {
        var recovered = Fold(BgsWork.None, "2026-10-10T10:00:00Z", 1, "Alpha Mining");
        recovered = Fold(recovered, "2026-10-10T11:00:00Z", 2, "Delta Pirates");
        var live = Fold(BgsWork.None, "2026-10-11T10:00:00Z", 1, "Gamma Party");

        var merged = recovered.With(live);

        Assert.Equal(["Gamma Party"], merged.FactionsIn(1));
        Assert.Equal(["Delta Pirates"], merged.FactionsIn(2));
    }

    [Fact]
    public void ASystemNeverSeenAnswersAnEmptyList()
    {
        Assert.Empty(Fold(BgsWork.None, "2026-10-10T10:00:00Z", 1, "Alpha Mining").FactionsIn(9));
    }
}
