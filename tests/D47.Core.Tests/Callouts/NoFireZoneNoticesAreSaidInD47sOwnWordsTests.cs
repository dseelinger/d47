using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class NoFireZoneNoticesAreSaidInD47sOwnWordsTests
{
    private const string Entered = "$STATION_NoFireZone_entered;";

    private const string Exited = "$STATION_NoFireZone_exited;";

    private static readonly string[] Wrong =
        ["not entered", "live", "keep out", "keep clear", "not permitted", "no exit", "disengage"];

    private static IncomingMessages Reader() => new()
    {
        Enabled = () => true,
        IncludeNpcs = () => true,
        CommanderName = "Doug Seelinger",
        CarrierCallSign = "BNH-T2F",
    };

    private static JournalEvent Notice(string key, string from, string time = "2026-10-30T21:35:00Z")
    {
        var localised = key == Entered ? "No fire zone entered." : "No fire zone exited.";
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = time,
            ["event"] = "ReceiveText",
            ["From"] = from,
            ["Message"] = key,
            ["Message_Localised"] = localised,
            ["Channel"] = "npc",
        });

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void EveryLineNamesTheNoFireZone()
    {
        foreach (var line in StationNotices.EnteredLines.Concat(StationNotices.ExitedLines))
        {
            var lower = line.ToLowerInvariant();

            Assert.Contains("no-fire zone", lower, StringComparison.Ordinal);
            Assert.DoesNotContain("no fire zone", lower, StringComparison.Ordinal);

            foreach (var wrong in Wrong)
            {
                Assert.DoesNotContain(wrong, lower, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("no-no-fire", lower, StringComparison.Ordinal);
            Assert.Equal(lower.Split("fire zone").Length - 1, lower.Split("no-fire zone").Length - 1);
        }
    }

    [Fact]
    public void EveryEnteredLineSaysTheShipIsInside()
    {
        Assert.Equal(8, StationNotices.EnteredLines.Count);

        foreach (var line in StationNotices.EnteredLines)
        {
            Assert.True(
                new[] { "entered", "inside", "crossed into" }.Any(w => line.Contains(w, StringComparison.OrdinalIgnoreCase)),
                line);
            Assert.True(
                new[] { "hold", "held", "no shooting" }.Any(w => line.Contains(w, StringComparison.OrdinalIgnoreCase)),
                line);
        }
    }

    [Fact]
    public void EveryExitedLineSaysTheShipHasLeft()
    {
        Assert.Equal(8, StationNotices.ExitedLines.Count);

        foreach (var line in StationNotices.ExitedLines)
        {
            Assert.True(
                new[] { "left", "cleared", "out of" }.Any(w => line.Contains(w, StringComparison.OrdinalIgnoreCase)),
                line);
        }
    }

    [Theory]
    [InlineData(Entered)]
    [InlineData(Exited)]
    public void AStationNoticeIsAPoolLineAndNeverGoesToTheModel(string key)
    {
        var read = Reader().Read(Notice(key, "Jameson Memorial"));

        Assert.NotNull(read);
        var pool = key == Entered ? StationNotices.EnteredLines : StationNotices.ExitedLines;
        Assert.Contains(read.Text, pool.Select(l => string.Format(System.Globalization.CultureInfo.InvariantCulture, l, "pilot")));
        Assert.Null(FlavourBriefs.For(read, personalityEnabled: true));
        Assert.Contains("No fire zone", read.Transcript, StringComparison.Ordinal);
    }

    [Fact]
    public void YourOwnCarrierAddressesTheOwner()
    {
        var read = Reader().Read(Notice(Entered, "Sacred Fire BNH-T2F"));

        Assert.NotNull(read);
        Assert.Equal(IncomingMessages.CarrierCannedKey, read.Key);
        Assert.Contains("Commander Seelinger", read.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("pilot", read.Text, StringComparison.Ordinal);
        Assert.Null(FlavourBriefs.For(read, personalityEnabled: true));
    }

    [Theory]
    [InlineData(Entered)]
    [InlineData(Exited)]
    public void TwoSuccessiveNoticesAreNeverTheSameLine(string key)
    {
        var reader = Reader();

        for (var second = 0; second < 40; second++)
        {
            var a = reader.Read(Notice(key, "Jameson Memorial", $"2026-10-30T21:35:{second:00}Z"));
            var b = reader.Read(Notice(key, "Jameson Memorial", $"2026-10-30T21:35:{second:00}Z"));

            Assert.NotEqual(a!.Text, b!.Text);
        }
    }
}
