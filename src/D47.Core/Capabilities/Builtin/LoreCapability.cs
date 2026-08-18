using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Lore;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Systems worth remarking on, and the Commander's own notes about them (list.md Phase 23).
/// <para>
/// <b>Reading is model-callable and so is writing, which is a departure worth stating.</b>
/// <see cref="ChecklistCapability"/> makes every write a proposal because the Commander's list is
/// theirs; <see cref="CalloutCapability"/> protects every row because a model that can switch off
/// the interdiction warning is one that can be told to by the Commander interdicting them. Neither
/// applies here: adding a note presses no key and disables no warning. What it does do is write
/// persistent local state that d47 later speaks aloud unprompted, which is a sharper primitive
/// than it looks — so the answer is not a lock but a label. <b>Every entry records how it
/// arrived</b>, and an entry the model wrote is read back as the model's for as long as it exists
/// (see <see cref="LoreArrival"/>).
/// </para>
/// <para>
/// <b>Writing is limited to the system the Commander is actually in.</b> Not caution — arithmetic:
/// a note is keyed on system address, addresses come out of the journal, and Core reaches no
/// network, so a name d47 has not been told the address of cannot be keyed at all. The moment a
/// Commander wants to note something about a system is the moment they are looking at it.
/// </para>
/// <para>
/// The remark setting is <see cref="SettingRow.Protected"/> like every other callout row, for the
/// reason that family documents: the trust boundary, not caution about the model.
/// </para>
/// </summary>
public static class LoreCapability
{
    public const string Id = "lore";

    public const string RemarksKey = "callouts.lore";

    /// <summary>
    /// The Commander's own notes, as a disclosure with the way into them beside it.
    /// <para>
    /// An <see cref="SettingKind.Info"/> row rather than anything writable, which is what puts
    /// adding a note on the right side of the trust boundary for free:
    /// <see cref="SettingsService.Apply"/> refuses Info rows outright, so nothing reachable from
    /// the tool surface can reach this at all. It is the one route that produces
    /// <see cref="LoreTier.Commander"/>, and it produces it because a person's hands were on it.
    /// </para>
    /// </summary>
    public const string BookKey = "lore.book";

    private static readonly string[] Choices = ["off", "remark", "lookup"];

    /// <summary>Where the Commander is, as much of it as a note needs.</summary>
    /// <param name="FrontierId">
    /// Who was aboard. Recorded on the entry and never keyed on — the book is per installation.
    /// </param>
    public sealed record LorePlace(long SystemAddress, string SystemName, string? FrontierId);

