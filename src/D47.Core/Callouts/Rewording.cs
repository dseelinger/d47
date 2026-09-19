using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// Says a briefed announcement in character or as written, and logs why whenever it is said as written
/// (#290).
/// </summary>
public sealed class Rewording(RewordChance chance, ILogger? logger)
{
    /// <summary>How long a line may spend being written, retry included, before the authored one is used.</summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>The announcement to speak, or null for silence.</summary>
    /// <param name="ask">Asks the model for one line from a brief and an instruction.</param>
    public async Task<Announcement?> VaryAsync(
        Announcement announcement,
        bool hasModel,
        bool personalityEnabled,
        int rewordPercent,
        Func<ShipFacts> facts,
        string? commanderName,
        Func<FlavourBrief, string, CancellationToken, Task<FlavourReply>> ask)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(ask);

        // Read at most once, so the ship cannot change between a line, its retry and its fallback (#338).
        var ship = new Lazy<ShipFacts>(facts);

        if (FlavourBriefs.For(announcement, personalityEnabled: true) is null)
        {
            return announcement;
        }

        if (!hasModel)
        {
            return AsWritten(announcement, "no model", ship, commanderName, checkFacts: false);
        }

        if (FlavourBriefs.For(announcement, personalityEnabled) is not { } brief)
        {
            return AsWritten(announcement, "personality off", ship, commanderName, checkFacts: false);
        }

        // Decided before any model call, so the as-written side makes none (#214).
        if (!chance.ShouldReword(announcement, rewordPercent))
        {
            return AsWritten(announcement, "roll", ship, commanderName, checkFacts: true);
        }

        var snapshot = ship.Value;

        using var budget = new CancellationTokenSource(Budget);

        var reply = await ask(brief, brief.Instruction, budget.Token).ConfigureAwait(false);

        var said = await ContradictedClaims.SayableAsync(
            reply.Line,
            snapshot,
            async contradiction =>
                (await ask(brief, $"{brief.Instruction} {contradiction.Correction}", budget.Token)
                    .ConfigureAwait(false)).Line,
            logger,
            announcement.Key).ConfigureAwait(false);

        if (said is not null)
        {
            return announcement with { Text = said };
        }

        var reason = reply.Miss switch
        {
            FlavourMiss.None => "rejected",
            FlavourMiss.Cancelled => "timed out",
            FlavourMiss.NoModel => "no model",
            _ => reply.Message is { Length: > 0 } message ? $"failed: {message}" : "failed",
        };

        return AsWritten(announcement, reason, ship, commanderName, checkFacts: true);
    }

    /// <summary>
    /// The authored line, checked against the ship on the paths that asked a model, and wherever a
    /// carrier's authored line replaced Frontier's.
    /// </summary>
    private Announcement? AsWritten(
        Announcement announcement,
        string reason,
        Lazy<ShipFacts> ship,
        string? commanderName,
        bool checkFacts)
    {
        var written = CarrierOwnerLines.AsWritten(announcement, commanderName);

        if ((checkFacts || !ReferenceEquals(written, announcement))
            && ContradictedClaims.Sayable(written.Text, ship.Value, logger, announcement.Key) is null)
        {
            return null;
        }

        logger?.LogInformation("{Key} was spoken as written: {Reason}.", announcement.Key, reason);
        return written;
    }
}
