using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// What a prospector limpet found, spoken in the ring (Phase 18, "Prospector and core callouts").
/// </summary>
public sealed class ProspectorCallout : ICallout
{
    public string Id => "prospector";

    /// <summary>
    /// The best proportion seen for each material this session, so "the richest you have found" can be
    /// said without a table of what rich means.
    /// </summary>
    private readonly Dictionary<string, double> _best = new(StringComparer.Ordinal);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ProspectedAsteroid")
            {
                continue;
            }

            var rock = ProspectedRock.From(journalEvent);

            if (rock.Materials.Count == 0)
            {
                continue;
            }

            var richest = rock.Richest!;
            var isBest = !_best.TryGetValue(richest.Symbol, out var previous) || richest.Proportion > previous;

            // Recorded whether or not it is announced, so the backlog primes the session's high water marks
            // and the first live rock is measured against a real baseline rather than against nothing.
            if (isBest)
            {
                _best[richest.Symbol] = richest.Proportion;
            }

            if (context.IsPriming)
            {
                continue;
            }

            var line = $"{richest.Name}, {Percent(richest.Proportion)}";

            if (rock.Materials.Count > 1)
            {
                line += $", plus {string.Join(" and ", rock.Materials.Skip(1).Select(
                    material => $"{material.Name} at {Percent(material.Proportion)}"))}";
            }

            // Only where there is something to have beaten.
            if (isBest && previous > 0)
            {
                line += ". Best you have found this session";
            }

            yield return new Announcement(
                // Unique per rock, so the engine's cooldown never swallows a legitimate second prospect of
                // the same material.
                $"prospector.{journalEvent.Timestamp.Ticks}",
                line + ".");
        }
    }

    private static string Percent(double proportion) =>
        proportion.ToString("0.#", CultureInfo.InvariantCulture) + "%";
}

/// <summary>A core asteroid, which is the one prospect worth interrupting anything for (Phase 18).</summary>
public sealed class CoreAsteroidCallout : ICallout
{
    public string Id => "core-asteroid";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            // Nothing to prime: a core is an event rather than a condition, so there is no state to carry
            // forward — only a backlog that must not be read out.
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "ProspectedAsteroid")
            {
                continue;
            }

            var rock = ProspectedRock.From(journalEvent);

            if (rock.Motherlode is not { } motherlode)
            {
                continue;
            }

            // Routine, not Urgent, and that is a deliberate refusal.
            yield return new Announcement(
                $"core.{journalEvent.Timestamp.Ticks}",
                $"Core asteroid. {motherlode}.");
        }
    }
}

/// <summary>One material in a prospected rock.</summary>
/// <param name="Symbol">
/// Folded for identity, because Elite writes the same material two ways. 14 of the 27 materials in the
/// corpus appear under both spellings — <c>gallite</c> and <c>Gallite</c>, <c>gold</c> and <c>Gold</c>
/// — depending on whether <c>Name_Localised</c> was written, which is 41 raw spellings for 27
/// materials.
/// </param>
public sealed record ProspectedMaterial(string Symbol, string Name, double Proportion);

/// <summary>One <c>ProspectedAsteroid</c> event, read into the two things worth saying about it.</summary>
public sealed record ProspectedRock(IReadOnlyList<ProspectedMaterial> Materials)
{
    /// <summary>The core's material, where there is one.</summary>
    public string? Motherlode { get; init; }

    /// <summary>Richest first, which is the one a Commander wants said before the others.</summary>
    public ProspectedMaterial? Richest => Materials.Count > 0 ? Materials[0] : null;

    public static ProspectedRock From(JournalEvent journalEvent)
    {
        var materials = journalEvent
            .Items("Materials")
            .Select(row => new
            {
                Symbol = JournalJson.Symbol(row.String("Name")),
                Name = row.Named("Name"),
                Proportion = row.Double("Proportion") ?? 0,
            })
            .Where(row => row.Symbol is not null && row.Name is not null)
            .Select(row => new ProspectedMaterial(row.Symbol!, JournalJson.Spoken(row.Name)!, row.Proportion))
            .OrderByDescending(material => material.Proportion)
            .ToList();

        return new ProspectedRock(materials)
        {
            Motherlode = JournalJson.Spoken(journalEvent.Named("MotherlodeMaterial")),
        };
    }
}
