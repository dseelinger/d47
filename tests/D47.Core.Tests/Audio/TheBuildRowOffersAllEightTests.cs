using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The row that lets a Commander choose which of Kokoro's eight builds runs the local voice
/// (<a href="https://github.com/dseelinger/d47/issues/139">#139</a>).
/// <para>
/// <b>What the row has to do is not obvious from its shape</b>, which is why it is asserted here
/// rather than left to the picker: it must state speed and size together, mark the one already
/// installed, read what is on disk rather than what settings say, be absent entirely until there is
/// a local voice to swap, and write nothing until the file has landed.
/// </para>
/// </summary>
public class TheBuildRowOffersAllEightTests
{
    /// <summary>
    /// The App's shape, and the reason the surface reads through delegates at all: a host that
    /// arrives after the rows do (see <see cref="TheLocalVoiceRowOffersItsButtonTests"/>, which is
    /// the fault this pattern exists to prevent).
    /// </summary>
    private static SpeechCapability.SpeechSurface Surface(
        string? installed,
        List<string>? switches = null) =>
        new()
        {
            Silence = () => { },
            Beds = () => [],
            InstalledLocalVoiceBuild = () => installed,
            SwitchLocalVoiceBuild = build => (_, _) =>
            {
                switches?.Add(build);
                return Task.FromResult<string?>(null);
            },
        };

    private static SettingRow? Row(SpeechCapability.SpeechSurface surface) =>
        SpeechCapability.Create(surface).Settings
            .SingleOrDefault(row => row.Key == SpeechCapability.LocalVoiceBuildKey);

    private static bool Applies(SettingRow row) =>
        row.AppliesWhen is null || row.AppliesWhen(new D47Settings());

    // ---- What it offers -----------------------------------------------------------------------

    /// <summary>All eight, and no build that has not been measured.</summary>
    [Fact]
    public void EveryPublishedBuildIsOffered()
    {
        var row = Row(Surface("fp32"))!;

        Assert.Equal(KokoroAssets.BuildIds, row.Choices);
        Assert.Equal(8, row.Choices!.Count);
    }

    /// <summary>
    /// <b>Speed and size on every choice, together.</b> The issue's own argument: listing eight
    /// builds by their file size would tell a Commander almost nothing true about what they were
    /// choosing, because the smallest is the slowest.
    /// </summary>
    [Fact]
    public void EveryChoiceStatesItsSizeAndItsSpeed()
    {
        var row = Row(Surface(null))!;

        foreach (var id in KokoroAssets.BuildIds)
        {
            var label = row.LabelForChoice(id, new D47Settings());

            Assert.Contains("MB", label, StringComparison.Ordinal);
            Assert.Contains("realtime", label, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The one already on disk is marked rather than hidden — a Commander comparing builds needs
    /// to know which choice costs a download and which is already paid for.
    /// </summary>
    [Fact]
    public void TheInstalledBuildIsMarked()
    {
        var row = Row(Surface("uint8"))!;

        Assert.EndsWith("— installed", row.LabelForChoice("uint8", new D47Settings()), StringComparison.Ordinal);
        Assert.DoesNotContain("installed", row.LabelForChoice("fp32", new D47Settings()), StringComparison.Ordinal);
    }

    // ---- What it reads ------------------------------------------------------------------------

    /// <summary>
    /// <b>What is on disk outranks what the settings file says.</b> The byte count is a fact and
    /// the setting is a record of one, so a Commander who replaced the model by hand reads the
    /// build they actually have.
    /// </summary>
    [Fact]
    public void TheRowReadsTheDiskRatherThanTheSetting()
    {
        var row = Row(Surface("q4f16"))!;

        var settings = new D47Settings
        {
            Speech = new SpeechSettings { LocalVoiceBuild = "quantized" },
        };

        Assert.Equal("q4f16", row.Binding!.Read(settings));
    }

    /// <summary>And it falls back to the setting, then to the default, when the disk cannot say.</summary>
    [Fact]
    public void WithNothingOnDiskItReadsTheSettingThenTheDefault()
    {
        var row = Row(Surface(null))!;

        Assert.Equal(
            "q4",
            row.Binding!.Read(new D47Settings
            {
                Speech = new SpeechSettings { LocalVoiceBuild = "q4" },
            }));

        Assert.Equal(KokoroAssets.DefaultBuildId, row.Binding!.Read(new D47Settings()));
    }

    // ---- When it is there at all --------------------------------------------------------------

    /// <summary>
    /// <b>Absent until there is a local voice to swap.</b> Offering a build to replace an install
    /// that does not exist is offering to download the model twice — before that, the row above it
    /// is the one that matters.
    /// </summary>
    [Fact]
    public void TheRowIsAbsentUntilTheLocalVoiceIsInstalled()
    {
        Assert.False(Applies(Row(Surface(null))!));
        Assert.True(Applies(Row(Surface("fp32"))!));
    }

    /// <summary>
    /// A surface that cannot answer at all keeps the row and cannot apply it, which is the
    /// designer's case — the row must exist either way, or a fault in it is invisible to the whole
    /// suite ("absent rows hide from every test").
    /// </summary>
    [Fact]
    public void NoSurfaceMeansNoSwapAndStillARow()
    {
        var row = Row(new SpeechCapability.SpeechSurface { Silence = () => { }, Beds = () => [] });

        Assert.NotNull(row);
        Assert.Null(row!.FetchChoiceAsync);
        Assert.False(Applies(row));
    }

    // ---- The setting is written only once the file is there -----------------------------------

    /// <summary>
    /// <b>The choice is the go-ahead and the fetch is what stands between it and the setting.</b>
    /// The row carries the download itself, so the view writes nothing until it answers — which is
    /// the rule the speech-to-text row already enforces, and the reason a row can never name a
    /// build d47 cannot load.
    /// </summary>
    [Fact]
    public async Task ChoosingABuildFetchesItBeforeAnythingIsWritten()
    {
        var switches = new List<string>();
        var row = Row(Surface("fp32", switches))!;

        Assert.NotNull(row.FetchChoiceAsync);

        var failure = await row.FetchChoiceAsync!(
            "uint8", new Progress<double>(), CancellationToken.None);

        Assert.Null(failure);
        Assert.Equal(["uint8"], switches);
    }

    /// <summary>And only then does the write land, on the build that was actually fetched.</summary>
    [Fact]
    public void TheWriteRecordsTheBuildRatherThanWhateverWasTyped()
    {
        var row = Row(Surface("fp32"))!;

        Assert.Equal(
            "uint8",
            row.Binding!.Write!(new D47Settings(), "uint8").Speech.LocalVoiceBuild);

        // A name this version does not publish resolves rather than being stored, so the file can
        // never come back naming something that will not load.
        Assert.Equal(
            KokoroAssets.DefaultBuildId,
            row.Binding!.Write!(new D47Settings(), "model_int2").Speech.LocalVoiceBuild);
    }
}
