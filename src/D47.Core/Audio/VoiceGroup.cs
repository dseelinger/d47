using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>
/// Which slot a voice comes out of, and so which provider synthesises it (Phase 57).
/// <para>
/// A layer above <see cref="VoiceRole"/> rather than a replacement for it. A role is who is
/// speaking and is what a caller knows; a group is who is <em>paying</em> for it and what a
/// settings row names. The two are not the same shape: the carrier's captain and its tower are
/// two people and one installation, and <see cref="VoiceRole.Comms"/> is one role carrying
/// everybody from a station's traffic controller to a stranger shouting in local.
/// </para>
/// <para>
/// <b>The groups are derived, not invented.</b> <see cref="RadioVoice.IsOverTheAir"/> already
/// separates who is in the room from who arrives through a radio, <see cref="VoiceCast"/>
/// already holds five roles, and the chat channel already says whether a message came from a
/// person and whether it came from one the Commander chose to be in contact with. Every line
/// below reads one of those; none of them draws a new one.
/// </para>
/// <para>
/// <b>The human channels sort by consent rather than by humanity</b>, settled with the Commander
/// on 2026-08-25. A friend is a real person whose lines the Commander probably wants on the good
/// voice; a stranger in local is a real person whose lines are heard once, are unbounded in
/// volume, and are chosen by somebody else. Cost and trust point the same way and both separate
/// those two.
/// </para>
/// </summary>
public enum VoiceGroup
{
    /// <summary>The two voices actually in the cockpit: the ship's AI and the crew.</summary>
    Aboard,

    /// <summary>The Commander's fleet carrier — its captain and its tower, one installation.</summary>
    Carrier,

    /// <summary>Game-authored traffic. Not real people, and not spammable at the Commander.</summary>
    Npcs,

    /// <summary>Real people the Commander has accepted, teamed with, or joined.</summary>
    PeopleYouKnow,

    /// <summary>A real person reaching the Commander directly, on the <c>player</c> channel.</summary>
    DirectMessages,

    /// <summary>
    /// Local and system chat: real people, no consent anywhere in it, unbounded in volume. The
    /// untrusted path, and the reason Phase 57 exists.
    /// </summary>
    AnyoneInRange,
}

/// <summary>What one slot covers, what it is called, and which side of the hull it is on.</summary>
public sealed record VoiceGroupInfo
{
    /// <summary>
    /// The settings key this slot is filed under. <b>Never renamed</b>: the settings file is
    /// append-only, and a renamed key is a Commander's choice silently dropped
    /// (see <see cref="SpeechSettings.GroupProviders"/>).
    /// </summary>
    public required string Id { get; init; }

    public required VoiceGroup Group { get; init; }

    /// <summary>How the settings row and the disclosure table name it.</summary>
    public required string Name { get; init; }

    /// <summary>Who is in it, in a clause, for the row's help and the disclosure's table.</summary>
    public required string Covers { get; init; }

    /// <summary>
    /// Whether this slot reaches the Commander through a radio rather than from the next seat.
    /// The line is <see cref="RadioVoice.IsOverTheAir"/>'s and it already existed; this is that
    /// division being given a second job rather than a new one being drawn.
    /// </summary>
    public required bool OverTheAir { get; init; }

    /// <summary>
    /// Whether the text this slot speaks was written by another player.
    /// <para>
    /// The property the phase is actually about. A paid provider here is a per-character bill for
    /// text somebody else writes and can write as much of as they like — and every character of
    /// it leaves this machine. The carrier is not in this set even though it is over the air: the
    /// captain and the tower are d47's own fictions, and NPC chatter is Frontier's.
    /// </para>
    /// </summary>
    public required bool OtherPeoplesWords { get; init; }

    /// <summary>
    /// The in-game chat channels that land here, or empty for the two slots that are not chat at
    /// all. One list rather than one per reader: <see cref="Callouts.IncomingMessages"/> asks this
    /// which channels are people, so the routing and the gating cannot disagree about
    /// <c>starsystem</c>.
    /// </summary>
    public IReadOnlyList<string> Channels { get; init; } = [];

    /// <summary>The roles that land here regardless of channel.</summary>
    public IReadOnlyList<VoiceRole> Roles { get; init; } = [];
}

/// <summary>
/// The six slots, what falls into each, and which provider speaks for it. One list read by the
/// provider rows, the disclosure, the wiring plan and the app's client map — so a slot cannot
/// exist in one of those and be missing from another (Phase 57).
/// </summary>
public static class VoiceGroups
{
    public static VoiceGroupInfo Aboard { get; } = new()
    {
        Id = "aboard",
        Group = VoiceGroup.Aboard,
        Name = "Aboard",
        Covers = "your ship's AI and your crew",
        OverTheAir = false,
        OtherPeoplesWords = false,
        Roles = [VoiceRole.ShipAi, VoiceRole.Crew],
    };

    public static VoiceGroupInfo Carrier { get; } = new()
    {
        Id = "carrier",
        Group = VoiceGroup.Carrier,
        Name = "Carrier",
        Covers = "your fleet carrier's captain and its tower",
        OverTheAir = true,
        OtherPeoplesWords = false,
        Roles = [VoiceRole.CarrierCaptain, VoiceRole.TowerControl],
    };

