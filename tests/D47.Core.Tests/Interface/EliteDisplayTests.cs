using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Whether d47 can tell that a topmost strip will be visible over the game (list.md Phase 48).
/// <para>
/// Worth a test out of proportion to its size, because the failure it guards against is silent:
/// an exclusive-fullscreen game composites over nothing, so the overlay is simply not there —
/// no error, no log line. The same shape as the VR overlay that ran with sound and no picture,
/// and as the microphone whose silent default was indistinguishable from not hearing.
/// </para>
/// </summary>
public class EliteDisplayTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-display-" + Guid.NewGuid().ToString("N"));

    public EliteDisplayTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A test's own scratch folder is not worth failing a run over.
        }
    }

    /// <summary>
    /// The file as it stands on the Commander's own machine, read 2026-08-22 with the game set to
    /// borderless — copied here whole rather than trimmed to the one element, because the reader's
    /// job includes not being confused by the rest of it.
    /// </summary>
    private const string Real = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <DisplayConfig>
        	<ScreenWidth>3840</ScreenWidth>
        	<ScreenHeight>2160</ScreenHeight>
        	<VSync>false</VSync>
        	<FullScreen>2</FullScreen>
        	<PresentInterval>1</PresentInterval>
        	<Adapter>0</Adapter>
        	<Monitor>1</Monitor>
        	<DX11_RefreshRateNumerator>60</DX11_RefreshRateNumerator>
        	<DX11_RefreshRateDenominator>1</DX11_RefreshRateDenominator>
        	<LimitFrameRate>true</LimitFrameRate>
        	<MaxFramesPerSecond>90</MaxFramesPerSecond>
        </DisplayConfig>
        """;

    [Fact]
    public void TwoIsBorderlessOnTheCommandersOwnFile()
    {
        Assert.Equal(EliteDisplayMode.Borderless, EliteDisplay.Read(Write(Real)));
    }

    [Theory]
    [InlineData(0, EliteDisplayMode.Windowed)]
    [InlineData(1, EliteDisplayMode.Exclusive)]
    [InlineData(2, EliteDisplayMode.Borderless)]
    public void EachDocumentedNumberReadsAsItsMode(int number, EliteDisplayMode expected)
    {
        Assert.Equal(expected, EliteDisplay.Read(Write(Config(number.ToString()))));
    }

    /// <summary>
    /// Missing, hand-edited, or written by a mod: all one answer, and that answer draws the
    /// overlay rather than refusing to.
    /// </summary>
    [Fact]
    public void AFileItCannotReadIsCannotTellRatherThanAnError()
    {
        Assert.Equal(EliteDisplayMode.Unknown, EliteDisplay.Read(Path.Combine(_folder, "absent.xml")));
        Assert.Equal(EliteDisplayMode.Unknown, EliteDisplay.Read(Write("<DisplayConfig>")));
        Assert.Equal(EliteDisplayMode.Unknown, EliteDisplay.Read(Write(Config("borderless"))));
        Assert.Equal(EliteDisplayMode.Unknown, EliteDisplay.Read(Write(Config(string.Empty))));
        Assert.Equal(EliteDisplayMode.Unknown, EliteDisplay.Read(Write("<Something><FullScreen>2</FullScreen></Something>")));
    }

    /// <summary>
    /// A number nobody has documented is reported <em>by number</em> rather than folded into
    /// "cannot tell". 0, 1 and 2 are the community's reading and only 2 has been seen here, so a 3
    /// arriving one day should say 3 out loud rather than looking like an unreadable file.
    /// </summary>
    [Fact]
    public void AnUndocumentedNumberIsNamedInTheSentence()
    {
        var path = Write(Config("7"));

        Assert.Equal(EliteDisplayMode.Unknown, EliteDisplay.Read(path));
        Assert.Contains("7", EliteDisplay.Describe(path), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The sentence is worth more than the feature it guards.</b> A Commander in exclusive full
    /// screen who turns the overlay on sees nothing at all, and this row is the only thing that
    /// can tell them why — so it has to name the mode and say what to do about it.
    /// </summary>
    [Fact]
    public void ExclusiveFullScreenIsSaidByName()
    {
        var said = EliteDisplay.Describe(Write(Config("1")));

        Assert.Contains("exclusive full screen", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("borderless", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CannotTellSaysSoAndDrawsAnyway()
    {
        var said = EliteDisplay.Describe(Path.Combine(_folder, "absent.xml"));

        Assert.Contains("could not read", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anyway", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Beside the graphics override the theme already opens, in the same folder.</summary>
    [Fact]
    public void ItLooksBesideTheFileTheThemeAlreadyReads()
    {
        Assert.Equal(
            Path.GetDirectoryName(ElitePalette.DefaultPath()),
            Path.GetDirectoryName(EliteDisplay.DefaultPath()));

        Assert.Equal("DisplaySettings.xml", Path.GetFileName(EliteDisplay.DefaultPath()));
    }

    private static string Config(string fullScreen) =>
        $"<DisplayConfig><ScreenWidth>1920</ScreenWidth><FullScreen>{fullScreen}</FullScreen></DisplayConfig>";

    private string Write(string xml)
    {
        var path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path, xml);
        return path;
    }
}
