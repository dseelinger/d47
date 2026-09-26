using D47.Core.Audio;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A cue, alert or bed with files of the Commander's plays one of them, dealt from a shuffle.</summary>
public class ASoundIsPickedFromItsFolderEachTimeTests
{
    [Fact]
    public void FiveListeningFilesAreEachHeardOnceBeforeAnyIsHeardTwice()
    {
        var library = Load(new Random(47), Cue("listening", "a", "b", "c", "d", "e"));

        for (var round = 0; round < 4; round++)
        {
            var heard = Enumerable.Range(0, 5).Select(_ => library.For(LoopState.Listening).Name).ToList();

            Assert.Equal(["a", "b", "c", "d", "e"], heard.Order());
        }
    }

    [Fact]
    public void NoFileIsHeardTwiceInARowAcrossAReshuffle()
    {
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var library = Load(new Random(seed), Cue("listening", "a", "b", "c"));
            var heard = Enumerable.Range(0, 60).Select(_ => library.For(LoopState.Listening).Name).ToList();

            Assert.All(heard.Zip(heard.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        }
    }

    [Fact]
    public void AnEmptyStateFolderPlaysTheShippedCue()
    {
        var library = Load(new Random(1), Cue("listening", "beep"));

        Assert.Equal("beep", library.For(LoopState.Listening).Name);
        Assert.Equal("thinking", library.For(LoopState.Thinking).Name);
    }

    [Fact]
    public void AFileInUnderFireReplacesTheUnderFireAlert()
    {
        var library = Load(new Random(1), "D47.Core.Alerts.under-fire.klaxon");

        Assert.Equal("klaxon", library.For(AlertCue.UnderFire).Name);
        Assert.Equal("piracy", library.For(AlertCue.Piracy).Name);
    }

    [Fact]
    public void TheBedIsPickedFromTheBedsFolderEachTurn()
    {
        var library = Load(new Random(3), "D47.Core.Beds.engine-room", "D47.Core.Beds.hangar");

        var heard = Enumerable.Range(0, 4).Select(_ => library.Bed().Name).ToList();

        Assert.Equal(["engine-room", "engine-room", "hangar", "hangar"], heard.Order());
    }

    [Fact]
    public void AnEmptyBedsFolderPlaysTheShippedBed()
    {
        Assert.Equal(CueLibrary.DefaultBed, Load(new Random(1)).Bed().Name);
    }

    [Fact]
    public void ASettingsFileThatStillPicksABedLoads()
    {
        using var install = new TempInstall();
        File.WriteAllText(
            install.Paths.SettingsFile,
            """{ "schemaVersion": 1, "speech": { "thinkingBedEnabled": false, "thinkingBed": "thinking-pulse" } }""");

        var settings = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance).Load();

        Assert.False(settings.Speech.ThinkingBedEnabled);
    }

    private static string[] Cue(string state, params string[] files) =>
        [.. files.Select(file => $"D47.Core.Cues.{state}.{file}")];

    private static CueLibrary Load(Random random, params string[] names) =>
        CueLibrary.Load(null, random, new EmbeddedCueSource(typeof(CueLibrary).Assembly), new DropIns(names));

    /// <summary>A Commander's folder with the named files in it, each a short silent clip.</summary>
    private sealed class DropIns(string[] names) : ICueSource
    {
        public IEnumerable<string> Names => names;

        public bool Required => false;

        public Stream Open(string name) => throw new NotSupportedException();

        public AudioClip Decode(string name, string clipName) =>
            new(clipName, new byte[96], AudioFormat.Standard);
    }
}
