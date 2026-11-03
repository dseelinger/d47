namespace D47.Core.Modules;

public sealed record ModulePage(string Id, string Title);

public sealed record ModuleVoice(string Provider, string Voice);

public sealed record ModuleCharacter(string Name, IReadOnlyList<ModuleVoice> Voices);

/// <summary>The contents of a module's <c>module.json</c>.</summary>
/// <param name="Command">The path as written, relative to the module's folder.</param>
public sealed record ModuleManifest(
    int Contract,
    string Id,
    string Name,
    string Author,
    string Version,
    string Command,
    IReadOnlyList<string> Args,
    int MinimumIntervalSeconds,
    IReadOnlyList<ModulePage> Pages,
    ModuleCharacter? Character)
{
    public const int KnownContract = 1;

    public const int MaxName = Characters.CustomCharacter.MaxName;

    /// <summary>The shortest speaking limit a module may declare.</summary>
    public const int ShortestIntervalSeconds = 120;

    public static bool IsId(string? id) =>
        id is { Length: >= 2 and <= 16 } && char.IsAsciiLetterLower(id[0]) && id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c));
}

public enum ModuleRefusalReason
{
    UnknownContract,
    Unparseable,
    MalformedId,
    DuplicateId,
    CommandOutsideFolder,
    CommandMissing,
    IntervalTooShort,
}

/// <summary>A folder under <c>data\modules</c>.</summary>
public abstract record InstalledModule(string Folder);

/// <param name="CommandPath">The full path of the executable, under <paramref name="Folder"/>.</param>
public sealed record ReadyModule(string Folder, ModuleManifest Manifest, string CommandPath) : InstalledModule(Folder);

public sealed record RefusedModule(string Folder, ModuleRefusalReason Reason, string Detail) : InstalledModule(Folder);
