using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AJournalIsReadALineAtATimeTests
{
    private const string Root = @"C:\d47-test\journals";

    private static readonly string[] Lines =
    [
        """{ "timestamp":"2026-02-10T09:10:00Z", "event":"FSDJump", "Taxi":false, "Multicrew":false, "StarSystem":"Fixture Nebula Point", "SystemAddress":1002, "StarPos":[-33.65625,72.46875,-20.65625], "SystemAllegiance":"Independent", "SystemEconomy":"$economy_Industrial;", "SystemEconomy_Localised":"Industrial", "Population":120000, "Body":"Fixture Nebula Point A", "BodyID":1, "BodyType":"Star", "JumpDist":18.42, "FuelUsed":2.7, "FuelLevel":28.3 }""",
        """{ "timestamp":"2026-02-10T09:15:00Z", "event":"Docked", "StationName":"Fixture Outpost", "StationType":"Coriolis", "Taxi":false, "Multicrew":false, "StarSystem":"Fixture Nebula Point", "SystemAddress":1002, "MarketID":3228000000, "StationFaction":{ "Name":"Fixture Union", "FactionState":"Boom" }, "DistFromStarLS":512.25 }""",
        """{ "timestamp":"2026-02-10T09:20:00Z", "event":"ReceiveText", "From":"$npc_name_decorate:#name=Fixture Pilot;", "From_Localised":"Fixture Pilot", "Message":"$Pirate_OnStartScanCargo07;", "Message_Localised":"Hold still while I look through your hold, Commander.", "Channel":"npc" }""",
        """{ "timestamp":"2026-02-10T09:25:00Z", "event":"Undocked", "StationName":"Fixture Outpost", "StationType":"Coriolis", "MarketID":3228000000, "Taxi":false, "Multicrew":false }""",
    ];

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void APartialLastLineIsCompletedOnTheNextPoll()
    {
        var path = Path.Combine(Root, "Journal.partial.log");
        _files.WriteText(path, Lines[0] + "\n" + Lines[1][..40]);
        var reader = new JournalReader(path, _files, NullLogger.Instance);

        Assert.Equal(["FSDJump"], reader.Poll().Select(e => e.Kind));
        Assert.Equal(Encoding.UTF8.GetByteCount(Lines[0] + "\n"), reader.Position);

        _files.AppendText(path, Lines[1][40..] + "\r\n");

        Assert.Equal(["Docked"], reader.Poll().Select(e => e.Kind));
        Assert.Equal(Encoding.UTF8.GetByteCount(Lines[0] + "\n" + Lines[1] + "\r\n"), reader.Position);
    }

    [Fact]
    public void AMultiByteCharacterBeforeALineEndIsCountedInBytes()
    {
        var path = Path.Combine(Root, "Journal.utf8.log");
        var first = """{ "timestamp":"2026-02-10T09:00:00Z", "event":"Location", "StarSystem":"Fixture Anchorage", "Body":"Fixture Anchorage é€😀" }""";
        _files.WriteText(path, first + "\n" + Lines[3] + "\n");
        var reader = new JournalReader(path, _files, NullLogger.Instance);

        var events = reader.Poll();

        Assert.Equal(["Location", "Undocked"], events.Select(e => e.Kind));
        Assert.Equal("Fixture Anchorage é€😀", events[0].String("Body"));
        Assert.Equal(Encoding.UTF8.GetByteCount(first + "\n" + Lines[3] + "\n"), reader.Position);
    }

    [Fact]
    public void AnInvalidByteInAValueIsReadAsAReplacementCharacter()
    {
        var path = Path.Combine(Root, "Journal.invalid.log");
        var before = Encoding.UTF8.GetBytes("""{ "timestamp":"2026-02-10T09:00:00Z", "event":"Location", "Body":"Fixture """);
        var after = Encoding.UTF8.GetBytes("\" }\n");
        _files.WriteBytes(path, [.. before, 0xFF, .. after]);
        var reader = new JournalReader(path, _files, NullLogger.Instance);

        var events = reader.Poll();

        Assert.Equal(["Location"], events.Select(e => e.Kind));
        Assert.Equal("Fixture �", events[0].String("Body"));
        Assert.Equal(before.Length + 1 + after.Length, reader.Position);
    }

    [Fact]
    public void ALineEndingExactlyAtTheLimitIsRead()
    {
        var path = Path.Combine(Root, "Journal.until.log");
        _files.WriteText(path, Lines[0] + "\n" + Lines[1] + "\n" + Lines[2] + "\n");
        var until = Encoding.UTF8.GetByteCount(Lines[0] + "\n" + Lines[1] + "\n");
        var reader = new JournalReader(path, _files, NullLogger.Instance, until);

        Assert.Equal(["FSDJump", "Docked"], reader.Poll().Select(e => e.Kind));
        Assert.Equal(until, reader.Position);
        Assert.Empty(reader.Poll());
    }

    [Fact]
    public void ALineLongerThanTheReadBufferIsParsedWhole()
    {
        var path = Path.Combine(Root, "Journal.long.log");
        var padding = new string('x', 200_000);
        var longLine = "{ \"timestamp\":\"2026-02-10T09:30:00Z\", \"event\":\"Loadout\", \"Padding\":\"" + padding + "\" }";
        _files.WriteText(path, Lines[0] + "\n" + longLine + "\n" + Lines[3] + "\n");
        var reader = new JournalReader(path, _files, NullLogger.Instance);

        var events = reader.Poll();

        Assert.Equal(["FSDJump", "Loadout", "Undocked"], events.Select(e => e.Kind));
        Assert.Equal(padding, events[1].String("Padding"));
        Assert.Equal(_files.Stat(path)!.Value.Length, reader.Position);
    }

    [Fact]
    public void ALargeJournalIsReadWithoutCopyingTheWholeFile()
    {
        var warm = Path.Combine(Root, "Journal.warm.log");
        _files.WriteText(warm, string.Join("\n", Lines) + "\n");
        _ = new JournalReader(warm, _files, NullLogger.Instance).Poll();

        var path = Path.Combine(Root, "Journal.large.log");
        var text = new StringBuilder();
        var count = 0;

        while (text.Length < 2_200_000)
        {
            text.Append(Lines[count++ % Lines.Length]).Append("\r\n");
        }

        _files.WriteText(path, text.ToString());
        var size = _files.Stat(path)!.Value.Length;
        var lines = Enumerable.Range(0, count).Select(i => Encoding.UTF8.GetBytes(Lines[i % Lines.Length])).ToArray();

        // The bound is on what Poll adds beyond the events it returns, which cost more than the file by themselves.
        var before = GC.GetAllocatedBytesForCurrentThread();

        foreach (var line in lines)
        {
            _ = JournalEvent.TryParse(line, NullLogger.Instance, out _);
        }

        var events = GC.GetAllocatedBytesForCurrentThread() - before;

        var reader = new JournalReader(path, _files, NullLogger.Instance);
        before = GC.GetAllocatedBytesForCurrentThread();
        var polled = reader.Poll();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(count, polled.Count);
        Assert.Equal(size, reader.Position);
        Assert.True(
            allocated - events < size / 4,
            $"Polling {size} bytes allocated {allocated}, of which {events} is the events themselves; the rest is in proportion to the file.");
    }
}
