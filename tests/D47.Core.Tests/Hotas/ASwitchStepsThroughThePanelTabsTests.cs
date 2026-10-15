using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A position can name the next or previous tab of the panel instead of a fixed page.</summary>
public class ASwitchStepsThroughThePanelTabsTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private static readonly IReadOnlyList<PanelDestination> Pages =
    [
        new(PanelTab.Transcript, new NavCrumb("conversation", "Conversation")),
    ];

    private static PanelNavigator Furnished(params PanelTab[] tabs)
    {
        var nav = new PanelNavigator();

        foreach (var tab in tabs)
        {
            nav.Register(tab, new NavCrumb($"root.{tab}", tab.ToString()));
        }

        return nav;
    }

    private static SwitchMapping Rocker() => new()
    {
        Name = "rocker",
        DeviceId = Stick,
        Positions =
        [
            new SwitchPosition(1, Destination: RelativeTab.Next),
            new SwitchPosition(2),
            new SwitchPosition(3, Destination: RelativeTab.Previous),
        ],
    };

    private static SwitchTick Tick(int button, string? showing = null)
    {
        var buttons = new bool[32];
        buttons[button] = true;

        return new SwitchTick
        {
            Now = DateTimeOffset.UnixEpoch,
            Readings = [new HotasReading { Id = Stick, Buttons = buttons }],
            Status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch },
            Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
            Enabled = false,
            Destinations = Pages,
            Showing = showing,
        };
    }

    [Fact]
    public void NextTabMovesOneTabRight()
    {
        var nav = Furnished(PanelTab.Transcript, PanelTab.Search, PanelTab.Settings);

        Assert.True(nav.Step(1));
        Assert.Equal(PanelTab.Search, nav.Tab);
    }

    [Fact]
    public void NextTabWrapsFromTheLastTabToTheFirst()
    {
        var nav = Furnished(PanelTab.Transcript, PanelTab.Search, PanelTab.Settings);
        nav.Select(PanelTab.Settings);

        Assert.True(nav.Step(1));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Fact]
    public void PreviousTabWrapsFromTheFirstTabToTheLast()
    {
        var nav = Furnished(PanelTab.Transcript, PanelTab.Search, PanelTab.Settings);

        Assert.True(nav.Step(-1));
        Assert.Equal(PanelTab.Settings, nav.Tab);
    }

    [Fact]
    public void ATabWithNothingRegisteredIsSkipped()
    {
        var nav = Furnished(PanelTab.Transcript, PanelTab.Commander, PanelTab.Settings);

        Assert.True(nav.Step(1));
        Assert.Equal(PanelTab.Commander, nav.Tab);

        Assert.True(nav.Step(-1));
        Assert.True(nav.Step(-1));
        Assert.Equal(PanelTab.Settings, nav.Tab);
    }

    [Fact]
    public void AStepLandsOnTheTabAsItWasLastLeft()
    {
        var nav = Furnished(PanelTab.Transcript, PanelTab.Search);
        nav.Register(PanelTab.Search, new NavCrumb("root.second", "Second"));
        nav.Select(PanelTab.Search);
        nav.SelectRoot("root.second");
        nav.Select(PanelTab.Transcript);

        Assert.True(nav.Step(1));
        Assert.Equal("root.second", nav.Root.Key);
    }

    [Fact]
    public void ALoneTabHasNowhereToStepTo()
    {
        var nav = Furnished(PanelTab.Transcript);

        Assert.False(nav.Step(1));
    }

    [Fact]
    public void ADialogHoldingThePanelRefusesTheStep()
    {
        var nav = Furnished(PanelTab.Transcript, PanelTab.Search);
        nav.Take(new NavCrumb("choose", "Choose"));

        Assert.False(nav.Step(1));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Fact]
    public void AFlipToNextTabQueuesAStepWhateverTheSurfacesShow()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { Rocker() };

        reconciler.Poll(Tick(2, showing: null), switches);
        reconciler.Poll(Tick(1, showing: null), switches);

        var pending = Assert.Single(reconciler.Drain());

        Assert.Equal(RelativeTab.Next, pending.Destination);
        Assert.Empty(pending.Steps);
    }

    [Fact]
    public void AFlipToPreviousTabQueuesAStepBack()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { Rocker() };

        reconciler.Poll(Tick(2), switches);
        reconciler.Poll(Tick(3), switches);

        Assert.Equal(RelativeTab.Previous, Assert.Single(reconciler.Drain()).Destination);
    }

    [Fact]
    public void ASwitchHeldInTheStepPositionDoesNotRepeat()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { Rocker() };

        reconciler.Poll(Tick(2), switches);
        reconciler.Poll(Tick(1), switches);
        reconciler.Drain();

        reconciler.Poll(Tick(1), switches);
        reconciler.Poll(Tick(1), switches);

        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void AStepPositionNeverDisagreesWithThePanel()
    {
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);
        var switches = new[] { Rocker() };

        reconciler.Poll(Tick(2, "conversation"), switches);
        reconciler.Poll(Tick(1, "conversation"), switches);

        Assert.Null(reconciler.States[0].Disagrees);
    }

    [Fact]
    public void AStepPositionPassesValidation()
    {
        Assert.Null(SwitchValidation.Problem(Rocker()));
    }
}
