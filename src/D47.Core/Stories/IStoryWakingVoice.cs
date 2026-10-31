using D47.Core.Persona;

namespace D47.Core.Stories;

/// <summary>What <see cref="StoryWaking"/> says and changes outside Core. Called on the tick thread, except <see cref="SayAside"/>, which runs on the pool.</summary>
public interface IStoryWakingVoice
{
    /// <summary>Whether a narrated scan line is waiting to be said.</summary>
    bool IsNarratingScan { get; }

    /// <summary>Whether a story's opening is still being said.</summary>
    bool IsSayingOpening { get; }

    /// <summary>Says a picked story's opening lines, then its narrated beacon scan. Must not block.</summary>
    void NarrateStart(StoryOpeningDue? opening, StoryScanDue? scan, string? commander);

    /// <summary>Says the current story's ending when it is due.</summary>
    void PostEndingIfDue(string? commander);

    /// <summary>Brings the story's core aboard for a waking and says its line.</summary>
    void CoresWoke(CoreWaking waking, Persona.Persona? storyCore);

    /// <summary>Puts the chosen core aboard again when the story's hold on the cores has changed.</summary>
    void ReadmitIfTheHoldChanged();

    /// <summary>Says a line aside.</summary>
    void SayAside(string line);
}
