using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class CueLibraryTests
{
    [Fact]
    public void EveryLoopStateHasAShippedCue()
    {
        var library = CueLibrary.Load();

        foreach (var state in Enum.GetValues<LoopState>())
        {
            var clip = library.For(state);
            Assert.Equal(state.ToString().ToLowerInvariant(), clip.Name);
            Assert.True(clip.Pcm.Length > 0, $"the {state} cue is empty");
        }
    }

    /// <summary>Adding a loop state without committing its cue has to fail here rather than at runtime, where it would present as one state that silently never makes a sound.</summary>
    [Fact]
    public void AMissingCueFailsLoudlyAndNamesTheState()
    {
        var source = EditedCueSource.Without(LoopState.Thinking);

        var error = Assert.Throws<CueSetException>(() => CueLibrary.Load(source));

        Assert.Contains("Thinking", error.Message, StringComparison.Ordinal);
        Assert.Contains("gen-cues.py", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACueNamedForNoLoopStateAlsoFails()
    {
        var source = EditedCueSource.Plus("hyperspace");

        var error = Assert.Throws<CueSetException>(() => CueLibrary.Load(source));

        Assert.Contains("hyperspace", error.Message, StringComparison.Ordinal);
    }

    /// <summary>The loader converts drop-ins; a shipped clip in another format is a generator fault.</summary>
    [Fact]
    public void EveryShippedFileIsAlreadyInTheStandardFormat()
    {
        var assembly = typeof(CueLibrary).Assembly;
        var shipped = assembly.GetManifestResourceNames()
            .Where(name => new[] { "D47.Core.Cues.", "D47.Core.Alerts.", "D47.Core.Beds.", "D47.Core.Music." }
                .Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.NotEmpty(shipped);

        foreach (var name in shipped)
        {
            using var stream = WavReader.Open(assembly.GetManifestResourceStream(name)!, name);
            Assert.True(stream.Format == AudioFormat.Standard, $"{name} is {stream.Format}; regenerate it with tools/gen-cues.py");
        }
    }

    [Fact]
    public void EveryShippedClipIsInTheOneFormatTheQueueCarries()
    {
        var library = CueLibrary.Load();

        foreach (var state in Enum.GetValues<LoopState>())
        {
            Assert.Equal(AudioFormat.Standard, library.For(state).Format);
        }

        Assert.Equal(AudioFormat.Standard, library.Bed().Format);
    }

    [Fact]
    public void TheDefaultBedShipsAndIsLongEnoughToLoopWithoutFlutter()
    {
        var library = CueLibrary.Load();

        var bed = library.Bed();

        Assert.Equal(CueLibrary.DefaultBed, bed.Name);
        Assert.True(
            bed.Duration > TimeSpan.FromSeconds(2),
            $"a {bed.Duration.TotalSeconds:0.0}s bed restarts often enough to be heard as a loop");
    }

    [Fact]
    public void OnlyOneBedShips()
    {
        var error = Assert.Throws<CueSetException>(() => CueLibrary.Load(new EditedCueSource(names =>
            names.Concat(["D47.Core.Beds.thinking-pulse"]))));

        Assert.Contains("beds/thinking-pulse", error.Message, StringComparison.Ordinal);
    }
}
