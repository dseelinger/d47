using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// What a prospector limpet found, spoken in the ring (list.md Phase 18, "Prospector and core
/// callouts"). Route planning gets the Commander to the ring; this is the part after they arrive,
/// and the whole value of it is not having to look at a panel while flying between rocks.
/// <para>
/// <b>Two callouts rather than one, because a core and a percentage are not the same appetite.</b>
/// A motherlode is <b>3 of 1,633</b> prospects measured — twice a session at best, and the thing
/// worth interrupting anything for. A percentage arrives every <b>48 seconds</b> at the median. A
/// Commander who finds the running commentary chatty must be able to silence it without also
/// silencing the one announcement they are actually mining for, which is why these are two ids with
/// two settings rows.
/// </para>
/// <para>
/// <b>The obvious signal is a trap, and it is measured.</b> Elite grades every rock
/// <c>Material Content: Low / Medium / High</c>, and it does not mean what a miner wants it to mean:
/// across 1,633 prospects the best material in a Low rock has a median of 19.9% against 20.3% for
/// High, the maxima are 66.7% and 66.4%, and <b>45% of the rocks carrying a material at 40% or more
/// are labelled Low</b>. The corpus holds a 58.3% Platinum rock graded Low. It grades the
/// engineering-material content, which is a different question from the commodity proportion being
/// refined — so this callout ignores <c>Content</c> entirely rather than passing on a label that
/// would send a Commander past the best rock in the cluster.
/// </para>
/// </summary>
public sealed class ProspectorCallout : ICallout
{
    public string Id => "prospector";

    /// <summary>
    /// The best proportion seen for each material this session, so "the richest you have found" can
    /// be said without a table of what rich means.
    /// <para>
    /// <b>A fixed percentage would be wrong per material and quietly so.</b> Platinum runs a median
    /// of 26.6% and a 90th percentile of 55.7%; Water runs 7.1% and 20.8%. "Tell me about anything
    /// over 25%" is routine for one and near-unheard-of for the other, and there is no licence-clean
    /// table of per-material distributions to thredhold against. What the Commander has actually
    /// seen this session needs no source at all and adapts to whatever they are mining.
    /// </para>
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

            // Recorded whether or not it is announced, so the backlog primes the session's high
            // water marks and the first live rock is measured against a real baseline rather than
            // against nothing.
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

            // Only where there is something to have beaten. The first rock of a session is not
            // "the best you have seen" — it is the only one.
            if (isBest && previous > 0)
            {
                line += ". Best you have found this session";
            }

            yield return new Announcement(
                // Unique per rock, so the engine's cooldown never swallows a legitimate second
                // prospect of the same material. Repetition is bounded by the limpet, not by a timer.
                $"prospector.{journalEvent.Timestamp.Ticks}",
                line + ".");
        }
    }

    private static string Percent(double proportion) =>
        proportion.ToString("0.#", CultureInfo.InvariantCulture) + "%";
}

/// <summary>
/// A core asteroid, which is the one prospect worth interrupting anything for (list.md Phase 18).
/// <para>
/// Kept apart from <see cref="ProspectorCallout"/> so it can be left on when the running commentary
/// is turned off. It is also the rarest thing d47 announces — 3 in 1,633 prospects — so its cue is
/// worth hearing and its line is worth cutting across whatever else was being said.
/// </para>
/// </summary>
public sealed class CoreAsteroidCallout : ICallout
{
    public string Id => "core-asteroid";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            // Nothing to prime: a core is an event rather than a condition, so there is no state
            // to carry forward — only a backlog that must not be read out.
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

            // Routine, not Urgent, and that is a deliberate refusal. Urgent speaks over the top of
            // whatever is being said and is reserved for danger and fuel; a core asteroid is
            // exciting and is not a safety matter, so announcing one across a hull warning would
            // get the priority exactly backwards. It stands out by being rare and by its wording.
            yield return new Announcement(
                $"core.{journalEvent.Timestamp.Ticks}",
                $"Core asteroid. {motherlode}.");
        }
    }
}

/// <summary>One material in a prospected rock.</summary>
/// <param name="Symbol">
/// Folded for identity, because Elite writes the same material two ways. 14 of the 27 materials in
/// the corpus appear under both spellings — <c>gallite</c> and <c>Gallite</c>, <c>gold</c> and
/// <c>Gold</c> — depending on whether <c>Name_Localised</c> was written, which is 41 raw spellings
/// for 27 materials. Tracking a session best on the raw name would keep two high water marks for one
/// material and announce a personal best that was not one.
/// </param>
public sealed record ProspectedMaterial(string Symbol, string Name, double Proportion);

/// <summary>
/// One <c>ProspectedAsteroid</c> event, read into the two things worth saying about it.
/// <para>
/// All 1,633 events measured carry <c>Materials</c>, <c>Content</c> and <c>Remaining</c>; a rock
/// holds one, two or three materials and never more.
/// </para>
/// </summary>
public sealed record ProspectedRock(IReadOnlyList<ProspectedMaterial> Materials)
{
    /// <summary>
    /// The core's material, where there is one. <c>MotherlodeMaterial_Localised</c> was present on
    /// only <b>one of the three</b> motherlodes in the corpus, so the localised name cannot be
    /// relied on and the fallback is the symbol Elite writes — which is already a display name here
    /// (<c>Alexandrite</c>, <c>Painite</c>) rather than a slug.
    /// </summary>
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
