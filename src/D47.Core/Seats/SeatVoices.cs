namespace D47.Core.Seats;

/// <summary>Which voice a crew seat speaks in.</summary>
public static class SeatVoices
{
    /// <summary>The key a role's voice is stored under: its name in camel case, as the settings file writes dictionary keys.</summary>
    public static string KeyOf(CrewRole role) =>
        $"{char.ToLowerInvariant(role.ToString()[0])}{role.ToString()[1..]}";

    /// <summary>
    /// The seat's own voice if it belongs to <paramref name="provider"/>, then the role's voice, each only
    /// where <paramref name="offered"/> accepts it; null when neither applies.
    /// </summary>
    /// <param name="roleVoices">The provider's role voices, keyed by <see cref="KeyOf"/>.</param>
    public static string? Resolve(
        CrewSeat seat,
        string provider,
        IReadOnlyDictionary<string, string> roleVoices,
        Func<string, bool> offered)
    {
        if (seat.Voice is { } own
            && string.Equals(own.Provider, provider, StringComparison.OrdinalIgnoreCase)
            && offered(own.VoiceId))
        {
            return own.VoiceId;
        }

        return seat.Role != CrewRole.Custom
            && roleVoices.TryGetValue(KeyOf(seat.Role), out var voice)
            && !string.IsNullOrWhiteSpace(voice)
            && offered(voice)
                ? voice
                : null;
    }
}
