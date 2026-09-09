using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Lore;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Systems worth remarking on, and the Commander's own notes about them (Phase 23).</summary>
public static class LoreCapability
{
    public const string Id = "lore";

    public const string RemarksKey = "callouts.lore";

    /// <summary>The Commander's own notes, as a disclosure with the way into them beside it.</summary>
    public const string BookKey = "lore.book";

    private static readonly string[] Choices = ["off", "remark", "lookup"];

    /// <summary>Where the Commander is, as much of it as a note needs.</summary>
    /// <param name="FrontierId">Who was aboard.</param>
    public sealed record LorePlace(long SystemAddress, string SystemName, string? FrontierId);

    /// <summary>Builds the descriptor.</summary>
    /// <param name="here">
    /// The system the Commander is in, or null when d47 has not been told yet — before the first
    /// journal event of a session, and after a login the journal did not narrate.
    /// </param>
    /// <param name="now">The clock, injected.</param>
    /// <param name="book">
    /// The Commander's own notes, or null where nothing composed a store — under the designer and in
    /// tests that are not about them.
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

        // Phrases, never bare words — "lore" and "system" both appear in half of everything a Commander says.
        Keywords =
        [
            "what is notable about this system",
            "anything notable here",
            "what is special about this system",
            "is there anything out here",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Lore", Order = 65 },
        Settings = [RemarksRow(), BookRow(book)],
        Tools =
        [
            // Argument-free and first, so "what is notable about this system" reaches it through the keyword
            // router with no model in the path.
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

        // The shipped row directly when there is no book.
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

        // Truncated rather than refused.
        var trimmed = note.Trim();

        if (trimmed.Length > MaxNote)
        {
            trimmed = trimmed[..MaxNote].TrimEnd() + "…";
        }

        var entry = book.Add(
            place.SystemAddress,
            place.SystemName,
            trimmed,

            // Always the model, and never the Commander, whatever the turn looked like from in here.
            LoreArrival.Model,
            now,
            place.FrontierId);

        return ToolResult.Ok($"Noted about {place.SystemName}, as unverified: {entry.Note}");
    }

    /// <summary>How long a note may be.</summary>
    private const int MaxNote = 400;

    private static SettingRow RemarksRow() => new()
    {
        Key = RemarksKey,
        Advanced = true,
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

        // Protected like every other callout row.
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

    /// <summary>What is in the book, for the row above the button.</summary>
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
        Advanced = true,
        Label = "Your own notes",
        Help = "What you have told D47 about a system, kept between sessions and said back on arrival.",
        Kind = SettingKind.Info,
        DocsAnchor = "notes",
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };

    /// <summary>Where the Commander is, from the game state — null until the journal has said.</summary>
    public static LorePlace? PlaceOf(CommanderGameState? state) =>
        state is { Location: { SystemAddress: { } address, StarSystem: { } name } }
            ? new LorePlace(address, name, state.Identity.FrontierId)
            : null;
}
