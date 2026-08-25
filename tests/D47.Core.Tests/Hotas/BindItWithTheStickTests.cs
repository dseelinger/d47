using D47.Core.Hotas;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>
/// Push-to-talk on a stick button (list.md Phase 53).
/// <para>
/// All of it runs with nothing plugged in, which is the point of <see cref="IHotasReader"/> being
/// a Core contract: the edges, the missing device and the fallback are the parts a Commander only
/// meets when something has gone wrong, and they are exactly the parts hardware testing is worst
/// at reaching.
/// </para>
/// </summary>
public class BindItWithTheStickTests
{
    private const string Stick = "NonRoamable+Id/One=";
    private const string Throttle = "NonRoamable+Id/Two=";

    private static HotasReading Reading(string id, int buttons, params int[] held)
    {
        var state = new bool[buttons];

        foreach (var button in held)
        {
            state[button] = true;
        }

        return new HotasReading { Id = id, Buttons = state };
    }

    // ---- The stored form ---------------------------------------------------------------------

    /// <summary>
    /// A NonRoamableId is base64-ish and carries '+', '/' and '='. A separator that split some
    /// Commanders' ids and not others is the kind of fault that only appears on hardware nobody
    /// testing it owns.
    /// </summary>
    [Fact]
    public void ADeviceIdSurvivesBeingWrittenDownAndReadBack()
    {
        var button = new HotasButton(Stick, 23);

        var read = HotasButton.Parse(button.ToString());

        Assert.Equal(button, read);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-separator")]
    [InlineData("#7")]
    [InlineData("device#")]
    [InlineData("device#nonsense")]
    [InlineData("device#-1")]
    public void AHandEditedBindingThatMakesNoSenseIsUnboundRatherThanACrash(string? stored)
    {
        Assert.Null(HotasButton.Parse(stored));
    }

    /// <summary>
    /// Every stick in the world prints its buttons from one. A Commander told "button 6" would go
    /// looking for the wrong one.
    /// </summary>
    [Fact]
    public void ItIsSaidBackOneBased()
    {
        Assert.Equal("button 24", new HotasButton(Stick, 23).Describe());
    }

    // ---- The walk ----------------------------------------------------------------------------

    private static ButtonCaptureResult Walk(ButtonCapture capture, HotasReading reading, double seconds) =>
        capture.Poll([reading], TimeSpan.FromSeconds(seconds));

    [Fact]
    public void PressingAndReleasingAButtonCapturesIt()
    {
        var capture = new ButtonCapture();

        Assert.Equal(ButtonCaptureStage.Waiting, Walk(capture, Reading(Stick, 32), 0).Stage);
        Assert.Equal(ButtonCaptureStage.Held, Walk(capture, Reading(Stick, 32, 6), 0.5).Stage);

        var done = Walk(capture, Reading(Stick, 32), 1.2);

        Assert.Equal(ButtonCaptureStage.Captured, done.Stage);
        Assert.Equal(new HotasButton(Stick, 6), done.Binding);
    }

    /// <summary>
    /// Sixteen buttons were held at rest on the bench — that is what a maintained switch looks
    /// like from here. A walk that took the first button it saw held would bind a switch position
    /// the Commander never touched.
    /// </summary>
    [Fact]
    public void ButtonsAlreadyHeldWhenTheWalkOpensAreIgnored()
    {
        var capture = new ButtonCapture();

        Walk(capture, Reading(Stick, 32, 3, 11, 19), 0);

        // Still resting, and still no capture, however long those stay down.
        Assert.Equal(ButtonCaptureStage.Waiting, Walk(capture, Reading(Stick, 32, 3, 11, 19), 1).Stage);

        var pressed = Walk(capture, Reading(Stick, 32, 3, 11, 19, 6), 2);
        Assert.Equal(ButtonCaptureStage.Held, pressed.Stage);

        var done = Walk(capture, Reading(Stick, 32, 3, 11, 19), 3);
        Assert.Equal(new HotasButton(Stick, 6), done.Binding);
    }

    /// <summary>
    /// The discriminator, and it is not a duration threshold: the Phase 21 spike proved those
    /// overlap. "Did it come back at all" is a different question and is not close.
    /// </summary>
    [Fact]
    public void AButtonThatNeverComesBackIsCalledASwitchAndDeclined()
    {
        var capture = new ButtonCapture();

        Walk(capture, Reading(Stick, 32), 0);
        Walk(capture, Reading(Stick, 32, 6), 0.5);

        var declined = Walk(capture, Reading(Stick, 32, 6), 0.5 + ButtonCapture.HoldCeiling.TotalSeconds + 1);

        Assert.Equal(ButtonCaptureStage.Declined, declined.Stage);
        Assert.Contains("switch", declined.Says, StringComparison.OrdinalIgnoreCase);
        Assert.Null(declined.Binding);
    }

    [Fact]
    public void TwoButtonsAtOnceIsDeclinedRatherThanGuessedAt()
    {
        var capture = new ButtonCapture();

        Walk(capture, Reading(Stick, 32), 0);

        var declined = Walk(capture, Reading(Stick, 32, 6, 9), 0.5);

        Assert.Equal(ButtonCaptureStage.Declined, declined.Stage);
        Assert.Null(declined.Binding);
    }

    [Fact]
    public void AWalkNobodyTouchesGivesUpAndChangesNothing()
    {
        var capture = new ButtonCapture();

        Walk(capture, Reading(Stick, 32), 0);

        var declined = Walk(capture, Reading(Stick, 32), ButtonCapture.Patience.TotalSeconds + 1);

        Assert.Equal(ButtonCaptureStage.Declined, declined.Stage);
        Assert.Contains("nothing has changed", declined.Says, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Several controllers is the ordinary case, so button 6 alone is ambiguous and the walk has
    /// to carry which device it happened on.
    /// </summary>
    [Fact]
    public void TheWalkRecordsWhichControllerTheButtonWasOn()
    {
        var capture = new ButtonCapture();

        capture.Poll([Reading(Stick, 32), Reading(Throttle, 32)], TimeSpan.Zero);
        capture.Poll([Reading(Stick, 32), Reading(Throttle, 32, 6)], TimeSpan.FromSeconds(0.5));

        var done = capture.Poll([Reading(Stick, 32), Reading(Throttle, 32)], TimeSpan.FromSeconds(1));

        Assert.Equal(new HotasButton(Throttle, 6), done.Binding);
    }

    // ---- The runtime edge --------------------------------------------------------------------

    private sealed record Edges(List<string> Seen)
    {
        public static Edges Watching(PushToTalkButton button)
        {
            var seen = new List<string>();

            button.Pressed += () => seen.Add("down");
            button.Released += () => seen.Add("up");

            return new Edges(seen);
        }
    }

    [Fact]
    public void HoldingTheBoundButtonRaisesTheTwoEdgesOnce()
    {
        var button = new PushToTalkButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32)]);
        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([Reading(Stick, 32)]);

        Assert.Equal(["down", "up"], edges.Seen);
    }

