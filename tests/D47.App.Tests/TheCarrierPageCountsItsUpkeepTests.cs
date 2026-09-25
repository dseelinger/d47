using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The carrier page's balance, less the weekly upkeep since it was recorded (#447).</summary>
public class TheCarrierPageCountsItsUpkeepTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T13:00:00Z");

    private static string Stats(string at, long balance) =>
        $$$"""{"timestamp":"{{{at}}}","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","JumpRangeCurr":500.0,"SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},"Finance":{"CarrierBalance":{{{balance}}}},"Crew":[{"CrewRole":"Refuel","Activated":true,"Enabled":true}]}""";

    private static CarrierState Fold(params string[] lines)
    {
        var state = CarrierState.None;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state = state.Apply(parsed!);
        }

        return state;
    }

    private static (Window Window, List<string> Text) Draw(CarrierState carrier)
    {
        var page = new CarrierPage(new CarrierSource(() => carrier, () => CarrierState.NoSquadron, () => 0), () => Now);
        var window = new Window { Content = page, Width = 924, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();

        return (window, text);
    }

    [AvaloniaFact]
    public void AnAdjustedBalanceSaysSoAndFromWhat()
    {
        var (window, text) = Draw(Fold(
            Stats("2026-09-13T20:00:00Z", 1_009_702_661),
            Stats("2026-09-18T20:00:00Z", 1_000_002_661),
            Stats("2026-09-22T23:24:52Z", 990_302_661)));

        Assert.Contains("about 980,602,661 cr", text);
        Assert.Contains("9,700,000 cr a week", text);
        Assert.Contains("101 weeks", text);
        Assert.Contains(
            "Balance adjusted: 990,302,661 on 22 Sep, less one week's upkeep of 9,700,000 cr.",
            text);

        var path = Path.Combine(TestSurface.CaptureDirectory, "carrier-upkeep-adjusted.png");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        window.Close();
        Assert.True(File.Exists(path));
    }

    [AvaloniaFact]
    public void BeforeAnyUpkeepIsKnownOnlyTheBalanceIsDrawn()
    {
        var (window, text) = Draw(Fold(Stats("2026-09-18T20:00:00Z", 990_302_661)));

        Assert.Contains("990,302,661 cr", text);
        Assert.DoesNotContain("Upkeep", text);
        Assert.DoesNotContain("Covers", text);
        Assert.DoesNotContain(text, line => line.StartsWith("Balance adjusted", StringComparison.Ordinal));

        window.Close();
    }
}