    /// <summary>
    /// Builds the descriptor.
    /// </summary>
    /// <param name="here">
    /// The system the Commander is in, or null when d47 has not been told yet — before the first
    /// journal event of a session, and after a login the journal did not narrate.
    /// </param>
    /// <param name="now">
    /// The clock, injected. Core reads no clock, and the stamp on an entry has to be a real
    /// instant rather than a tick number, so this is the one thing the capability is handed.
    /// </param>
    /// <param name="book">
    /// The Commander's own notes, or null where nothing composed a store — under the designer and
    /// in tests that are not about them. <b>The shipped table is still answered from</b>, because
    /// it is part of the build rather than part of the state: a null book costs the notes, not the
    /// capability (list.md Phase 3, and the same shape <c>galaxy</c> has in
    /// <see cref="BuiltinCapabilities"/>).
    /// </param>
    public static CapabilityDescriptor Create(
        LoreBook? book,
        Func<LorePlace?> here,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Lore",
        Summary = "Say what is notable about a system on arrival, and remember what you tell me about one.",
        Examples =
        [
            "what is notable about this system",
            "is there anything out here",
            "remember that this system has a good mining ring",
        ],

        // Phrases, never bare words — "lore" and "system" both appear in half of everything a
        // Commander says. The rule JournalCapability documents.
        Keywords =
        [
            "what is notable about this system",
            "anything notable here",
            "what is special about this system",
            "is there anything out here",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Lore", Order = 59 },
        Settings = [RemarksRow(), BookRow(book)],
        Tools =
        [
            // Argument-free and first, so "what is notable about this system" reaches it through
            // the keyword router with no model in the path.
            new ToolDefinition
            {
                Name = "get_system_lore",
                Description =
                    "What D47 knows about the system the Commander is in that is not astrography: the "
                    + "shipped table's entry, plus anything the Commander has had noted about it. Each "
                    + "answer states where it came from.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(book, here()))),
            },

            new ToolDefinition
            {
                Name = "remember_about_system",
                Description =
                    "Note something about the system the Commander is in, kept between sessions and "
                    + "spoken back on a later arrival. Only this system — a note is keyed on the system "
                    + "address, and D47 has no address for a system it has not been to. Stored as "
                    + "unverified, and read back that way.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "note",
                        Type = ToolParameterType.String,
                        Description =
                            "What is worth remembering, in one or two sentences. Facts about the place, "
                            + "not about what the Commander is doing today.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Remember(book, here(), now(), arguments)),
            },
        ],
    };

    private static string Describe(LoreBook? book, LorePlace? place)
    {
        if (place is null)
        {
            return "I do not know where you are yet.";
        }

        // The shipped row directly when there is no book. Not a fallback so much as the honest
        // split: the table is compiled in and the notes are state, and only one of the two can
        // be missing.
        var described = book is null
            ? LoreDirectory.ByAddress(place.SystemAddress)?.Spoken()
            : book.Describe(place.SystemAddress, place.SystemName);

        return described ?? $"Nothing on {place.SystemName} beyond what the scanners say.";
    }

    private static ToolResult Remember(LoreBook? book, LorePlace? place, DateTimeOffset now, ToolArguments arguments)
    {
        if (book is null)
        {
            return ToolResult.Error("I have nowhere to keep notes in this configuration.");
        }

        if (place is null)
        {
            return ToolResult.Error("I do not know which system you are in, so there is nothing to attach a note to.");
        }

        if (!arguments.TryGetString("note", out var note) || string.IsNullOrWhiteSpace(note))
        {
            return ToolResult.Error("A note needs something in it.");
        }

        // Truncated rather than refused. The text arrives from a model that read untrusted input,
        // and the file is the Commander's to read — a note that runs to a page is a note that
        // buries the ones they made themselves.
        var trimmed = note.Trim();

        if (trimmed.Length > MaxNote)
        {
            trimmed = trimmed[..MaxNote].TrimEnd() + "…";
        }

        var entry = book.Add(
            place.SystemAddress,
            place.SystemName,
            trimmed,

            // Always the model, and never the Commander, whatever the turn looked like from in
            // here. A turn steered by a hostile in-game message is indistinguishable from one the
            // Commander asked for at this depth, so the label says what is actually known.
            LoreArrival.Model,
            now,
            place.FrontierId);

        return ToolResult.Ok($"Noted about {place.SystemName}, as unverified: {entry.Note}");
    }

    /// <summary>
    /// How long a note may be. Two or three sentences — long enough for a fact, short enough that
    /// it is read out rather than recited.
    /// </summary>
    private const int MaxNote = 400;

    private static SettingRow RemarksRow() => new()
    {
        Key = RemarksKey,
        Label = "Remark on arrival",
        Help =
            "Say something on arriving in a system with a story attached, at most once a day per system. "
            + "\"Remark and look it up\" follows the bare fact with a web search, where searching is on.",
        Kind = SettingKind.Choice,
        Choices = Choices,
        ChoiceLabel = value => value switch
        {
            "off" => "Never",
            "remark" => "Remark only",
            _ => "Remark, and look it up",
        },
        DefaultDisplay = "Remark, and look it up",
        DocsAnchor = "remarks",

        // Protected like every other callout row. Not caution about the model: journal text and
        // in-game comms are untrusted, so anything the model can call a hostile message can
        // attempt to invoke, and a row that silences d47 is a row worth attempting.
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("stop remarking on systems", "off"),
            new SettingCommandPhrase("start remarking on systems", "lookup"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Callouts.Lore switch
            {
                LoreRemarks.Off => "off",
                LoreRemarks.Remark => "remark",
                _ => "lookup",
            },
            Write = (s, v) => s with
            {
                Callouts = s.Callouts with
                {
                    Lore = v switch
                    {
                        "off" => LoreRemarks.Off,
                        "remark" => LoreRemarks.Remark,
                        _ => LoreRemarks.Lookup,
                    },
                },
            },
        },
    };

    /// <summary>
    /// What is in the book, for the row above the button. A count and the systems, never the
    /// notes — the notes are what the window and the arrival remark are for.
    /// </summary>
    public static string Summarise(LoreBook? book)
    {
        var mine = book?.Store.Entries ?? [];

        var line = $"{LoreDirectory.All.Count} systems ship with something to say about them.";

        if (mine.Count == 0)
        {
            return line + " You have not added any of your own.";
        }

        var systems = mine.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        line += $" You have {mine.Count} note{(mine.Count == 1 ? "" : "s")} of your own, "
                + $"about {systems} system{(systems == 1 ? "" : "s")}.";

        return book?.Store.Problems is { Count: > 0 } problems
            ? line + $" {problems.Count} could not be read back."
            : line;
    }

    private static SettingRow BookRow(LoreBook? book) => new()
    {
        Key = BookKey,
        Label = "Your own notes",
        Help = "What you have told D47 about a system, kept between sessions and said back on arrival.",
        Kind = SettingKind.Info,
        DocsAnchor = "notes",
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };

    /// <summary>
    /// Where the Commander is, from the game state — null until the journal has said. Lives here
    /// rather than at the wiring site so the App and the replay harness read it the same way.
    /// </summary>
    public static LorePlace? PlaceOf(CommanderGameState? state) =>
        state is { Location: { SystemAddress: { } address, StarSystem: { } name } }
            ? new LorePlace(address, name, state.Identity.FrontierId)
            : null;
}
