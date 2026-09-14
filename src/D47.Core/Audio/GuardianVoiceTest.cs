namespace D47.Core.Audio;

/// <summary>
/// Which clip the Guardian voice group's Test button plays, given the catalogue flags, whether the
/// ship voice has a free sample and whether an audition of it was already paid for this session
/// (#226). A pure function of that state — it never touches a provider, which is what lets it be
/// tested without one and is what guarantees <see cref="Synthesize"/> is only ever picked for a
/// provider that costs nothing.
/// </summary>
public static class GuardianVoiceTest
{
    public enum Source
    {
        /// <summary>The provider is not billed: synthesise the ship's own audition line.</summary>
        Synthesize,

        /// <summary>The provider is billed but offers a free sample of this voice.</summary>
        FreeSample,

        /// <summary>The provider is billed, and an audition of this voice was already paid for.</summary>
        CachedAudition,

        /// <summary>Nothing free is available: the bundled stand-in clip.</summary>
        StandIn,
    }

    public static Source SourceFor(TtsProviderInfo provider, bool hasFreeSample, bool hasCachedAudition)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Id == TtsProviderCatalog.NoneId)
        {
            return Source.StandIn;
        }

        if (!provider.Billed)
        {
            return Source.Synthesize;
        }

        if (provider.OffersFreePreviews && hasFreeSample)
        {
            return Source.FreeSample;
        }

        return hasCachedAudition ? Source.CachedAudition : Source.StandIn;
    }
}
