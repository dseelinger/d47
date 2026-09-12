using Microsoft.Extensions.Logging;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Registration is retried on every tick until it takes, but a refusal that persists writes the
/// manifest files once and says so once (#149).
/// </summary>
public class ARefusedRegistrationIsRetriedQuietlyTests
{
    [Fact]
    public void AHundredRefusalsAreOneWarningAndOneSetOfFiles()
    {
        var (openVr, input, lines, folder) = Session();

        try
        {
            openVr.ManifestRefused = EVRInputError.InvalidHandle;

            input.Register(folder);
            Directory.Delete(folder, true);

            for (var again = 0; again < 99; again++)
            {
                input.Register(folder);
            }

            Assert.False(Directory.Exists(folder));
            Assert.Equal(100, openVr.Count(nameof(FakeOpenVr.SetActionManifestPath)));
            Assert.Single(lines, line => line.Contains("would not load the action manifest", StringComparison.Ordinal));
            Assert.False(input.Ready);
        }
        finally
        {
            Clean(folder);
        }
    }

    [Fact]
    public void TheCallThatGoesThroughSaysSoAndIsTheLastOne()
    {
        var (openVr, input, lines, folder) = Session();

        try
        {
            openVr.ManifestRefused = EVRInputError.InvalidHandle;
            input.Register(folder);

            openVr.ManifestRefused = EVRInputError.None;
            input.Register(folder);

            Assert.True(input.Ready);
            Assert.Contains(lines, line => line.Contains("Controller input is on", StringComparison.Ordinal));

            openVr.Calls.Clear();
            input.Register(folder);

            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.SetActionManifestPath)));
        }
        finally
        {
            Clean(folder);
        }
    }

    [Fact]
    public void ADifferentRefusalIsSaidOnceMore()
    {
        var (openVr, input, lines, folder) = Session();

        try
        {
            openVr.ManifestRefused = EVRInputError.InvalidHandle;
            input.Register(folder);
            input.Register(folder);

            openVr.ManifestRefused = EVRInputError.NoActiveActionSet;
            input.Register(folder);
            input.Register(folder);

            Assert.Equal(
                2,
                lines.Count(line => line.Contains("would not load the action manifest", StringComparison.Ordinal)));
        }
        finally
        {
            Clean(folder);
        }
    }

    private static (FakeOpenVr OpenVr, VrActionInput Input, List<string> Lines, string Folder) Session()
    {
        var openVr = new FakeOpenVr();
        var error = EVRInitError.None;

        openVr.Init(ref error, EVRApplicationType.VRApplication_Overlay);

        var lines = new List<string>();
        var folder = Path.Combine(Path.GetTempPath(), "d47-actions-" + Guid.NewGuid().ToString("N"));

        return (openVr, new VrActionInput(new Capture(lines), openVr), lines, folder);
    }

    private static void Clean(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private sealed class Capture(List<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
