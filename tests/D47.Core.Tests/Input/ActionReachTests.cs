using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// What d47 will admit to being able to do, given the Commander's own bindings and the mode
/// they are in (list.md Phase 10, items 1 and 4).
/// <para>
/// The shapes here are taken from the bindings Elite ships. <c>PrimaryFire</c> really is on
/// <c>Mouse_1</c> in <c>KeyboardMouseOnly</c>, <c>GalaxyMapOpen</c> really is unbound in it,
/// and Frontier's own files really do spell the same key <c>Key_BackSlash</c> in one place and
/// <c>Key_backslash</c> in another. Each of those is a test below because each one silently
/// produced the wrong answer first.
/// </para>
/// </summary>
public class ActionReachTests
{
    private static EliteBinds Binds(params (string Action, string Device, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(entry => new EliteBinding(entry.Action, "Primary", entry.Device, entry.Key))],
    };

    private static GameAction Action(string id) =>
        GameActions.Find(id) ?? throw new InvalidOperationException($"No action '{id}' in the catalog.");

    [Fact]
    public void AKeyboardBoundActionInTheRightModeIsOffered()
    {
        var reach = ActionReachability.Resolve(
            Action("landing_gear"),
            Binds(("LandingGearToggle", "Keyboard", "Key_L")),
            ControlContext.NormalSpace);

        Assert.True(reach.IsOffered);
        Assert.Equal("Key_L", reach.Binding?.Key);
        Assert.Empty(reach.Reason);
    }

