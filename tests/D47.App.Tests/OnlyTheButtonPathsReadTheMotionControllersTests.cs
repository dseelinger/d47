using D47.Core.Hotas;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The motion controllers reach the stick-button poll and a button capture and nothing else, so the HOTAS switches
/// page, the lost-device finder and the stored-id carry-over never see a <c>vr:</c> device (#2548).
/// </summary>
public class OnlyTheButtonPathsReadTheMotionControllersTests
{
    [Trait("Category", "Gate")]
    [Fact]
    public void TheMotionControllersAreReadOnlyByTheStickPollAndACapture()
    {
        var readers = AppSource.CodeLines("ControllerButtons.Poll(", ".MotionControllers(");

        Assert.Equal(
            ["AppHost.cs", "AppHost.cs", "BindCapture.cs"],
            readers.Select(line => line.File).Order(StringComparer.Ordinal));
        Assert.Contains(readers, line => line.Text.Contains("PollTheStick(services.SampledControllers,", StringComparison.Ordinal));
        Assert.Contains(readers, line => line.Text.StartsWith("MotionControllers = () =>", StringComparison.Ordinal));
    }

    [Fact]
    public void AMotionControllerIsNotAStoredIdToCarryOver() =>
        Assert.False(StoredIdCarryOver.IsLegacy("vr:right"));
}
