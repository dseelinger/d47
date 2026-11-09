namespace D47.Core.Configuration;

/// <summary>
/// The two layers of the settings file, read as one and written back as two (Phase 44, "The split is
/// per row and per store, declared rather than inferred").
/// </summary>
public static class CommanderScope
{
    /// <summary>
    /// The settings as this Commander sees them: the installation's, with the fields that are theirs
    /// taken from their own entry, or from the row's default where they have set none. With no Commander
    /// known, <paramref name="pending"/> stands in for the entry.
    /// </summary>
    public static D47Settings Project(D47Settings stored, string? fid, CommanderSettings? pending = null)
    {
        var overlay = OverlayFor(stored, fid) ?? (fid is { Length: > 0 } ? null : pending) ?? new CommanderSettings { CommanderFid = string.Empty };
        var defaults = new D47Settings();

        return stored with
        {
            CommanderGender = Read(overlay.CommanderGender, defaults.CommanderGender),
            Llm = stored.Llm with
            {
                AboutMe = Read(overlay.AboutMe, defaults.Llm.AboutMe),
                CharacterSheet = Read(overlay.CharacterSheet, defaults.Llm.CharacterSheet),
                Scenario = Read(overlay.Scenario, defaults.Llm.Scenario),
                ScenarioAudience = overlay.ScenarioAudience ?? defaults.Llm.ScenarioAudience,
            },
            Persona = stored.Persona with
            {
                ShipCoreShip = overlay.ShipCoreShip ?? defaults.Persona.ShipCoreShip,
            },
            Callouts = stored.Callouts with
            {
                HomeSystem = Read(overlay.HomeSystem, defaults.Callouts.HomeSystem),
            },
            CareerRatings = stored.CareerRatings with
            {
                Combat = overlay.CombatRating ?? defaults.CareerRatings.Combat,
                Trading = overlay.TradingRating ?? defaults.CareerRatings.Trading,
                Mining = overlay.MiningRating ?? defaults.CareerRatings.Mining,
                Exploration = overlay.ExplorationRating ?? defaults.CareerRatings.Exploration,
                Exobiology = overlay.ExobiologyRating ?? defaults.CareerRatings.Exobiology,
                Passengers = overlay.PassengersRating ?? defaults.CareerRatings.Passengers,
                SearchAndRescue = overlay.SearchAndRescueRating ?? defaults.CareerRatings.SearchAndRescue,
                Crime = overlay.CrimeRating ?? defaults.CareerRatings.Crime,
            },
        };
    }

    /// <summary>
    /// The document to write after a change made against the projected view. The installation's copy of
    /// the Commander fields is never written; with no Commander known, <see cref="Pending"/> holds them.
    /// </summary>
    public static D47Settings Persist(
        D47Settings stored,
        D47Settings effective,
        D47Settings next,
        string? fid,
        string? name)
    {
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

        if (fid is not { Length: > 0 })
        {
            return install == stored ? stored : install;
        }

        var overlay = OverlayFor(stored, fid) ?? new CommanderSettings { CommanderFid = fid };
        var updated = Changed(overlay, effective, next);

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

    /// <summary>The per-Commander values held while no Commander is known, after a change.</summary>
    public static CommanderSettings Pending(CommanderSettings pending, D47Settings effective, D47Settings next) =>
        Changed(pending, effective, next);

    /// <summary>
    /// Gives <paramref name="pending"/> to this Commander for each field they have no value of their own
    /// for. Returns <paramref name="stored"/> when nothing is taken.
    /// </summary>
    public static D47Settings Adopt(D47Settings stored, string fid, string? name, CommanderSettings pending)
    {
        var overlay = OverlayFor(stored, fid) ?? new CommanderSettings { CommanderFid = fid };

        var updated = overlay with
        {
            AboutMe = overlay.AboutMe ?? pending.AboutMe,
            CharacterSheet = overlay.CharacterSheet ?? pending.CharacterSheet,
            Scenario = overlay.Scenario ?? pending.Scenario,
            ScenarioAudience = overlay.ScenarioAudience ?? pending.ScenarioAudience,
            ShipCoreShip = overlay.ShipCoreShip ?? pending.ShipCoreShip,
            HomeSystem = overlay.HomeSystem ?? pending.HomeSystem,
            CommanderGender = overlay.CommanderGender ?? pending.CommanderGender,
            CombatRating = overlay.CombatRating ?? pending.CombatRating,
            TradingRating = overlay.TradingRating ?? pending.TradingRating,
            MiningRating = overlay.MiningRating ?? pending.MiningRating,
            ExplorationRating = overlay.ExplorationRating ?? pending.ExplorationRating,
            ExobiologyRating = overlay.ExobiologyRating ?? pending.ExobiologyRating,
            PassengersRating = overlay.PassengersRating ?? pending.PassengersRating,
            SearchAndRescueRating = overlay.SearchAndRescueRating ?? pending.SearchAndRescueRating,
            CrimeRating = overlay.CrimeRating ?? pending.CrimeRating,
        };

        if (updated == overlay)
        {
            return stored;
        }

        updated = updated with { CommanderName = name ?? overlay.CommanderName };

        return stored with
        {
            Commanders = [.. stored.Commanders.Where(entry => !IsFor(entry, fid)), updated],
        };
    }

    /// <summary>The overlay with every per-Commander field the change moved written into it.</summary>
    private static CommanderSettings Changed(CommanderSettings overlay, D47Settings effective, D47Settings next)
    {
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

        return updated;
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

    /// <summary>Null reads as the default; empty reads as nothing; text reads as text.</summary>
    private static string? Read(string? overlay, string? @default) =>
        overlay switch
        {
            null => @default,
            { Length: 0 } => null,
            _ => overlay,
        };

    /// <summary>A cleared value is recorded as blank, never as unset.</summary>
    private static string Written(string? value) => value ?? string.Empty;
}
