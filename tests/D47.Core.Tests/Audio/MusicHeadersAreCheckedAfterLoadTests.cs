using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Loading the library does not open music files; <see cref="CueLibrary.CheckMusic"/> does, later.</summary>
public class MusicHeadersAreCheckedAfterLoadTests
{
    [Fact]
    public void LoadOpensNoMusicFile()
    {
        var source = new Music(isPack: false, "docked.good", "docked.bad", "supercruise.other");

        var library = Load(source);

        Assert.Empty(source.Opened);
        Assert.Equal(["good", "bad"], library.Music("docked").Select(track => track.Name));
    }

    [Fact]
    public void AnUnreadableOwnFileIsSkippedAndLeavesTheSituationsTracks()
    {
        var source = new Music(isPack: false, "docked.good", "docked.bad", "supercruise.bad2");
        source.Unreadable.Add("bad");
        source.Unreadable.Add("bad2");
        var library = Load(source);

        var dropped = library.CheckMusic();

        Assert.Equal(2, dropped);
        Assert.Equal(["good"], library.Music("docked").Select(track => track.Name));
        Assert.Empty(library.Music("supercruise"));
        Assert.Equal(["bad: unreadable", "bad2: unreadable"], library.Skipped);
        Assert.Equal(3, source.Opened.Count);
    }

    [Fact]
    public void ASkippedOwnFileIsTakenOutOfTheCounts()
    {
        var source = new Music(isPack: false, "docked.good", "docked.bad", "supercruise.bad2");
        source.Unreadable.Add("bad");
        source.Unreadable.Add("bad2");
        var library = Load(source);

        Assert.Equal(3, library.CustomCount);

        library.CheckMusic();

        Assert.Equal(1, library.CustomCount);
        Assert.Equal([new KeyValuePair<string, int>("music/docked", 1)], library.FolderCounts);
    }

    [Fact]
    public void AnUnreadablePackFileIsLoggedAndNotListed()
    {
        var source = new Music(isPack: true, "docked.good", "docked.bad");
        source.Unreadable.Add("bad");
        var logger = new Recording();
        var library = CueLibrary.Load(logger, new EmbeddedCueSource(typeof(CueLibrary).Assembly), source);

        library.CheckMusic();

        Assert.Equal(["good"], library.Music("docked").Select(track => track.Name));
        Assert.Empty(library.Skipped);
        Assert.Contains(logger.Warnings, line => line.Contains("Skipped a music pack file", StringComparison.Ordinal) && line.Contains("bad", StringComparison.Ordinal));
    }

    [Fact]
    public void CuesAndAlertsAreTheSameAsBefore()
    {
        var library = Load(new Music(isPack: false, "docked.good"));
        var plain = CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly));

        Assert.Equal(plain.For(LoopState.Listening).Name, library.For(LoopState.Listening).Name);
        Assert.Equal(plain.For(Enum.GetValues<AlertCue>()[0]).Name, library.For(Enum.GetValues<AlertCue>()[0]).Name);
        Assert.Equal(plain.Bed().Name, library.Bed().Name);
    }

    [Fact]
    public void ARetiredLibraryIsNotChangedByAnUnfinishedCheck()
    {
        var source = new Music(isPack: false, "docked.good", "docked.bad");
        source.Unreadable.Add("bad");
        var library = Load(source);

        library.Retire();

        Assert.Equal(0, library.CheckMusic());
        Assert.Empty(library.Skipped);
        Assert.Equal(2, library.Music("docked").Count);
    }

    [Fact]
    public void TheCheckRunsOnce()
    {
        var source = new Music(isPack: false, "docked.good");
        var library = Load(source);

        library.CheckMusic();
        library.CheckMusic();

        Assert.Single(source.Opened);
    }

    [Fact]
    public async Task TheSkippedListCanBeReadWhileTheCheckRuns()
    {
        var names = Enumerable.Range(0, 200).Select(index => $"docked.t{index}").ToArray();
        var source = new Music(isPack: false, names);

        foreach (var name in names)
        {
            source.Unreadable.Add(name["docked.".Length..]);
        }

        var library = Load(source);
        var check = Task.Run(library.CheckMusic);

        while (!check.IsCompleted)
        {
            _ = library.Skipped.Count + library.FolderCounts.Count + library.Music("docked").Count;
        }

        Assert.Equal(200, await check);
        Assert.Equal(200, library.Skipped.Count);
    }

    private static CueLibrary Load(Music source) =>
        CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly), source);

    private sealed class Music(bool isPack, params string[] names) : ICueSource
    {
        public List<string> Opened { get; } = [];

        public HashSet<string> Unreadable { get; } = [];

        public IEnumerable<string> Names => names.Select(name => CueLibrary.MusicPrefix + name);

        public bool Required => false;

        public bool IsPack => isPack;

        public Stream Open(string name) => throw new NotSupportedException();

        public IPcmStream Stream(string name, string clipName)
        {
            lock (Opened)
            {
                Opened.Add(clipName);
            }

            return Unreadable.Contains(clipName) ? throw new CueSetException("unreadable") : new Silence();
        }
    }

    private sealed class Silence : IPcmStream
    {
        public int Read(Span<byte> buffer) => 0;

        public TimeSpan? Length => null;

        public void Seek(TimeSpan to)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class Recording : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