    public static VoiceGroupInfo Npcs { get; } = new()
    {
        Id = "npcs",
        Group = VoiceGroup.Npcs,
        Name = "NPCs",
        Covers = "stations, police, and every other ship the game speaks for",
        OverTheAir = true,
        OtherPeoplesWords = false,
        Channels = ["npc"],
    };

    public static VoiceGroupInfo PeopleYouKnow { get; } = new()
    {
        Id = "known",
        Group = VoiceGroup.PeopleYouKnow,
        Name = "People you know",
        Covers = "your friends, your wing and your squadron",
        OverTheAir = true,
        OtherPeoplesWords = true,

        // squadleaders is squadron leadership on its own channel (#299) — a Commander does not
        // know Elite writes those as two channels, so it is grouped rather than left to fall
        // through to Npcs, which is what an unlisted channel means to IsAPerson.
        Channels = ["friend", "wing", "squadron", "squadleaders"],
    };

    public static VoiceGroupInfo DirectMessages { get; } = new()
    {
        Id = "direct",
        Group = VoiceGroup.DirectMessages,
        Name = "Direct messages",
        Covers = "a Commander messaging you directly",
        OverTheAir = true,
        OtherPeoplesWords = true,
        Channels = ["player"],
    };

    public static VoiceGroupInfo AnyoneInRange { get; } = new()
    {
        Id = "range",
        Group = VoiceGroup.AnyoneInRange,
        Name = "Anyone in range",
        Covers = "local and system chat — anybody at all, whether you know them or not",
        OverTheAir = true,
        OtherPeoplesWords = true,
        Channels = ["local", "starsystem"],
    };

    /// <summary>
    /// Every slot, in the order the settings rows offer them: the cockpit first, then outward by
    /// how little the Commander chose to hear from it.
    /// </summary>
    public static IReadOnlyList<VoiceGroupInfo> All { get; } =
        [Aboard, Carrier, Npcs, PeopleYouKnow, DirectMessages, AnyoneInRange];

    public static VoiceGroupInfo Info(VoiceGroup group) =>
        All.First(slot => slot.Group == group);

