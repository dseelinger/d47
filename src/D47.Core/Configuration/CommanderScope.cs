namespace D47.Core.Configuration;

/// <summary>
/// The two layers of the settings file, read as one and written back as two (Phase 44, "The split is
/// per row and per store, declared rather than inferred").
/// </summary>
public static class CommanderScope
{
    /// <summary>
    /// The settings as this Commander sees them: the installation's, with their own values laid over
    /// the fields that are theirs.
    /// </summary>
    public static D47Settings Project(D47Settings stored, string? fid)
    {
        if (OverlayFor(stored, fid) is not { } overlay)
        {
            return stored;
        }

        return stored with
        {
            CommanderGender = Read(overlay.CommanderGender, stored.CommanderGender),
            Llm = stored.Llm with
            {
                AboutMe = Read(overlay.AboutMe, stored.Llm.AboutMe),
                CharacterSheet = Read(overlay.CharacterSheet, stored.Llm.CharacterSheet),
                Scenario = Read(overlay.Scenario, stored.Llm.Scenario),
                ScenarioAudience = overlay.ScenarioAudience ?? stored.Llm.ScenarioAudience,
            },
            Persona = stored.Persona with
            {
                ShipCoreShip = overlay.ShipCoreShip ?? stored.Persona.ShipCoreShip,
            },
            Callouts = stored.Callouts with
            {
                HomeSystem = Read(overlay.HomeSystem, stored.Callouts.HomeSystem),
            },
            CareerRatings = stored.CareerRatings with
            {
                Combat = overlay.CombatRating ?? stored.CareerRatings.Combat,
                Trading = overlay.TradingRating ?? stored.CareerRatings.Trading,
                Mining = overlay.MiningRating ?? stored.CareerRatings.Mining,
                Exploration = overlay.ExplorationRating ?? stored.CareerRatings.Exploration,
                Exobiology = overlay.ExobiologyRating ?? stored.CareerRatings.Exobiology,
                Passengers = overlay.PassengersRating ?? stored.CareerRatings.Passengers,
                SearchAndRescue = overlay.SearchAndRescueRating ?? stored.CareerRatings.SearchAndRescue,
                Crime = overlay.CrimeRating ?? stored.CareerRatings.Crime,
            },
        };
    }

    /// <summary>The document to write after a change made against the projected view.</summary>
    public static D47Settings Persist(
        D47Settings stored,
        D47Settings effective,
        D47Settings next,
        string? fid,
        string? name)
    {
        if (fid is not { Length: > 0 })
        {
            return next;
        }

        // The installation's copy of the Commander fields is never written through a Commander: whatever the
        // row did to them is moved into the overlay below.
        var install = next with
        {
            CommanderGender = stored.CommanderGender,
            Llm = next.Llm with
            {
                AboutMe = stored.Llm.AboutMe,
                CharacterSheet = stored.Llm.CharacterSheet,
                Scenario = stored.Llm.Scenario,
                ScenarioAudience = stored.Llm.ScenarioAudience,
            },
            Persona = next.Persona with
            {
                ShipCoreShip = stored.Persona.ShipCoreShip,
            },
            Callouts = next.Callouts with
            {
                HomeSystem = stored.Callouts.HomeSystem,
            },
            CareerRatings = stored.CareerRatings,
        };

        var overlay = OverlayFor(stored, fid) ?? new CommanderSettings { CommanderFid = fid };
        var updated = overlay;

        if (!string.Equals(next.Llm.AboutMe, effective.Llm.AboutMe, StringComparison.Ordinal))
        {
            updated = updated with { AboutMe = Written(next.Llm.AboutMe) };
        }

        if (!string.Equals(next.Llm.CharacterSheet, effective.Llm.CharacterSheet, StringComparison.Ordinal))
        {
            updated = updated with { CharacterSheet = Written(next.Llm.CharacterSheet) };
        }

        if (!string.Equals(next.Llm.Scenario, effective.Llm.Scenario, StringComparison.Ordinal))
        {
            updated = updated with { Scenario = Written(next.Llm.Scenario) };
        }

        if (next.Llm.ScenarioAudience != effective.Llm.ScenarioAudience)
        {
            updated = updated with { ScenarioAudience = next.Llm.ScenarioAudience };
        }

        if (next.Persona.ShipCoreShip != effective.Persona.ShipCoreShip)
        {
            updated = updated with { ShipCoreShip = next.Persona.ShipCoreShip };
        }

        if (!string.Equals(next.Callouts.HomeSystem, effective.Callouts.HomeSystem, StringComparison.Ordinal))
        {
            updated = updated with { HomeSystem = Written(next.Callouts.HomeSystem) };
        }

        if (!string.Equals(next.CommanderGender, effective.CommanderGender, StringComparison.Ordinal))
        {
            updated = updated with { CommanderGender = Written(next.CommanderGender) };
        }

        if (next.CareerRatings.Combat != effective.CareerRatings.Combat)
        {
            updated = updated with { CombatRating = next.CareerRatings.Combat };
        }

        if (next.CareerRatings.Trading != effective.CareerRatings.Trading)
        {
            updated = updated with { TradingRating = next.CareerRatings.Trading };
        }

        if (next.CareerRatings.Mining != effective.CareerRatings.Mining)
        {
            updated = updated with { MiningRating = next.CareerRatings.Mining };
        }

        if (next.CareerRatings.Exploration != effective.CareerRatings.Exploration)
        {
            updated = updated with { ExplorationRating = next.CareerRatings.Exploration };
        }

        if (next.CareerRatings.Exobiology != effective.CareerRatings.Exobiology)
        {
            updated = updated with { ExobiologyRating = next.CareerRatings.Exobiology };
        }

        if (next.CareerRatings.Passengers != effective.CareerRatings.Passengers)
        {
            updated = updated with { PassengersRating = next.CareerRatings.Passengers };
        }

        if (next.CareerRatings.SearchAndRescue != effective.CareerRatings.SearchAndRescue)
        {
            updated = updated with { SearchAndRescueRating = next.CareerRatings.SearchAndRescue };
        }

        if (next.CareerRatings.Crime != effective.CareerRatings.Crime)
        {
            updated = updated with { CrimeRating = next.CareerRatings.Crime };
        }

        if (updated == overlay)
        {
            // Nothing of this Commander's moved.
            return install == stored ? stored : install;
        }

        // The name is written beside the id for a person reading the file, and only when the entry is being
        // written anyway: a file that differs from disk by a name the journal just restated is not worth a
        // write.
        updated = updated with { CommanderName = name ?? overlay.CommanderName };

        return install with
        {
            Commanders =
            [
                .. stored.Commanders.Where(entry => !IsFor(entry, fid)),
                updated,
            ],
        };
    }

