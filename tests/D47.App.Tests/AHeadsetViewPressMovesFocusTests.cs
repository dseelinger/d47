using D47.App.Input;
using D47.Core.Capabilities.Builtin;
using D47.Core.Hotas;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A headset view switch that sends Shift+Win+D puts Elite in front for VR and d47's main window in front for
/// Desktop, and moves nothing when no press was sent (#1778).
/// </summary>
public class AHeadsetViewPressMovesFocusTests
{
    private sealed class FixedView(HeadsetView showing) : IHeadsetViewSource
    {
        public HeadsetView Read() => showing;
    }

    private sealed class FakeElite(FocusResult result) : IEliteWindow
    {
        public int Raises { get; private set; }

        public bool IsRunning => true;

        public bool IsForeground => false;

        public (int X, int Y, int Width, int Height)? Bounds => null;

        public FocusResult Raise()
        {
            Raises++;
            return result;
        }
    }

    private sealed class FakeMainWindow(FocusResult result)
    {
        public int Raises { get; private set; }

        public FocusResult Raise()
        {
            Raises++;
            return result;
        }
    }

    private static HeadsetViewKey Key(HeadsetView showing, FakeElite elite, FakeMainWindow main) =>
        new(new FixedView(showing), NullLogger<HeadsetViewKey>.Instance, elite: elite, raiseMainWindow: main.Raise)
        {
            DryRun = true,
        };

    private static PendingReconcile Press(HeadsetView view) => new("view switch", "the view", [], View: view);

    private static SwitchReconciler Reconciler() => new(NullLogger<SwitchReconciler>.Instance);

    [Fact]
    public void APressToVrRaisesEliteOnceAndNotD47()
    {
        var elite = new FakeElite(FocusResult.Raised);
        var main = new FakeMainWindow(FocusResult.Raised);

        Assert.Null(Key(HeadsetView.Desktop, elite, main).CarryOut(Press(HeadsetView.Vr), Reconciler()));

        Assert.Equal(1, elite.Raises);
        Assert.Equal(0, main.Raises);
    }

    [Fact]
    public void APressToDesktopRaisesD47OnceAndNotElite()
    {
        var elite = new FakeElite(FocusResult.Raised);
        var main = new FakeMainWindow(FocusResult.Raised);

        Assert.Null(Key(HeadsetView.Vr, elite, main).CarryOut(Press(HeadsetView.Desktop), Reconciler()));

        Assert.Equal(0, elite.Raises);
        Assert.Equal(1, main.Raises);
    }

    [Theory]
    [InlineData(HeadsetView.Vr, HeadsetView.Vr)]
    [InlineData(HeadsetView.Desktop, HeadsetView.Desktop)]
    [InlineData(HeadsetView.Unknown, HeadsetView.Vr)]
    [InlineData(HeadsetView.Unknown, HeadsetView.Desktop)]
    public void NothingIsRaisedWhenNoPressIsSent(HeadsetView showing, HeadsetView wanted)
    {
        var elite = new FakeElite(FocusResult.Raised);
        var main = new FakeMainWindow(FocusResult.Raised);

        Key(showing, elite, main).CarryOut(Press(wanted), Reconciler());

        Assert.Equal(0, elite.Raises);
        Assert.Equal(0, main.Raises);
    }

    [Fact]
    public void ARefusedRaiseOfEliteIsSpokenWithTheSwitchName()
    {
        var said = Key(HeadsetView.Desktop, new FakeElite(FocusResult.Refused), new FakeMainWindow(FocusResult.Raised))
            .CarryOut(Press(HeadsetView.Vr), Reconciler());

        Assert.Equal(
            "I could not bring Elite forward from view switch. " + FocusCapability.Describe(FocusResult.Refused),
            said);
    }

    [Fact]
    public void ARefusedRaiseOfD47IsSpokenWithTheSwitchName()
    {
        var said = Key(HeadsetView.Vr, new FakeElite(FocusResult.Raised), new FakeMainWindow(FocusResult.Refused))
            .CarryOut(Press(HeadsetView.Desktop), Reconciler());

        Assert.StartsWith("I could not bring d47 forward from view switch.", said);
    }

    [Theory]
    [InlineData(FocusResult.Raised)]
    [InlineData(FocusResult.AlreadyThere)]
    [InlineData(FocusResult.NotRunning)]
    public void ARaiseThatIsNotRefusedIsSilent(FocusResult result)
    {
        Assert.Null(Key(HeadsetView.Desktop, new FakeElite(result), new FakeMainWindow(result))
            .CarryOut(Press(HeadsetView.Vr), Reconciler()));
        Assert.Null(Key(HeadsetView.Vr, new FakeElite(result), new FakeMainWindow(result))
            .CarryOut(Press(HeadsetView.Desktop), Reconciler()));
    }
}
