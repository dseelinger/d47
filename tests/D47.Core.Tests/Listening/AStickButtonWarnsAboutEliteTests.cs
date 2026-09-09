using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>The push-to-talk button asking the question the key has always asked.</summary>
public class AStickButtonWarnsAboutEliteTests
{
    private const string Stick = "VID_231D&PID_0201";

    private static ListeningCapability.ListeningSurface Surface(params EliteBinding[] bindings) => new()
    {
        InputDevices = () => ["mic-1"],
        DeviceLabel = id => id,
        SinceHeard = () => null,
        CaptureState = () => (true, null),
        TranscriberState = () => (true, "tiny.en", null),
        InstalledModels = () => ["tiny.en"],
        KeyLabel = key => key,
        Binds = () => new EliteBinds
        {
            PresetName = "Custom",
            SourceFile = "Custom.4.0.binds",
            Bindings = [.. bindings],
        },
    };

    /// <summary>The binds file was never read, which is not the same as having read it.</summary>
    private static ListeningCapability.ListeningSurface Unread() => Surface() with
    {
        Binds = () => new EliteBinds(),
    };

    private static D47Settings BoundTo(int button) => new()
    {
        Listening = new ListeningSettings
        {
            PushToTalkButton = new D47.Core.Hotas.HotasButton(Stick, button).ToString(),
            InputDevice = "mic-1",
        },
    };

    /// <summary>
    /// Elite counts its buttons from one and d47 counts from zero, which is the off-by-one this whole
    /// feature is most likely to ship.
    /// </summary>
    private static EliteBinding Joy(int eliteNumber, string action) =>
        new(action, "Primary", "231D0201", $"Joy_{eliteNumber}");

    [Fact]
    public void ACollidingButtonIsSaidEvenThoughEverythingElseWorks()
    {
        var text = ListeningCapability.Describe(BoundTo(6), Surface(Joy(7, "SelectTarget")));

        Assert.Contains("may collide", text, StringComparison.Ordinal);
        Assert.Contains("SelectTarget", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The hedge is the accurate part and must survive anybody tidying the sentence: d47 cannot tell
    /// whether Elite's <c>Joy_7</c> is on this stick or another one, so it never says "collides".
    /// </summary>
    [Fact]
    public void TheWarningIsHedgedAndNeverClaimsTheSameStick()
    {
        var text = ListeningCapability.Describe(BoundTo(6), Surface(Joy(7, "SelectTarget")));

        Assert.DoesNotContain("is also bound in Elite", text, StringComparison.Ordinal);
        Assert.Contains("same controller", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADifferentButtonIsNotACollision()
    {
        var text = ListeningCapability.DescribeInDetail(BoundTo(6), Surface(Joy(3, "SelectTarget")));

        Assert.DoesNotContain("may collide", text, StringComparison.Ordinal);
        Assert.Contains("No Elite binding uses a button of that number", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The all-clear is plain, exactly as the key's is: nothing found means no button of that number is
    /// bound on any device, so there is nothing left to be uncertain about.
    /// </summary>
    [Fact]
    public void NothingFoundIsAPlainAllClear()
    {
        var text = ListeningCapability.DescribeInDetail(BoundTo(6), Surface());

        Assert.Contains("No Elite binding uses a button of that number", text, StringComparison.Ordinal);
    }

    /// <summary>Silence when the binds were never read.</summary>
    [Fact]
    public void UnreadBindsSayNothingEitherWay()
    {
        var text = ListeningCapability.DescribeInDetail(BoundTo(6), Unread());

        Assert.DoesNotContain("may collide", text, StringComparison.Ordinal);
        Assert.DoesNotContain("No Elite binding uses a button", text, StringComparison.Ordinal);
    }

    /// <summary>No button bound is not an all-clear about a button.</summary>
    [Fact]
    public void NoButtonBoundSaysNothingAboutButtons()
    {
        var keyOnly = new D47Settings
        {
            Listening = new ListeningSettings { PushToTalkKey = "Oem4", InputDevice = "mic-1" },
        };

        var text = ListeningCapability.DescribeInDetail(keyOnly, Surface(Joy(7, "SelectTarget")));

        Assert.DoesNotContain("may collide", text, StringComparison.Ordinal);
        Assert.DoesNotContain("button of that number", text, StringComparison.Ordinal);
    }
}
