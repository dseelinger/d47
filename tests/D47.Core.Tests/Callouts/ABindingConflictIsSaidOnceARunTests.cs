using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The start line, the line for a conflict that appears later, and that neither repeats in a run (#2384).</summary>
public class ABindingConflictIsSaidOnceARunTests
{
    private static readonly DateTimeOffset Now = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly BindingConflict PushToTalk = new(
        "Push-to-talk", "listening.pushToTalkKey", null, null, BindingKind.Key, "Ctrl+Alt+O", ["ShipSpotLightToggle"]);

    private static readonly BindingConflict Cancel = new(
        "Cancel", "speech.cancelKey", null, null, BindingKind.Key, "Ctrl+Alt+P", ["NightVisionToggle"]);

    private static readonly BindingConflict PushToTalkButton = new(
        "Push-to-talk", "listening.pushToTalkButton", null, null, BindingKind.Button, "Button 9", ["LandingGearToggle"]);

    private static readonly BindingConflict GearSwitch = new(
        "Gear", null, "Gear", "Button 12", BindingKind.SwitchPosition, "Button 12", ["LandingGearToggle"]);

    private static CalloutContext Tick(bool priming = false) =>
        new(Now, priming, null, GameStatus.Unknown, NavRoute.None, []);

    private static List<string> Said(BindingConflictCallout callout, bool priming = false) =>
        [.. callout.Examine(Tick(priming)).Select(announcement => announcement.Text)];

    [Fact]
    public void TheStartLineIsSaidOnceWhenConflictsExist()
    {
        IReadOnlyList<BindingConflict> conflicts = [PushToTalk, Cancel, PushToTalkButton, GearSwitch];
        var callout = new BindingConflictCallout(() => conflicts);

        Assert.Equal(
            ["Commander, Elite also binds four of my keys and buttons. They are on the Binding conflicts page."],
            Said(callout));
        Assert.Empty(Said(callout));
    }

    [Fact]
    public void NothingIsSaidWithNoConflicts()
    {
        var callout = new BindingConflictCallout(() => []);

        Assert.Empty(Said(callout));
        Assert.Empty(Said(callout));
    }

    [Fact]
    public void OneConflictIsSaidInTheSingular()
    {
        var callout = new BindingConflictCallout(() => [PushToTalk]);

        Assert.Equal(
            ["Commander, Elite also binds one of my keys. It is on the Binding conflicts page."],
            Said(callout));
    }

    [Fact]
    public void ButtonsAndSwitchPositionsAreButtons()
    {
        var callout = new BindingConflictCallout(() => [PushToTalkButton, GearSwitch]);

        Assert.Equal(
            ["Commander, Elite also binds two of my buttons. They are on the Binding conflicts page."],
            Said(callout));
    }

    [Fact]
    public void ANewConflictGetsTheSecondLineOnceAndIsNotSaidAgainWhenItReturns()
    {
        IReadOnlyList<BindingConflict> conflicts = [PushToTalk];
        var callout = new BindingConflictCallout(() => conflicts);

        Assert.Single(Said(callout));

        conflicts = [PushToTalk, PushToTalkButton];
        Assert.Equal(["Elite now binds one more of my buttons. It is on the Binding conflicts page."], Said(callout));
        Assert.Empty(Said(callout));

        conflicts = [PushToTalk];
        Assert.Empty(Said(callout));

        conflicts = [PushToTalk, PushToTalkButton];
        Assert.Empty(Said(callout));
    }

    [Fact]
    public void ARebindToAnotherConflictingKeyIsANewConflict()
    {
        IReadOnlyList<BindingConflict> conflicts = [PushToTalk];
        var callout = new BindingConflictCallout(() => conflicts);

        Assert.Single(Said(callout));

        conflicts = [PushToTalk with { Gesture = "Ctrl+Alt+P", EliteActions = ["NightVisionToggle"] }];

        Assert.Equal(["Elite now binds one more of my keys. It is on the Binding conflicts page."], Said(callout));
    }

    [Fact]
    public void AConflictAppearingAfterAStartWithNoneGetsTheSecondLine()
    {
        IReadOnlyList<BindingConflict> conflicts = [];
        var callout = new BindingConflictCallout(() => conflicts);

        Assert.Empty(Said(callout));

        conflicts = [PushToTalk, Cancel];

        Assert.Equal(["Elite now binds two more of my keys. They are on the Binding conflicts page."], Said(callout));
    }

    [Fact]
    public void NothingIsMarkedSpokenWhilePriming()
    {
        var callout = new BindingConflictCallout(() => [PushToTalk]);

        Assert.Empty(Said(callout, priming: true));
        Assert.Empty(Said(callout, priming: true));

        Assert.Equal(
            ["Commander, Elite also binds one of my keys. It is on the Binding conflicts page."],
            Said(callout));
    }

    [Fact]
    public void BindsKnownAfterTheFirstTickGiveTheStartLine()
    {
        IReadOnlyList<BindingConflict>? conflicts = null;
        var callout = new BindingConflictCallout(() => conflicts);

        Assert.Empty(Said(callout));
        Assert.Empty(Said(callout));

        conflicts = [PushToTalk, PushToTalkButton];

        Assert.Equal(
            ["Commander, Elite also binds two of my keys and buttons. They are on the Binding conflicts page."],
            Said(callout));
    }

    [Fact]
    public void ItsSwitchAndAllCalloutsSilenceIt()
    {
        var (engine, settings) = ShippedCatalogue.Build(() => [PushToTalk]);

        settings.Replace("test", current => current with { Callouts = current.Callouts with { BindingConflicts = false } });
        ShippedCallouts.Apply(engine, settings, Now);
        engine.Tick(Tick());
        Assert.DoesNotContain(engine.Drain(), announcement => announcement.Key == BindingConflictCallout.Key);

        settings.Replace("test", current => current with
        {
            Callouts = current.Callouts with { BindingConflicts = true, Enabled = false },
        });
        ShippedCallouts.Apply(engine, settings, Now);
        engine.Tick(Tick());
        Assert.DoesNotContain(engine.Drain(), announcement => announcement.Key == BindingConflictCallout.Key);

        settings.Replace("test", current => current with { Callouts = current.Callouts with { Enabled = true } });
        ShippedCallouts.Apply(engine, settings, Now);
        engine.Tick(Tick());
        Assert.Contains(engine.Drain(), announcement => announcement.Key == BindingConflictCallout.Key);
    }

    [Fact]
    public void TheSwitchIsOnByDefault() => Assert.True(new CalloutSettings().BindingConflicts);
}
