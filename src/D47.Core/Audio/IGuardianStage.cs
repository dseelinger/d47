namespace D47.Core.Audio;

/// <summary>One Guardian effect running on one channel of a clip as its samples arrive.</summary>
internal interface IGuardianStage
{
    /// <summary>Takes the next samples and appends every output sample that no later input can change.</summary>
    void Push(ReadOnlySpan<double> input, List<double> output);

    /// <summary>Appends the rest: samples held back for look-ahead, then any tail.</summary>
    void Finish(List<double> output);
}
