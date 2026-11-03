using System.Text.Json;
using D47.Core.Storage;

namespace D47.Core.Modules;

/// <summary>The modules installed under <c>data\modules</c>. Reads when asked and does not watch the folder.</summary>
public sealed class ModuleCatalog(IFileSystem files, string modulesFolder)
{
    public const string ManifestFile = "module.json";

    private volatile IReadOnlyList<InstalledModule> _modules = [];

    public IReadOnlyList<InstalledModule> Modules => _modules;

    /// <summary>Reads every <c>module.json</c> one folder below the modules folder.</summary>
    public IReadOnlyList<InstalledModule> Reload()
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modulesFolder));
        var candidates = new List<(string Folder, ModuleManifest Manifest)>();
        var found = new List<InstalledModule>();

        foreach (var file in files.Enumerate(root, ManifestFile, recursive: true))
        {
            var folder = Path.GetDirectoryName(file)!;

            if (!string.Equals(Path.GetDirectoryName(folder), root, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var (manifest, refusal) = Parse(files.ReadText(file));

            if (manifest is null)
            {
                found.Add(new RefusedModule(folder, refusal!.Value.Reason, refusal.Value.Detail));
            }
            else
            {
                candidates.Add((folder, manifest));
            }
        }

        var counts = candidates
            .GroupBy(c => c.Manifest.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var (folder, manifest) in candidates)
        {
            found.Add(counts[manifest.Id] > 1
                ? new RefusedModule(folder, ModuleRefusalReason.DuplicateId, $"Another module also uses the id {manifest.Id}.")
                : Check(folder, manifest));
        }

        _modules = [.. found.OrderBy(m => m.Folder, StringComparer.OrdinalIgnoreCase)];
        return _modules;
    }

    private InstalledModule Check(string folder, ModuleManifest manifest)
    {
        var command = manifest.Command;
        string full;

        try
        {
            full = Path.GetFullPath(Path.Combine(folder, command));
        }
        catch (ArgumentException)
        {
            return new RefusedModule(folder, ModuleRefusalReason.CommandOutsideFolder, "The command is not a valid path.");
        }

        var inside = full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        if (Path.IsPathRooted(command) || !inside)
        {
            return new RefusedModule(folder, ModuleRefusalReason.CommandOutsideFolder, "The command must be a path inside the module's folder.");
        }

        if (!full.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || files.Stat(full) is null)
        {
            return new RefusedModule(folder, ModuleRefusalReason.CommandMissing, "The command must be an .exe that exists.");
        }

        return manifest.MinimumIntervalSeconds < ModuleManifest.ShortestIntervalSeconds
            ? new RefusedModule(folder, ModuleRefusalReason.IntervalTooShort, $"The minimum interval is below {ModuleManifest.ShortestIntervalSeconds} seconds.")
            : new ReadyModule(folder, manifest, full);
    }

    private static (ModuleManifest? Manifest, (ModuleRefusalReason Reason, string Detail)? Refusal) Parse(string? text)
    {
        static (ModuleManifest?, (ModuleRefusalReason, string)?) Bad(string detail) =>
            (null, (ModuleRefusalReason.Unparseable, detail));

        if (text is null)
        {
            return Bad("The file could not be read.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                text,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Bad("The manifest is not an object.");
            }

            if (!root.TryGetProperty("contract", out var contractElement) || !contractElement.TryGetInt32(out var contract))
            {
                return Bad("contract is missing or not an integer.");
            }

            if (contract != ModuleManifest.KnownContract)
            {
                return (null, (ModuleRefusalReason.UnknownContract, $"Contract {contract} is not known."));
            }

            var id = Text(root, "id");
            var name = Text(root, "name");
            var author = Text(root, "author");
            var version = Text(root, "version");
            var command = Text(root, "command");

            if (name is null || author is null || version is null || command is null)
            {
                return Bad("name, author, version and command must be non-empty text.");
            }

            if (name.Length > ModuleManifest.MaxName)
            {
                return Bad($"name is longer than {ModuleManifest.MaxName} characters.");
            }

            if (!root.TryGetProperty("minimumIntervalSeconds", out var interval) || !interval.TryGetInt32(out var seconds))
            {
                return Bad("minimumIntervalSeconds is missing or not an integer.");
            }

            var args = List(root, "args", e => e.ValueKind == JsonValueKind.String ? e.GetString() : null);
            var pages = List(root, "pages", e => Text(e, "id") is { } pageId && Text(e, "title") is { } title ? new ModulePage(pageId, title) : null);

            if (args is null || pages is null)
            {
                return Bad("args must be a list of text and pages a list of {id, title}.");
            }

            ModuleCharacter? character = null;

            if (root.TryGetProperty("character", out var characterElement) && characterElement.ValueKind != JsonValueKind.Null)
            {
                var isObject = characterElement.ValueKind == JsonValueKind.Object;
                var characterName = isObject ? Text(characterElement, "name") : null;
                var voices = isObject
                    ? List(characterElement, "voices", e => Text(e, "provider") is { } provider && Text(e, "voice") is { } voice ? new ModuleVoice(provider, voice) : null)
                    : null;

                if (characterName is null || characterName.Length > ModuleManifest.MaxName || voices is null)
                {
                    return Bad($"character must have a name of up to {ModuleManifest.MaxName} characters and a list of {{provider, voice}}.");
                }

                character = new ModuleCharacter(characterName, voices);
            }

            if (!ModuleManifest.IsId(id))
            {
                return (null, (ModuleRefusalReason.MalformedId, "The id must be a lowercase letter then up to 15 lowercase letters or digits."));
            }

            return (new ModuleManifest(contract, id!, name, author, version, command, args, seconds, pages, character), null);
        }
        catch (JsonException)
        {
            return Bad("The file is not valid JSON.");
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>The converted items, empty when the property is absent, or null when it or any item is malformed.</summary>
    private static List<T>? List<T>(JsonElement element, string property, Func<JsonElement, T?> convert)
        where T : class
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<T>();

        foreach (var item in value.EnumerateArray())
        {
            if (convert(item) is not { } converted)
            {
                return null;
            }

            items.Add(converted);
        }

        return items;
    }
}
