using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using UnlockEvidence = D47.Core.Engineers.UnlockEvidence;

namespace D47.Core.Tests.Engineers;

public class AnOnFootTributeIsReadFromTheShipLockerTests
{
    private static readonly Engineer Domino = EngineerDirectory.ByName("Domino Green")!;

    private static SuitInventory Carrying(int push) => new()
    {
        ShipLocker = [new SuitItem("Push", "Item", push)],
        ShipLockerReadAt = DateTimeOffset.UnixEpoch,
    };

    private static UnlockEvidence Evidence(SuitInventory suit, bool unlocked = false)
    {
        var progress = unlocked
            ? EngineerProgressState.Empty.Apply(Event(
                $$"""{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Domino Green","EngineerID":{{Domino.Id}},"Progress":"Unlocked","Rank":1}]}"""))
            : null;

        return new UnlockEvidence(progress, null, null, null, null, null, suit);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static UnlockCriterion Tribute(UnlockEvidence evidence) =>
        EngineerAccess.CriteriaFor(Domino, evidence).Single(line => line.Text == Domino.Unlock);

    [Fact]
    public void TheTributeIsALockerTest()
    {
        Assert.Equal(new UnlockTest.Locker("push", 5), Domino.UnlockTest);
    }

    [Fact]
    public void ThreeOfFiveReadsAsCarryingThree()
    {
        var line = Tribute(Evidence(Carrying(3)));

        Assert.Null(line.Met);
        Assert.Equal("carrying 3 of 5", line.Reading);
        Assert.Equal(new UnlockMeasure(3, 5, false), line.Measure);
        Assert.False(EngineerAccess.HandOverHeld(Domino, Evidence(Carrying(3)), null, null));
    }

    [Fact]
    public void FiveOfFiveIsStillUndecidedButHeld()
    {
        var evidence = Evidence(Carrying(5));

        Assert.Null(Tribute(evidence).Met);
        Assert.True(EngineerAccess.HandOverHeld(Domino, evidence, null, null));
    }

    [Fact]
    public void AnUnlockedEngineersTributeIsMet()
    {
        Assert.True(Tribute(Evidence(Carrying(0), unlocked: true)).Met);
    }
}
