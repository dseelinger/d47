namespace D47.Core.Configuration;

/// <summary>
/// The two layers of the settings file, read as one and written back as two (Phase 44,
/// "The split is per row and per store, declared rather than inferred").
/// <para>
/// <see cref="D47Settings"/> is one record in one file and most of it is the installation's. A
/// handful of fields are the Commander's — About Me, the character sheet, which ship the
/// core-binding rows point at — and those are layered: the active Commander's
/// <see cref="CommanderSettings"/> entry over the installation's value. <see cref="Project"/> is
/// that read, and <see cref="Persist"/> is the write that puts a change back in the layer it
/// belongs to. Everything else in the service sees a plain <see cref="D47Settings"/> and never
/// learns there were two.
/// </para>
/// <para>
/// <b>Unset and deliberately blank stay apart.</b> An overlay field that is null has never been
/// set and reads through to the installation's; one that is empty was cleared and reads as
/// nothing. For About Me, empty is meaningful: a Commander who cleared their story must not be
/// handed somebody else's because the box was blank. So a clear written through a Commander
/// row lands in the overlay as empty rather than vanishing from it.
/// </para>
/// <para>
/// <b>The field list lives here and in <see cref="CommanderSettings"/> only.</b> A row declares
/// <see cref="Capabilities.SettingScope.Commander"/>; this is what makes the declaration true.
/// <c>CommanderScopeTests</c> asserts the two agree, which is the gate against a per-Commander
/// field being added without its row saying so — or a row saying so without anything behind it.
/// </para>
/// </summary>
public static class CommanderScope
{
    /// <summary>
    /// The settings as this Commander sees them: the installation's, with their own values laid
    /// over the fields that are theirs. The installation's record itself when nobody is flying
    /// yet, or when this Commander has never set anything — both are the plain file.
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

    /// <summary>
    /// The document to write after a change made against the projected view.
    /// <para>
    /// Install-level fields take the new value outright. A Commander field that changed goes into
    /// this Commander's overlay — as empty rather than null when it was cleared, which is the
    /// unset-versus-blank rule — and one that did not change is left exactly as the overlay had it,
    /// so a field this Commander never set stays following the installation's. With nobody flying
    /// the change is the installation's, whole: d47 runs before Elite has said who is aboard, and a
    /// value typed then belongs to the installation rather than to whoever logs in first.
    /// </para>
    /// <para>
    /// Returns <paramref name="stored"/> itself when there is nothing to write, so a caller can
    /// tell a real change from a projection that merely came back equal.
    /// </para>
    /// </summary>
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

        // The installation's copy of the Commander fields is never written through a Commander:
        // whatever the row did to them is moved into the overlay below.
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
            // Nothing of this Commander's moved. Whatever did move is the installation's, and a
            // record that came back equal is not a write at all.
            return install == stored ? stored : install;
        }

        // The name is written beside the id for a person reading the file, and only when the
        // entry is being written anyway: a file that differs from disk by a name the journal
        // just restated is not worth a write.
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
    /// This Commander's document with one of their own fields forgotten — one entry per field
    /// they could have set (<a href="https://github.com/dseelinger/d47/issues/61">#61</a>).
    /// <para>
    /// <b>Reset on a per-Commander row means "stop having my own answer", not "write a blank
    /// one".</b> <see cref="Persist"/> records a cleared field as <em>empty</em> rather than null,
    /// deliberately, because that is what keeps <em>this Commander blanked it</em> apart from
    /// <em>this Commander never set it</em>. Reset wants the second of those, so it cannot go
    /// through the ordinary write at all — an empty would leave the Commander permanently opted
    /// out of the installation's value, which is the opposite of a way back.
    /// </para>
    /// <para>
    /// <b>The candidates are returned rather than a row-to-field map being kept here.</b> The
    /// caller finds its field by asking which candidate moves the row's own value, which is
    /// exactly how <c>CommanderScopeTests</c> already decides which rows the overlay reaches. One
    /// rule, used twice; a second list of row keys is a list that would drift.
    /// </para>
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
    /// The document with this Commander's entry replaced — or removed outright once nothing of
    /// theirs is left in it, so forgetting the last field leaves no husk behind carrying only an
    /// id and a name.
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
