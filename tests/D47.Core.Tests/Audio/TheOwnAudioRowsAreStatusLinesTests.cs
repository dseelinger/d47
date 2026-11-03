using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheOwnAudioRowsAreStatusLinesTests
{
    private static readonly string[] None = [];

    private static OwnAudioReport Report(IEnumerable<(string Key, int Count)> folders, string[]? ignored = null, string[]? skipped = null) =>
        OwnAudioReport.Of(folders.Select(f => new KeyValuePair<string, int>(f.Key, f.Count)), ignored ?? None, skipped ?? None);

    private static IEnumerable<(string, int)> ThirtyEightSituations()
    {
        yield return ("music/main-menu", 24);
        yield return ("music/docked", 23);
        yield return ("music/supercruise", 23);
        yield return ("music/normal-space", 21);
        yield return ("music/on-foot", 21);
        yield return ("music/docking-computer", 15);

        for (var index = 0; index < 32; index++)
        {
            yield return ($"music/extra-{index:00}", 1);
        }
    }

    [Fact]
    public void MusicNamesTheThreeBiggestFoldersAndCountsTheRest()
    {
        Assert.Equal(
            "159 files for 38 situations: main menu 24, docked 23, supercruise 23 and 35 more",
            Report(ThirtyEightSituations()).MusicLine());
    }

    [Fact]
    public void MusicWithThreeOrFewerFoldersNamesAllOfThem()
    {
        var report = Report([("music/docked", 3), ("music/on-foot", 1)]);

        Assert.Equal("4 files for 2 situations: docked 3, on foot 1", report.MusicLine());
    }

    [Fact]
    public void NoMusicSaysNoneOfYourOwn()
    {
        Assert.Equal("None of your own.", Report([]).MusicLine());
    }

    [Fact]
    public void CuesAndAlertsAreCountedTogether()
    {
        var report = Report([("cues/listening", 5), ("alerts/under-fire", 1)]);

        Assert.Equal("6 files for 2 cues: listening 5, under fire 1", report.CuesLine());
        Assert.Null(report.CuesHint());
    }

    [Fact]
    public void NoCuesOrBedsSayHowToAddOne()
    {
        var report = Report([]);

        Assert.Equal("None of your own. D47 plays its own cues.", report.CuesLine());
        Assert.Equal("A file in cues/<state> replaces one.", report.CuesHint());
        Assert.Equal("None of your own.", report.BedsLine());
        Assert.Equal("A file in beds/ adds one.", report.BedsHint());
    }

    [Fact]
    public void BedsAreCounted()
    {
        Assert.Equal("2 files", Report([("beds", 2)]).BedsLine());
    }

    [Fact]
    public void ThePageListsEveryFolderMostFilesFirstThenByName()
    {
        var report = Report([("music/b", 2), ("music/a", 2), ("music/c", 5), ("alerts/x", 1)]);

        Assert.Equal(["c", "a", "b"], report.In(OwnAudioReport.Music).Select(f => f.Name));
        Assert.Equal(["x"], report.In(OwnAudioReport.Alerts).Select(f => f.Name));
    }

    [Fact]
    public void TheWarningCountsWhatWasSkippedAndIgnored()
    {
        var skipped = Report([], skipped: ["a: bad"]);
        var both = Report([], ["x", "y"], ["a: bad"]);

        Assert.Equal("1 file skipped", skipped.WarningLabel());
        Assert.Equal("2 files ignored · 1 skipped", both.WarningLabel());
        Assert.Equal("x · and 2 more", both.WarningDetail());
        Assert.False(Report([]).HasWarning);
    }

    [Fact]
    public void TheFullTextStaysOnTheDropsSetting()
    {
        var capability = AudioCapability.Create(() => "full text", ownAudio: () => Report([]));

        Assert.Equal("full text", capability.Settings.Single(row => row.Key == AudioCapability.DropsKey).Binding!.Read(new D47.Core.Configuration.D47Settings()));
        Assert.True(capability.Settings.Single(row => row.Key == AudioCapability.DropsKey).DrawnElsewhere);
    }
}