    /// <summary>
    /// This Commander's document with one of their own fields forgotten — one entry per field they
    /// could have set (#61).
    /// </summary>
    public static IReadOnlyList<D47Settings> WithOneFieldForgotten(D47Settings stored, string? fid)
    {
        if (fid is not { Length: > 0 } || OverlayFor(stored, fid) is not { } overlay)
        {
            return [];
        }

        return
        [
            Without(stored, fid, overlay with { AboutMe = null }),
            Without(stored, fid, overlay with { CharacterSheet = null }),
            Without(stored, fid, overlay with { Scenario = null }),
            Without(stored, fid, overlay with { ScenarioAudience = null }),
            Without(stored, fid, overlay with { ShipCoreShip = null }),
            Without(stored, fid, overlay with { HomeSystem = null }),
            Without(stored, fid, overlay with { CommanderGender = null }),
            Without(stored, fid, overlay with { CombatRating = null }),
            Without(stored, fid, overlay with { TradingRating = null }),
            Without(stored, fid, overlay with { MiningRating = null }),
            Without(stored, fid, overlay with { ExplorationRating = null }),
            Without(stored, fid, overlay with { ExobiologyRating = null }),
            Without(stored, fid, overlay with { PassengersRating = null }),
            Without(stored, fid, overlay with { SearchAndRescueRating = null }),
            Without(stored, fid, overlay with { CrimeRating = null }),
        ];
    }

    /// <summary>
    /// The document with this Commander's entry replaced — or removed outright once nothing of theirs
    /// is left in it, so forgetting the last field leaves no husk behind carrying only an id and a
    /// name.
    /// </summary>
    private static D47Settings Without(D47Settings stored, string fid, CommanderSettings updated)
    {
        var others = stored.Commanders.Where(entry => !IsFor(entry, fid)).ToList();

        var empty = updated.AboutMe is null
                    && updated.CharacterSheet is null
                    && updated.Scenario is null
                    && updated.ScenarioAudience is null
                    && updated.ShipCoreShip is null
                    && updated.HomeSystem is null
                    && updated.CommanderGender is null
                    && updated.CombatRating is null
                    && updated.TradingRating is null
                    && updated.MiningRating is null
                    && updated.ExplorationRating is null
                    && updated.ExobiologyRating is null
                    && updated.PassengersRating is null
                    && updated.SearchAndRescueRating is null
                    && updated.CrimeRating is null;

        return stored with { Commanders = empty ? [.. others] : [.. others, updated] };
    }

    private static CommanderSettings? OverlayFor(D47Settings stored, string? fid) =>
        fid is { Length: > 0 } ? stored.Commanders.FirstOrDefault(entry => IsFor(entry, fid)) : null;

    private static bool IsFor(CommanderSettings entry, string fid) =>
        string.Equals(entry.CommanderFid, fid, StringComparison.Ordinal);

    /// <summary>Null reads through; empty reads as nothing; text reads as text.</summary>
    private static string? Read(string? overlay, string? install) =>
        overlay switch
        {
            null => install,
            { Length: 0 } => null,
            _ => overlay,
        };

    /// <summary>A cleared value is recorded as blank, never as unset.</summary>
    private static string Written(string? value) => value ?? string.Empty;
}
