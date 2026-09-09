using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheLocalVoiceRowOffersItsButtonTests
{
    /// <summary>
    /// The App's shape, and the reason for the test: the thing the delegates close over does not
    /// exist yet when the rows are built.
    /// </summary>
    private static (SpeechCapability.SpeechSurface Surface, Action Arrive, List<string> Presses) Deferred()
    {
        object? host = null;
        var presses = new List<string>();

        var surface = new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Beds = () => [],
            LocalVoiceState = () => host is null ? "Not available." : "Not downloaded. About 350 MB.",
            DownloadLocalVoice = () => host is null
                ? null
                : (_, _) =>
                {
                    presses.Add("pressed");
                    return Task.FromResult<string?>(null);
                },
        };

        return (surface, () => host = new object(), presses);
    }

    private static SettingRow Row(SpeechCapability.SpeechSurface surface) =>
        SpeechCapability.Create(surface).Settings
            .Single(row => row.Key == SpeechCapability.LocalVoiceKey);

    /// <summary>
    /// The button is there for a host that arrives after the rows do, which is every host d47 has.
    /// </summary>
    [Fact]
    public async Task TheButtonSurvivesAHostThatArrivesLater()
    {
        var (surface, arrive, presses) = Deferred();

        // Built while the host is still null, exactly as AppHost builds it.
        var row = Row(surface);

        arrive();

        Assert.NotNull(row.PressLabel);
        Assert.NotNull(row.PressAsync);

        await row.PressAsync!(new Progress<double>(), CancellationToken.None);

        Assert.Single(presses);
    }

    /// <summary>
    /// And the state is read at draw time rather than captured, so the row changes the moment a
    /// download finishes rather than describing the world as it was at startup.
    /// </summary>
    [Fact]
    public void TheStateIsReadWhenTheRowIsDrawnRatherThanWhenItIsBuilt()
    {
        var (surface, arrive, _) = Deferred();
        var row = Row(surface);

        Assert.Equal("Not available.", row.Binding!.Read(new D47Settings()));

        arrive();

        Assert.Equal("Not downloaded. About 350 MB.", row.Binding!.Read(new D47Settings()));
    }

    /// <summary>
    /// A surface that genuinely cannot download keeps the row and loses the button, which is the
    /// designer's case and the one the "absent rows hide from every test" rule cares about: the row
    /// must exist either way, or the fault in it is invisible to the whole suite.
    /// </summary>
    [Fact]
    public void NoDownloaderMeansNoButtonAndStillARow()
    {
        var row = Row(new SpeechCapability.SpeechSurface { Silence = () => { }, Beds = () => [] });

        Assert.Null(row.Press);
        Assert.Null(row.PressAsync);
        Assert.Null(row.PressLabel);
        Assert.NotNull(row.Binding);
    }
}
