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

    /// <summary>
    /// The gate that gives #20 its teeth: adding a loop state without committing its cue has
    /// to fail here rather than at runtime, where it would present as one state that silently
    /// never makes a sound.
    /// </summary>
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

    [Fact]
    public void EveryShippedClipIsInTheOneFormatTheQueueCarries()
    {
        var library = CueLibrary.Load();

        foreach (var state in Enum.GetValues<LoopState>())
        {
            Assert.Equal(AudioFormat.Standard, library.For(state).Format);
        }

        foreach (var bed in library.BedNames)
        {
            Assert.Equal(AudioFormat.Standard, library.Bed(bed).Format);
        }
    }

    [Fact]
    public void TheDefaultBedShipsAndIsLongEnoughToLoopWithoutFlutter()
    {
        var library = CueLibrary.Load();

        var bed = library.Bed(CueLibrary.DefaultBed);

        Assert.Equal(CueLibrary.DefaultBed, bed.Name);
        Assert.True(
            bed.Duration > TimeSpan.FromSeconds(2),
            $"a {bed.Duration.TotalSeconds:0.0}s bed restarts often enough to be heard as a loop");
    }

    [Fact]
    public void AnUnknownBedNameFallsBackToTheDefaultRatherThanGoingSilent()
    {
        var library = CueLibrary.Load();

        Assert.Equal(CueLibrary.DefaultBed, library.Bed("no-such-bed").Name);
    }
}
