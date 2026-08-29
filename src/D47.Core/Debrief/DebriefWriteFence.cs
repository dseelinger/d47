namespace D47.Core.Debrief;

/// <summary>
/// One write was refused, and what it was aimed at.
/// <para>
/// An exception rather than a returned false, because a caller that has reached this point has
/// already decided to write something: there is no sensible continuation, and a boolean nobody
/// checked is a fence that is not one.
/// </para>
/// </summary>
public sealed class DebriefWriteRefused(string path, string why)
    : InvalidOperationException($"The debrief pass may not write {path}: {why}")
{
    /// <summary>What it tried to write.</summary>
    public string Attempted { get; } = path;

    /// <summary>Why it was refused, in the words the message carries.</summary>
    public string Why { get; } = why;
}

/// <summary>
/// The whole of what the debrief pass is allowed to write
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>One file, named exactly, in the data folder.</b> Everything else is refused, and three
/// refusals matter more than the rest:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>The guardrails.</b> They sit above the persona precisely so nothing downstream can strip
/// them (architecture.md §6), and a self-improving loop that could edit them would be the one
/// downstream thing that could. <see cref="Conversation.PromptAssembly.Guardrails"/> has no setter
/// for the same reason; this is that decision reaching the disk.
/// </item>
/// <item>
/// <b>Tool schemas.</b> They must serialize byte-identically across turns or prompt caching dies,
/// and a loop that rewrote a description would invalidate ~39,000 bytes of prefix on the turn after
/// it ran, for a change nobody asked for.
/// </item>
/// <item>
/// <b>The persona pack.</b> Persona writing lives twice — <c>guardian-personas.md</c> is ported
/// into <see cref="Persona.PersonaCatalog"/> — so a runtime loop editing either copy manufactures
/// port drift between them. A learned style note is an overlay in <c>data\</c>, carried by
/// <see cref="StandingDirection.Persona"/>, and the pack is never touched.
/// </item>
/// </list>
/// <para>
/// <b>It is a check on the path rather than on the caller's intentions</b>, which is what makes it
/// testable: a test points a store at the guardrails source, at the persona pack, at
/// <c>settings.json</c>, and watches each one refused with the file's bytes untouched. A fence
/// written as a paragraph addressed to whoever writes the next store would be a fence that holds
/// until somebody writes the next store — the same lesson the issue wrapper recorded when a
/// default-deny rule addressed to an agent turned out to stop nothing.
/// </para>
/// </summary>
public static class DebriefWriteFence
{
    /// <summary>
    /// The only file name the pass may write. Compared exactly and case-insensitively, because
    /// Windows would otherwise let <c>Standing-Directions.JSON</c> through a check meant to be
    /// exhaustive.
    /// </summary>
    public const string FileName = "standing-directions.json";

    /// <summary>
    /// The only folder it may sit in — <c>data\</c> beside the executable, which is where
    /// everything d47 writes goes and where a Commander looks for it.
    /// <para>
    /// Named rather than resolved against a live <see cref="AppPaths"/>, because Core owns no
    /// installation and a test must be able to build one in a temporary folder. The name is the
    /// invariant; the drive it is on is not.
    /// </para>
    /// </summary>
    public const string FolderName = AppPaths.DataFolderName;

    /// <summary>
    /// Whether this path is the one file, said as a question so the panel can ask without
    /// catching. <see cref="Enforce"/> is what a write goes through.
    /// </summary>
    public static bool Permits(string? path) => Refusal(path) is null;

    /// <summary>
    /// Refuses anything that is not the standing-directions file, and says why. Called when a
    /// store is constructed and again before every save — construction alone would leave a store
    /// that was handed a legal path and then asked to write elsewhere.
    /// </summary>
    public static void Enforce(string? path)
    {
        if (Refusal(path) is { } why)
        {
            throw new DebriefWriteRefused(path ?? "(nothing)", why);
        }
    }

    /// <summary>
    /// Why this path is refused, or null if it is the one allowed file.
    /// <para>
    /// <b>Allow-list first, and the named refusals are for the message.</b> The rule that actually
    /// holds is "this exact name in a folder called data"; naming the guardrails and the persona
    /// pack afterwards buys nothing in safety and a great deal in what a failure tells its reader,
    /// which is the difference between a test that documents the fence and one that merely passes.
    /// </para>
    /// </summary>
    private static string? Refusal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "it is not a path";
        }

        string full;
        string name;
        string? folder;

        try
        {
            full = Path.GetFullPath(path);
            name = Path.GetFileName(full);
            folder = Path.GetFileName(Path.GetDirectoryName(full));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "it is not a path";
        }

        if (Named(name) is { } named)
        {
            return named;
        }

        if (!string.Equals(name, FileName, StringComparison.OrdinalIgnoreCase))
        {
            return $"the only file it may write is {FileName}";
        }

        if (!string.Equals(folder, FolderName, StringComparison.OrdinalIgnoreCase))
        {
            return $"{FileName} has to sit in the {FolderName} folder";
        }

        return null;
    }

    /// <summary>
    /// The refusals worth naming out loud. Every one of these is already caught by the rule above;
    /// what this adds is a sentence a reader of the failure understands without going and reading
    /// the fence.
    /// </summary>
    private static string? Named(string name) => name switch
    {
        "Guardrails.cs" => "the guardrails are never editable by anything downstream of them",
        "PromptAssembly.cs" => "prompt assembly order is a contract, not a preference",
        "guardian-personas.md" or "PersonaCatalog.cs" =>
            "persona writing lives twice, and a loop editing either copy manufactures port drift",
        "settings.json" => "settings are the Commander's, and the panel is where they are written",
        _ => name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            ? "it writes no source"
            : null,
    };
}
