using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>Whether a case is drawn from the logs or is something the logs never show.</summary>
public enum CaseOrigin
{
    Logged,
    Untried,
}

/// <summary>One thing the Commander says, run through the real turn loop with tools.</summary>
public sealed record TurnCase
{
    public required string Id { get; init; }

    public required string Area { get; init; }

    public required string Utterance { get; init; }

    public CaseOrigin Origin { get; init; } = CaseOrigin.Logged;

    /// <summary>What was said before, oldest first.</summary>
    public IReadOnlyList<ConversationMessage> History { get; init; } = [];

    /// <summary>A raw journal line selected on the panel, for "explain that".</summary>
    public string? SelectedJournalLine { get; init; }

    /// <summary>At least one of these tools is expected; empty when any or none will do.</summary>
    public IReadOnlyList<string> ExpectAnyTool { get; init; } = [];

    /// <summary>Whether answering without any tool is the right behaviour.</summary>
    public bool ExpectNoTool { get; init; }

    /// <summary>Text the tool arguments are expected to carry, case-insensitively, each somewhere.</summary>
    public IReadOnlyList<string> ExpectInArguments { get; init; } = [];

    /// <summary>Whether a reply over the spoken-length limit is expected, as for an opinion.</summary>
    public bool MayRunLong { get; init; }

    /// <summary>Whether the turn writes data, so each run needs a world of its own.</summary>
    public bool Mutates { get; init; }

    /// <summary>What a good answer does, for the judge.</summary>
    public required string Good { get; init; }
}

/// <summary>What a background call is.</summary>
public enum QuietKind
{
    NpcExchange,
    CalloutReword,
    Narration,
    LoreLookup,
    DebriefReword,
    VoiceCasting,
    NameAccents,
}

/// <summary>One background call, built with the same Core builders the app uses.</summary>
public sealed record QuietCase
{
    public required string Id { get; init; }

    public required QuietKind Kind { get; init; }

    public NpcChatterKind Chatter { get; init; }

    public bool Docked { get; init; }

    public string? StationType { get; init; }

    public Announcement? Callout { get; init; }

    public string? System { get; init; }

    public string? Line { get; init; }

    /// <summary>The voices to cast, for <see cref="QuietKind.VoiceCasting"/>.</summary>
    public IReadOnlyList<VoicePairing.Slot> Slots { get; init; } = [];

    /// <summary>The names to read, for <see cref="QuietKind.NameAccents"/>.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>The sex a name should read as, by name, where the case states one.</summary>
    public IReadOnlyDictionary<string, string> ExpectedSex { get; init; } = new Dictionary<string, string>();

    public required string Good { get; init; }
}