    /// <summary>The slot with this settings key, or null for one d47 does not ship.</summary>
    public static VoiceGroupInfo? ById(string? id) =>
        All.FirstOrDefault(slot => string.Equals(slot.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Which slot a line belongs to. Pure, and the whole of the routing.
    /// </summary>
    /// <param name="channel">
    /// The in-game chat channel a re-voiced message arrived on, or null where the line is not one
    /// — a callout, a crew member, the carrier. Only <see cref="VoiceRole.Comms"/> reads it,
    /// because it is the only role that carries more than one kind of speaker.
    /// <para>
    /// <b>An unknown channel is an NPC</b>, which is the same answer
    /// <see cref="Callouts.IncomingMessages"/> gives it — a channel neither of them recognises
    /// must not be spoken by one and gated by the other.
    /// </para>
    /// </param>
    public static VoiceGroup Of(VoiceRole role, string? channel = null)
    {
        foreach (var slot in All)
        {
            if (slot.Roles.Contains(role))
            {
                return slot.Group;
            }
        }

        foreach (var slot in All)
        {
            if (slot.Channels.Contains(channel ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                return slot.Group;
            }
        }

        return VoiceGroup.Npcs;
    }

    /// <summary>
    /// Whether a channel carries somebody a person typed as. Asked by
    /// <see cref="Callouts.IncomingMessages"/> so the channel list lives once — the gate on NPC
    /// chatter and the slot a line is billed to have to agree about <c>starsystem</c>, and two
    /// copies of that list are two things to keep agreeing.
    /// </summary>
    public static bool IsAPerson(string? channel) => Of(VoiceRole.Comms, channel) != VoiceGroup.Npcs;

    /// <summary>
    /// Which provider speaks for one slot.
    /// <para>
    /// <b><see cref="VoiceGroup.Aboard"/> is <see cref="SpeechSettings.Provider"/> and cannot be
    /// anything else.</b> That row has meant "the voice provider" since Phase 4 and it keeps
    /// meaning it; the five rows this phase adds are the other five slots. Read through here by
    /// the rows, the wiring and the disclosure, so none of them can believe a different answer.
    /// </para>
    /// <para>
    /// An absent entry means the same as Aboard, which is what a settings file written before
    /// this phase loads as — every voice from one provider, exactly as it sounded. The migration
    /// in <see cref="Migrated"/> is what moves an existing file off that, once and deliberately.
    /// </para>
    /// </summary>
    public static string ProviderFor(SpeechSettings speech, VoiceGroup group)
    {
        if (group == VoiceGroup.Aboard)
        {
            return TtsProviderCatalog.Selected(speech.Provider).Id;
        }

        var slot = Info(group);
        var chosen = speech.GroupProviders?.GetValueOrDefault(slot.Id);

        var resolved = chosen is null
            ? TtsProviderCatalog.Selected(speech.Provider)
            : TtsProviderCatalog.Selected(chosen);

        // A provider that cannot be told a language never speaks for a slot carrying other
        // people's words, however it got named — the picker does not offer it, and a hand-edited
        // file naming one is treated the way every unusable value is rather than obeyed
        // (Phase 58). Falling back to Edge rather than to `Provider`: the ship's provider
        // could be the very one being refused, and a fallback that can loop is not one.
        return TtsProviderCatalog.For(slot).Contains(resolved)
            ? resolved.Id
            : TtsProviderCatalog.EdgeId;
    }

    /// <summary>Every slot's provider, resolved. What the wiring plan is asked to arrange.</summary>
    public static IReadOnlyDictionary<VoiceGroup, string> Selected(SpeechSettings speech) =>
        All.ToDictionary(slot => slot.Group, slot => ProviderFor(speech, slot.Group));

    /// <summary>
    /// What a voice id is called, looked for in <b>every</b> slot's list rather than the ship's
    /// alone (<a href="https://github.com/dseelinger/d47/issues/149">#149</a>).
    /// <para>
    /// The ship's list was the whole answer while one provider spoke for everybody. Since this
    /// phase six slots can name three, so a voice handed to an NPC out of ElevenLabs' pool was
    /// looked up in Kokoro's list, found nowhere, and written into the log as a bare
    /// <c>FwuKjlVpi0N3exead7ji</c> — telling the reader neither whose voice it was nor which of
    /// the three providers had been billed for it. The name was fetched and held the whole time,
    /// under another slot's key.
    /// </para>
    /// <para>
    /// <b><see cref="Aboard"/> first, because <see cref="All"/> starts there.</b> An id that
    /// already resolved gets exactly the answer it got before, and the other five are consulted
    /// only where there was nothing — so widening this cannot change a line that was already
    /// right. Ids are a provider's own namespace, and two providers minting the same one is not a
    /// case worth ordering around beyond that.
    /// </para>
    /// <para>
    /// Null is a normal state and not a fault: a list is fetched when a key arrives and when a
    /// provider changes, so an id chosen last session is unresolved until that returns, and one
    /// the Commander typed by hand may never resolve at all. The caller decides what to show.
    /// </para>
    /// </summary>
    /// <param name="catalogues">
    /// What one slot's provider offers. A function rather than the six lists, because the app
    /// holds them per <em>provider</em> and two slots routinely share one — asking per slot is
    /// what keeps this from needing to know that.
    /// </param>
    public static string? NameFor(Func<VoiceGroup, VoiceCatalogue> catalogues, string? id)
    {
        ArgumentNullException.ThrowIfNull(catalogues);

        if (id is not { Length: > 0 })
        {
            return null;
        }

        foreach (var slot in All)
        {
            var named = catalogues(slot.Group).Voices
                .FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase));

            if (named?.Name is { Length: > 0 } name)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// The providers actually needed, each once. The list the app builds clients from — one per
    /// provider and never one per slot, so two slots on ElevenLabs share the account's
    /// concurrency gate rather than each believing they own it
    /// (<c>ElevenLabsTtsProvider.MaxConcurrent</c>).
    /// </summary>
    public static IReadOnlyList<string> ProvidersInUse(SpeechSettings speech) =>
        [.. Selected(speech).Values
            .Where(id => TtsProviderCatalog.Selected(id).Speaks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)];

    /// <summary>
    /// The same settings with the five over-the-air slots moved to Edge, once, for a file written
    /// before this phase.
    /// <para>
    /// <b>Migrated rather than grandfathered.</b> Leaving an existing file alone would leave every
    /// stranger in local speaking through whatever paid provider the Commander had chosen for
    /// their companion — which is the bill this phase exists to stop, and it would arrive quietly.
    /// Safe to do here because the install base is two, recorded as being true on 2026-08-25
    /// rather than for ever.
    /// </para>
    /// <para>
    /// <b>Nothing happens while nothing is speaking.</b> With "none" selected the Commander has
    /// asked for silence, and writing Edge into five slots would answer that by starting to talk.
    /// The map stays unwritten until a provider that speaks is selected, and the ruling applies at
    /// the moment it means something.
    /// </para>
    /// <para>
    /// Null is what says "before this phase". An empty map is a file this build has already
    /// written and a Commander who has put every slot back onto the ship's provider by hand —
    /// two different things that must not be confused, which is why the property is nullable
    /// rather than merely empty (<see cref="SpeechSettings.VoicesProvider"/> is the precedent and
    /// the same reasoning).
    /// </para>
    /// </summary>
    public static D47Settings Migrated(D47Settings settings)
    {
        if (settings.Speech.GroupProviders is not null
            || !TtsProviderCatalog.Selected(settings.Speech.Provider).Speaks)
        {
            return settings;
        }

        var moved = All
            .Where(slot => slot.Group != VoiceGroup.Aboard)
            .ToDictionary(slot => slot.Id, _ => TtsProviderCatalog.EdgeId, StringComparer.OrdinalIgnoreCase);

        return settings with { Speech = settings.Speech with { GroupProviders = moved } };
    }
}
