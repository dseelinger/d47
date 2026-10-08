using D47.App.Diagnostics;
using D47.Core.Interface;
using D47.Vr;
using D47.Vr.Binding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Stills come from the headset only while SteamVR is showing Elite; otherwise they are Elite's window.
/// A picture of the screen is refused while Elite is not running.
/// </summary>
public class StillsComeFromWhereEliteIsShownTests
{
    private const uint Elite = 4242;

    private static readonly ScreenPicture WindowPicture = new([0xFF, 0xD8], 2, 1, ScreenPictures.FromWindow);

    private sealed class StubWindow(string? refusal) : IWindowCapture
    {
        public List<string> Asked { get; } = [];

        public string? Capture(string path)
        {
            Asked.Add(path);
            return refusal;
        }

        public ScreenCaptureResult Take()
        {
            Asked.Add("take");
            return refusal is null ? new(WindowPicture, null) : new(null, refusal);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Elite's window could not be found")]
    public void NoSessionMeansTheWindowAndItsAnswer(string? refusal)
    {
        var window = new StubWindow(refusal);

        using var capture = Capture(() => null, () => Elite, window);

        Assert.Equal(refusal, capture.Capture("01-00-step.png"));
        Assert.Equal(refusal, capture.Take().Refusal);
        Assert.Equal(["01-00-step.png", "take"], window.Asked);
    }

    [Fact]
    public void ARuntimeThatNeverAttachedMeansTheWindow()
    {
        var window = new StubWindow(null);
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, OpenVrBinding.Instance);

        using var capture = Capture(() => runtime, () => Elite, window);

        Assert.Null(capture.Capture("01-00-step.png"));
        Assert.Same(WindowPicture, capture.Take().Picture);
        Assert.Equal(2, window.Asked.Count);
    }

    [Fact]
    public void WithEliteClosedThePictureIsRefusedAndTheTraceStillAsksTheWindow()
    {
        var window = new StubWindow("Elite's window could not be found");

        using var capture = Capture(() => null, () => 0, window);

        Assert.Equal(new ScreenCaptureResult(null, "Elite is not running"), capture.Take());
        Assert.Empty(window.Asked);

        Assert.Equal("Elite's window could not be found", capture.Capture("01-00-step.png"));
        Assert.Equal(["01-00-step.png"], window.Asked);
    }

    private static HeadsetEyeCapture Capture(Func<SteamVrRuntime?> headset, Func<uint> elite, IWindowCapture window) =>
        new(headset, elite, window, InputTraceWriter.StillWidth, NullLogger<HeadsetEyeCapture>.Instance);
}
