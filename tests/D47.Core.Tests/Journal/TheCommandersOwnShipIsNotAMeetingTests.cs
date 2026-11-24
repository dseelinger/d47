using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCommandersOwnShipIsNotAMeetingTests
{
    private static string Line(string time, string kind, string rest = "") =>
        "{\"timestamp\":\"2026-10-01T" + time + "Z\",\"event\":\"" + kind + "\"" + rest + "}";

    private static string Commander(string time, string fid, string name) =>
        Line(time, "Commander", ",\"FID\":\"" + fid + "\",\"Name\":\"" + name + "\"");

    private static string Load(string time, string fid, string name) =>
        Line(time, "LoadGame", ",\"FID\":\"" + fid + "\",\"Commander\":\"" + name + "\"");

    private static string Unmanned(string time, string name) =>
        Line(time, "ShipTargeted", ",\"TargetLocked\":true,\"Ship_Localised\":\"Caspian Explorer\",\"PilotName\":\"$RolePanel2_unmanned; $cmdr_decorate:#name="
            + name + ";\",\"PilotName_Localised\":\"unmanned CMDR " + name + "\"");

    private static string Manned(string time, string name) =>
        Line(time, "ShipTargeted", ",\"TargetLocked\":true,\"Ship_Localised\":\"Python\",\"PilotName\":\"$cmdr_decorate:#name="
            + name + ";\",\"PilotName_Localised\":\"CMDR " + name + "\"");

    private static CommanderLog Folded(params string[] lines)
    {
        var log = new CommanderLog(new MemoryFileSystem(), NullLogger.Instance);
        log.Fold(lines);
        return log;
    }

    [Fact]
    public void TargetingYourOwnParkedShipFromTheSrvFilesNothing()
    {
        var log = Folded(
            Commander("10:00:00", "F1", "Orla Venn"),
            Load("10:00:01", "F1", "Orla Venn"),
            Unmanned("10:05:00", "Orla Venn"));

        Assert.Empty(log.Encounters("F1", "Orla Venn"));
        Assert.Empty(log.Met("F1"));
        Assert.Null(log.LastTargeted());
    }

    [Fact]
    public void YourOwnNameIsMatchedWithoutCaseOrPrefix()
    {
        var log = Folded(
            Load("10:00:00", "F1", "Orla Venn"),
            Unmanned("10:05:00", "ORLA VENN"),
            Line("10:06:00", "ReceiveText", ",\"From\":\"CMDR Orla Venn\",\"Message\":\"x\",\"Channel\":\"player\""));

        Assert.Empty(log.Met("F1"));
    }

    [Fact]
    public void AnotherCommandersShipIsStillAMeeting()
    {
        var log = Folded(
            Commander("10:00:00", "F1", "Orla Venn"),
            Load("10:00:01", "F1", "Orla Venn"),
            Manned("10:05:00", "Tamsin Rook"),
            Unmanned("10:06:00", "Orla Venn"));

        Assert.Equal("Tamsin Rook", Assert.Single(log.Met("F1")).Name);
        Assert.Equal("Tamsin Rook", log.LastTargeted()?.Name);
    }

    [Fact]
    public void OneCommandersShipIsAMeetingForAnotherCommanderOnTheSameMachine()
    {
        var log = Folded(
            Load("10:00:00", "F1", "Orla Venn"),
            Unmanned("10:05:00", "Orla Venn"),
            Load("11:00:00", "F2", "Tamsin Rook"),
            Manned("11:05:00", "Orla Venn"));

        Assert.Empty(log.Met("F1"));
        Assert.Equal("Orla Venn", Assert.Single(log.Met("F2")).Name);
    }
}
