using D47.Core.Audio;

namespace D47.Core.Persona;

/// <summary>What the carrier's captain and tower lines share: the distance to the carrier and the brief's notes.</summary>
public static class CarrierComms
{
    /// <summary>How far the carrier's crew can be heard.</summary>
    public const double RangeLightYears = LinkSignal.LostLightYears;

    /// <summary>The out-of-range refusal for <paramref name="who"/>, who cannot hear the Commander from <paramref name="away"/>.</summary>
    public static string OutOfRange(double away, string who) =>
        $"The carrier is {away:N0} light years away, beyond comms range. {who} can't hear you from here.";

    /// <summary>Zero in the same system; null when there is nothing to ask or the answer fails.</summary>
    public static async Task<double?> DistanceAsync(
        string? here,
        string carrierSystem,
        Func<string, string, CancellationToken, Task<double?>>? distance,
        CancellationToken cancellationToken)
    {
        if (here is not { Length: > 0 })
        {
            return null;
        }

        if (string.Equals(here, carrierSystem, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (distance is null)
        {
            return null;
        }

        try
        {
            return await distance(here, carrierSystem, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The Commander's notes on a carrier character as a brief paragraph, or empty without notes.</summary>
    public static string NotesBlock(string? notes) =>
        notes?.Trim() is { Length: > 0 } text
            ? $"\n\nThe Commander has told you what you are like: {text}"
            : string.Empty;
}
