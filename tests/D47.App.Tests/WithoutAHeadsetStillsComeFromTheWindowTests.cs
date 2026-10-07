using D47.App.Diagnostics;
using D47.Vr;
using D47.Vr.Binding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The input trace's still comes from the headset only while d47 is attached to SteamVR; otherwise it
/// is Elite's window, exactly as before (#601).
/// </summary>
public class WithoutAHeadsetStillsComeFromTheWindowTests
{
    private sealed class StubWindow(string? refusal) : IWindowCapture
    {
        public List<string> Asked { get; } = [];

        public string? Capture(string path)
        {
            Asked.Add(path);
            return refusal;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Elite's window could not be found")]
    public void NoSessionMeansTheWindowAndItsAnswer(string? refusal)
    {
        var window = new StubWindow(refusal);

        using var capture = new HeadsetEyeCapture(
            () => null, window, InputTraceWriter.StillWidth, NullLogger<HeadsetEyeCapture>.Instance);

        Assert.Equal(refusal, capture.Capture("01-00-step.png"));
        Assert.Equal(["01-00-step.png"], window.Asked);
    }

    [Fact]
    public void ARuntimeThatNeverAttachedMeansTheWindow()
    {
        var window = new StubWindow(null);
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, OpenVrBinding.Instance);

        using var capture = new HeadsetEyeCapture(
            () => runtime, window, InputTraceWriter.StillWidth, NullLogger<HeadsetEyeCapture>.Instance);

        Assert.Null(capture.Capture("01-00-step.png"));
        Assert.Single(window.Asked);
    }
}
