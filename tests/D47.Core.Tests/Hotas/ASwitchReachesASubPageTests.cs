using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A switch position can name a root's fixed sub-page as well as the root (#1149).</summary>
public class ASwitchReachesASubPageTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private static readonly NavCrumb Plan = new("routing.plan", "Plan");

    private static readonly IReadOnlyList<PanelDestination> Pages =
    [
        new(PanelTab.Navigation, Plan),
        new(PanelTab.Navigation, Plan, new NavCrumb("routing.progress", "Progress")),
        new(PanelTab.Transcript, new NavCrumb("conversation", "Conversation")),
    ];

    /// <summary>Button 4 is the conversation, 5 is Plan, 6 is Plan's Progress.</summary>
    private static SwitchMapping PanelSwitch() => new()
    {
        Name = "panel switch",
        DeviceId = Stick,
        Positions =
        [
            new SwitchPosition(4, Destination: "conversation"),
            new SwitchPosition(5, Destination: "routing.plan"),
            new SwitchPosition(6, Destination: "routing.progress"),
        ],
    };

    private static HotasReading Held(int button)
    {
        var buttons = new bool[32];
        buttons[button] = true;

        return new HotasReading { Id = Stick, Buttons = buttons };
    }

    private static SwitchTick Tick(int button, string? showing) => new()
    {
        Now = DateTimeOffset.UnixEpoch,
        Readings = [Held(button)],
        Status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch },
        Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
        Enabled = false,
        Destinations = Pages,
        Showing = showing,
    };

    private static SwitchReconciler New() => new(NullLogger<SwitchReconciler>.Instance);

    [Fact]
    public void AFlipToASubPageQueuesAMoveWithItsWord()
    {
        var reconciler = New();
        var switches = new[] { PanelSwitch() };

        reconciler.Poll(Tick(4, "conversation"), switches);
        reconciler.Poll(Tick(6, "conversation"), switches);

        var pending = Assert.Single(reconciler.Drain());

        Assert.Equal("routing.progress", pending.Destination);
        Assert.Equal("Progress", pending.Label);
        Assert.Empty(pending.Steps);
    }

    [Fact]
    public void AFlipToTheSubPageShowingMovesNothing()
    {
        var reconciler = New();
        var switches = new[] { PanelSwitch() };

        reconciler.Poll(Tick(4, "routing.progress"), switches);
        reconciler.Poll(Tick(6, "routing.progress"), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Null(reconciler.States[0].Disagrees);
    }

    [Fact]
    public void ARootIsStillShowingWhileOneOfItsSubPagesIsOpen()
    {
        // Every launch opens Plan with Progress beside it; a switch sitting on Plan agrees with that.
        var reconciler = New();
        var switches = new[] { PanelSwitch() };

        reconciler.Poll(Tick(4, "routing.progress"), switches);
        reconciler.Poll(Tick(5, "routing.progress"), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Null(reconciler.States[0].Disagrees);
    }

    [Fact]
    public void ASubPageIsNotShowingWhileOnlyItsRootIs()
    {
        var reconciler = New();
        var switches = new[] { PanelSwitch() };

        reconciler.Poll(Tick(5, "routing.plan"), switches);
        reconciler.Poll(Tick(6, "routing.plan"), switches);

        Assert.Equal("routing.progress", Assert.Single(reconciler.Drain()).Destination);

        reconciler.Poll(Tick(6, "routing.plan"), switches);

        Assert.Equal("the panel is on Plan", reconciler.States[0].Disagrees);
    }
}