    /// <summary>Another button on the same stick is not this one.</summary>
    [Fact]
    public void AnotherButtonOnTheSameStickDoesNothing()
    {
        var button = new PushToTalkButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32, 9)]);

        Assert.Empty(edges.Seen);
    }

    /// <summary>And the same button index on a different stick is a different button.</summary>
    [Fact]
    public void TheSameIndexOnAnotherControllerIsADifferentButton()
    {
        var button = new PushToTalkButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Throttle, 32, 6)]);

        Assert.Empty(edges.Seen);
        Assert.False(button.DevicePresent);
    }

    /// <summary>
    /// Unplugging mid-transmission closes the gate rather than stranding it open, which is the
    /// listening equivalent of the stranded key release_all() exists for.
    /// </summary>
    [Fact]
    public void AControllerThatVanishesWhileHeldReleases()
    {
        var button = new PushToTalkButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([]);

        Assert.Equal(["down", "up"], edges.Seen);
    }

    /// <summary>
    /// Nothing bound and a bound device that has never been seen are different states, and only
    /// the second is worth interrupting a Commander about.
    /// </summary>
    [Fact]
    public void NothingBoundIsNotTheSameAsADeviceThatIsMissing()
    {
        var button = new PushToTalkButton();

        Assert.Null(button.DevicePresent);

        button.Bind(new HotasButton(Stick, 6));
        Assert.False(button.DevicePresent);

        button.Poll([Reading(Stick, 32)]);
        Assert.True(button.DevicePresent);
    }

    [Fact]
    public void RebindingWhileHeldReleasesFirst()
    {
        var button = new PushToTalkButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32, 6)]);
        button.Bind(new HotasButton(Stick, 9));

        Assert.Equal(["down", "up"], edges.Seen);
    }

    // ---- Both bound (the Commander's call, 2026-08-25) ----------------------------------------

    /// <summary>
    /// Either opens the microphone. The interesting case is both at once: letting go of one while
    /// the other is still held must not close the gate, which is why this counts holds rather
    /// than or-ing two booleans.
    /// </summary>
    [Fact]
    public void EitherSourceOpensTheGateAndTheLastReleaseClosesIt()
    {
        var sources = new PushToTalkSources();
        var seen = new List<string>();

        sources.Pressed += () => seen.Add("down");
        sources.Released += () => seen.Add("up");

        sources.KeyPressed();
        sources.ButtonPressed();
        sources.KeyReleased();

        Assert.True(sources.IsDown);
        Assert.Equal(["down"], seen);

        sources.ButtonReleased();

        Assert.False(sources.IsDown);
        Assert.Equal(["down", "up"], seen);
    }

    [Fact]
    public void EachSourceOnItsOwnStillWorks()
    {
        var sources = new PushToTalkSources();
        var seen = new List<string>();

        sources.Pressed += () => seen.Add("down");
        sources.Released += () => seen.Add("up");

        sources.ButtonPressed();
        sources.ButtonReleased();
        sources.KeyPressed();
        sources.KeyReleased();

        Assert.Equal(["down", "up", "down", "up"], seen);
    }

    // ---- The clash check ---------------------------------------------------------------------

    private static EliteBinds Binds(params EliteBinding[] bindings) => new()
    {
        PresetName = "Custom",
        SourceFile = "Custom.binds",
        Bindings = bindings,
    };

    /// <summary>
    /// Elite counts buttons from one and HotasReading counts from zero. This is the off-by-one
    /// this feature is likeliest to ship, so it is asserted rather than assumed.
    /// </summary>
    [Fact]
    public void EliteCountsButtonsFromOneAndTheReaderCountsFromZero()
    {
        var binds = Binds(new EliteBinding("UseBoostJuice", "Primary", "4098BD65", "Joy_24"));

        Assert.Single(binds.UsingJoystickButton(23));
        Assert.Empty(binds.UsingJoystickButton(24));
    }

    [Fact]
    public void AKeyboardBindingIsNotAJoystickClash()
    {
        var binds = Binds(new EliteBinding("UseBoostJuice", "Primary", "Keyboard", "Joy_24"));

        Assert.Empty(binds.UsingJoystickButton(23));
    }

    [Fact]
    public void AButtonNobodyElseUsesReportsNoClash()
    {
        var binds = Binds(new EliteBinding("UseBoostJuice", "Primary", "4098BD65", "Joy_24"));

        Assert.Empty(binds.UsingJoystickButton(5));
    }
}
