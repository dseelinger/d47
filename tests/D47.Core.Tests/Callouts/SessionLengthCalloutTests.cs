using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The opt-in reminder that a session has run long.</summary>
public class SessionLengthCalloutTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, DateTimeOffset at)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        });
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<Announcement> Say(
        SessionLengthCallout callout, DateTimeOffset now, bool priming = false, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, priming, null, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void AFourHourSessionSpeaksOnceAtFourHours()
    {
        var callout = new SessionLengthCallout();
        Assert.Empty(Say(callout, Start, false, Event("LoadGame", Start)));
        Assert.Empty(Say(callout, Start.AddHours(3.9)));

        var said = Assert.Single(Say(callout, Start.AddHours(4)));

        Assert.Equal(SessionLengthCallout.Key, said.Key);
        Assert.Equal("You have been flying 4 hours, Commander.", said.Text);
        Assert.Empty(Say(callout, Start.AddHours(5)));
    }

    [Fact]
    public void AThreeHourSessionStaysSilent()
    {
        var callout = new SessionLengthCallout();
        Say(callout, Start, false, Event("LoadGame", Start));

        Assert.Empty(Say(callout, Start.AddHours(3), false, Event("Shutdown", Start.AddHours(3))));
        Assert.Empty(Say(callout, Start.AddHours(8)));
    }

    [Fact]
    public void ANewLoadGameRearmsIt()
    {
        var callout = new SessionLengthCallout();
        Say(callout, Start, false, Event("LoadGame", Start));
        Assert.Single(Say(callout, Start.AddHours(4)));

        var second = Start.AddHours(6);
        Assert.Empty(Say(callout, second, false, Event("LoadGame", second)));
        Assert.Single(Say(callout, second.AddHours(4)));
    }

    [Fact]
    public void PrimingSetsTheStartAndSaysNothing()
    {
        var callout = new SessionLengthCallout();

        Assert.Empty(Say(callout, Start.AddHours(5), true, Event("LoadGame", Start)));

        Assert.Single(Say(callout, Start.AddHours(5)));
    }

    [Fact]
    public void TheChosenLengthIsRead()
    {
        var callout = new SessionLengthCallout { Hours = () => 2 };
        Say(callout, Start, false, Event("LoadGame", Start));

        var said = Assert.Single(Say(callout, Start.AddHours(2)));

        Assert.Equal("You have been flying 2 hours, Commander.", said.Text);
    }

    [Fact]
    public void TheRowsExistAndTheReminderDefaultsOff()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var rows = surface.Registry.All.SelectMany(capability => capability.Descriptor.Settings).ToList();

        var toggle = rows.Single(row => row.Key == CalloutCapability.SessionLengthKey);
        var hours = rows.Single(row => row.Key == CalloutCapability.SessionLengthHoursKey);

        Assert.Equal(SettingKind.Toggle, toggle.Kind);
        Assert.Equal(SettingKind.Choice, hours.Kind);
        Assert.False(new CalloutSettings().SessionLength);
        Assert.Equal(4, new CalloutSettings().SessionLengthHours);
        Assert.Equal(6, hours.Binding!.Write!(D47Settings.Defaults, "6")!.Callouts.SessionLengthHours);
        Assert.Equal(4, hours.Binding!.Write!(D47Settings.Defaults, "5")!.Callouts.SessionLengthHours);
    }
}