    [Fact]
    public void TheSameActionIsRefusedInSupercruiseWithTheModeAsTheReason()
    {
        // The cockpit is not one mode. The key exists and would do nothing, which is exactly
        // the case that produces an acknowledged command with no effect.
        var reach = ActionReachability.Resolve(
            Action("landing_gear"),
            Binds(("LandingGearToggle", "Keyboard", "Key_L")),
            ControlContext.Supercruise);

        Assert.Equal(ActionAvailability.WrongMode, reach.Availability);
        Assert.Contains("supercruise", reach.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnActionOnTheJoystickNamesTheDeviceRatherThanFailingSilently()
    {
        var reach = ActionReachability.Resolve(
            Action("hardpoints"),
            Binds(("DeployHardpointToggle", "Joystick", "Joy_3")),
            ControlContext.NormalSpace);

        Assert.Equal(ActionAvailability.OnAnotherDevice, reach.Availability);
        Assert.Contains("joystick", reach.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnboundActionSaysSoRatherThanClaimingTheModeIsWrong()
    {
        // GalaxyMapOpen ships unbound in KeyboardMouseOnly, so this is the default experience
        // rather than an edge case.
        var reach = ActionReachability.Resolve(
            Action("galaxy_map"),
            Binds(("LandingGearToggle", "Keyboard", "Key_L")),
            ControlContext.NormalSpace);

        Assert.Equal(ActionAvailability.NotBound, reach.Availability);
        Assert.Contains("no binding", reach.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AKeyboardSecondaryBeatsAJoystickPrimary()
    {
        // The HOTAS case the checklist is about: the stick holds Primary and the keyboard
        // holds Secondary. Preferring Primary would refuse an action bound to a key in the
        // same file.
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings =
            [
                new EliteBinding("DeployHardpointToggle", "Primary", "Joystick", "Joy_3"),
                new EliteBinding("DeployHardpointToggle", "Secondary", "Keyboard", "Key_U"),
            ],
        };

        var reach = ActionReachability.Resolve(Action("hardpoints"), binds, ControlContext.NormalSpace);

        Assert.True(reach.IsOffered);
        Assert.Equal("Key_U", reach.Binding?.Key);
    }

    [Fact]
    public void AMouseBoundActionIsPressableBecauseTheInjectorReachesButtons()
    {
        // PrimaryFire is Mouse_1 on the preset this machine runs, and it is the honk's only
        // route in. Keyboard-only reachability would make the discovery scanner unreachable
        // on Elite's own default keyboard setup.
        var reach = ActionReachability.Resolve(
            Action("primary_fire"),
            Binds(("PrimaryFire", "Mouse", "Mouse_1")),
            ControlContext.NormalSpace);

        Assert.True(reach.IsOffered);
    }

    [Fact]
    public void TheSrvVariantIsChosenInTheSrv()
    {
        var binds = Binds(
            ("ToggleCargoScoop", "Keyboard", "Key_Home"),
            ("ToggleCargoScoop_Buggy", "Keyboard", "Key_Home"));

        var inShip = ActionReachability.Resolve(Action("cargo_scoop"), binds, ControlContext.NormalSpace);
        var inSrv = ActionReachability.Resolve(Action("cargo_scoop"), binds, ControlContext.Srv);

        Assert.True(inShip.IsOffered);
        Assert.True(inSrv.IsOffered);
        Assert.Equal("ToggleCargoScoop", inShip.Binding?.Action);
        Assert.Equal("ToggleCargoScoop_Buggy", inSrv.Binding?.Action);
    }

    [Fact]
    public void UnknownBindsRefuseEverythingWithOneHonestReason()
    {
        var reach = ActionReachability.Resolve(Action("lights"), EliteBinds.None, ControlContext.NormalSpace);

        Assert.Equal(ActionAvailability.BindsUnknown, reach.Availability);
        Assert.Contains("cannot read", reach.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NothingIsOfferedBeforeStatusJsonHasBeenSeen()
    {
        // A Commander who has not started the game gets an empty profile rather than the whole
        // catalogue advertised against a mode d47 is guessing at.
        var offered = ActionReachability.OfferedIds(
            Binds(("LandingGearToggle", "Keyboard", "Key_L")),
            ControlContexts.Of(GameStatus.Unknown));

        Assert.Empty(offered);
    }

    [Fact]
    public void HyperspaceOffersNothingBecauseTheGameHasTheControls()
    {
        var status = new GameStatus { Flags = StatusFlags.InMainShip | StatusFlags.FsdJump, ReadAt = DateTimeOffset.UnixEpoch };

        Assert.Equal(ControlContext.Hyperspace, ControlContexts.Of(status));
        Assert.Empty(ActionReachability.OfferedIds(Binds(("LandingGearToggle", "Keyboard", "Key_L")), ControlContext.Hyperspace));
    }

    [Fact]
    public void OnFootWinsOverInMainShipBecauseEliteLeavesBothSet()
    {
        // Standing on the pad next to your own ship: Elite keeps InMainShip set. Asking about
        // the ship first would put a walking Commander in a cockpit.
        var status = new GameStatus
        {
            Flags = StatusFlags.InMainShip | StatusFlags.Docked,
            Flags2 = (uint)StatusFlags2.OnFoot,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        Assert.Equal(ControlContext.OnFoot, ControlContexts.Of(status));
    }

    [Theory]
    [InlineData("Key_BackSlash")]
    [InlineData("Key_backslash")]
    public void FrontiersOwnInconsistentSpellingResolvesEitherWay(string symbol)
    {
        // Both spellings appear in the presets Elite ships. An ordinal lookup resolves one and
        // silently drops the other.
        var key = EliteKeys.Resolve(symbol);

        Assert.Equal(EliteInputKind.Keyboard, key.Kind);
        Assert.Equal(0xDCu, key.Code);
    }

    [Theory]
    [InlineData("Neg_Mouse_XAxis")]
    [InlineData("Pos_Joy_RXAxis")]
    [InlineData("Joy_XAxis")]
    public void AnAxisIsNotAButtonEvenWhenItsNameStartsWithOne(string symbol)
    {
        // "Neg_Mouse_XAxis" starts with the mouse prefix and is emphatically not a button.
        var key = EliteKeys.Resolve(symbol);

        Assert.Equal(EliteInputKind.Axis, key.Kind);
        Assert.False(key.IsPressable);
    }

    [Theory]
    [InlineData("Key_Delete", true)]
    [InlineData("Key_RightArrow", true)]
    [InlineData("Key_Numpad_Enter", true)]
    [InlineData("Key_Enter", false)]
    [InlineData("Key_Numpad_4", false)]
    [InlineData("Key_L", false)]
    public void ExtendedKeysAreFlaggedBecauseScancodesCannotTellThemApartWithoutIt(string symbol, bool extended) =>
        Assert.Equal(extended, EliteKeys.IsExtended(symbol));

    [Fact]
    public void EveryCatalogueEntryNamesAtLeastOneModeAndOneEliteAction()
    {
        foreach (var action in GameActions.All)
        {
            Assert.NotEmpty(action.Variants);
            Assert.NotEqual(ControlContext.None, action.Contexts);

            foreach (var variant in action.Variants)
            {
                Assert.NotEqual(ControlContext.None, variant.Contexts);
                Assert.False(string.IsNullOrWhiteSpace(variant.EliteAction));
            }
        }
    }

    [Fact]
    public void ActionIdsAreUniqueBecauseTheyAreAToolsClosedVocabulary()
    {
        var ids = GameActions.Ids;

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }
}
