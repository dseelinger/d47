using D47.Core.Hotas;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>Push-to-talk on a stick button.</summary>
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

    /// <summary>A NonRoamableId is base64-ish and carries '+', '/' and '='.</summary>
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

    /// <summary>Every stick in the world prints its buttons from one.</summary>
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
    /// Sixteen buttons were held at rest on the bench — that is what a maintained switch looks like
    /// from here.
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
    /// The discriminator, and it is not a duration threshold: the Phase 21 spike proved those overlap.
    /// "Did it come back at all" is a different question and is not close.
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
    /// Several controllers is the ordinary case, so button 6 alone is ambiguous and the walk has to
    /// carry which device it happened on.
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
        public static Edges Watching(BoundButton button)
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
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32)]);
        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([Reading(Stick, 32)]);

        Assert.Equal(["down", "up"], edges.Seen);
    }

    [Fact]
    public void AnotherButtonOnTheSameStickDoesNothing()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32, 9)]);

        Assert.Empty(edges.Seen);
    }

    [Fact]
    public void TheSameIndexOnAnotherControllerIsADifferentButton()
    {
        var button = new BoundButton();
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
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32, 6)]);
        button.Poll([]);

        Assert.Equal(["down", "up"], edges.Seen);
    }

    /// <summary>
    /// Nothing bound and a bound device that has never been seen are different states, and only the
    /// second is worth interrupting a Commander about.
    /// </summary>
    [Fact]
    public void NothingBoundIsNotTheSameAsADeviceThatIsMissing()
    {
        var button = new BoundButton();

        Assert.Null(button.DevicePresent);

        button.Bind(new HotasButton(Stick, 6));
        Assert.False(button.DevicePresent);

        button.Poll([Reading(Stick, 32)]);
        Assert.True(button.DevicePresent);
    }

    [Fact]
    public void RebindingWhileHeldReleasesFirst()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        var edges = Edges.Watching(button);

        button.Poll([Reading(Stick, 32, 6)]);
        button.Bind(new HotasButton(Stick, 9));

        Assert.Equal(["down", "up"], edges.Seen);
    }

    // ---- Both bound (the Commander's call, 2026-08-25) ----------------------------------------

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

    /// <summary>The question asked at the instant of binding has no answer, and an absence of answer is not a "no".</summary>
    [Fact]
    public void NothingIsSaidAboutADeviceNothingHasLookedFor()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        Assert.Null(button.MissingDeviceNotice());

        // And still nothing while the polls are being counted.
        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled - 1; i++)
        {
            button.Poll([]);
            Assert.Null(button.MissingDeviceNotice());
        }
    }

    /// <summary>
    /// The case the warning exists for, which the obvious fix would have silenced along with the false
    /// one.
    /// </summary>
    [Fact]
    public void ADeviceThatNeverTurnsUpIsReportedOnceTheChancesRunOut()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled; i++)
        {
            button.Poll([]);
        }

        var notice = button.MissingDeviceNotice();

        Assert.True(notice.HasValue);
        Assert.Equal(Stick, notice!.Value.DeviceId);
        Assert.Equal(6, notice.Value.Button);
    }

    [Fact]
    public void TheNoticeIsGivenOncePerBinding()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled; i++)
        {
            button.Poll([]);
        }

        Assert.NotNull(button.MissingDeviceNotice());

        for (var i = 0; i < 50; i++)
        {
            button.Poll([]);
            Assert.Null(button.MissingDeviceNotice());
        }

        // Binding again is a new question about a new button, so it re-arms.
        button.Bind(new HotasButton(Throttle, 9));

        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled; i++)
        {
            button.Poll([]);
        }

        Assert.Equal(Throttle, button.MissingDeviceNotice()?.DeviceId);
    }

    /// <summary>
    /// The stick from the Commander's log: bound, present, and spoken through eight seconds later.
    /// </summary>
    [Fact]
    public void AStickThatIsActuallyThereIsNeverReported()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 11));

        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled * 4; i++)
        {
            button.Poll([Reading(Stick, 32)]);
            Assert.Null(button.MissingDeviceNotice());
        }

        Assert.True(button.DevicePresent);
    }

    /// <summary>
    /// A stick that appears late — a wireless one waking up, or a hub enumerating slowly — is not
    /// reported either, as long as it arrives within its chances.
    /// </summary>
    [Fact]
    public void ADeviceThatArrivesLateIsStillNotReported()
    {
        var button = new BoundButton();
        button.Bind(new HotasButton(Stick, 6));

        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled - 1; i++)
        {
            button.Poll([]);
        }

        button.Poll([Reading(Stick, 32)]);

        Assert.Null(button.MissingDeviceNotice());
        Assert.True(button.DevicePresent);
    }

    [Fact]
    public void NothingBoundIsNeverAMissingDevice()
    {
        var button = new BoundButton();
        button.Bind(null);

        for (var i = 0; i < BoundButton.PollsBeforeAbsenceIsCalled * 2; i++)
        {
            button.Poll([]);
        }

        Assert.Null(button.MissingDeviceNotice());
        Assert.Null(button.DevicePresent);
    }
}
