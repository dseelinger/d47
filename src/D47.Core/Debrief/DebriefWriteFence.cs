namespace D47.Core.Debrief;

/// <summary>One write was refused, and what it was aimed at.</summary>
public sealed class DebriefWriteRefused(string path, string why)
    : InvalidOperationException($"The debrief pass may not write {path}: {why}")
{
    /// <summary>What it tried to write.</summary>
    public string Attempted { get; } = path;

    /// <summary>Why it was refused, in the words the message carries.</summary>
    public string Why { get; } = why;
}

/// <summary>The whole of what the debrief pass is allowed to write (#162).</summary>
public static class DebriefWriteFence
{
    /// <summary>The only file name the pass may write.</summary>
    public const string FileName = "standing-directions.json";

    /// <summary>
    /// The only folder it may sit in — <c>data\</c> beside the executable, which is where everything
    /// d47 writes goes and where a Commander looks for it.
    /// </summary>
    public const string FolderName = AppPaths.DataFolderName;

    /// <summary>
    /// Whether this path is the one file, said as a question so the panel can ask without catching.
    /// </summary>
    public static bool Permits(string? path) => Refusal(path) is null;

    /// <summary>Refuses anything that is not the standing-directions file, and says why.</summary>
    public static void Enforce(string? path)
    {
        if (Refusal(path) is { } why)
        {
            throw new DebriefWriteRefused(path ?? "(nothing)", why);
        }
    }

    /// <summary>Why this path is refused, or null if it is the one allowed file.</summary>
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

    /// <summary>The refusals worth naming out loud.</summary>
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
