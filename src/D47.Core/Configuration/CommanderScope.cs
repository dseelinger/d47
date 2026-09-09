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
            Llm = stored.Llm with
            {
                AboutMe = Read(overlay.AboutMe, stored.Llm.AboutMe),
                CharacterSheet = Read(overlay.CharacterSheet, stored.Llm.CharacterSheet),
            },
            Persona = stored.Persona with
            {
                ShipCoreShip = overlay.ShipCoreShip ?? stored.Persona.ShipCoreShip,
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
            Llm = next.Llm with
            {
                AboutMe = stored.Llm.AboutMe,
                CharacterSheet = stored.Llm.CharacterSheet,
            },
            Persona = next.Persona with
            {
                ShipCoreShip = stored.Persona.ShipCoreShip,
            },
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

        if (next.Persona.ShipCoreShip != effective.Persona.ShipCoreShip)
        {
            updated = updated with { ShipCoreShip = next.Persona.ShipCoreShip };
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
            Without(stored, fid, overlay with { ShipCoreShip = null }),
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
                    && updated.ShipCoreShip is null;

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
