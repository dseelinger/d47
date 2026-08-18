using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Checklists;

/// <summary>
/// The Commander's checklist, in one file beside the executable (list.md Phase 17).
/// <para>
/// <b><see cref="Actions.MacroStore"/> is the precedent and this follows it deliberately.</b> It
/// is the repo's only Commander-authored, mutable, list-shaped state, and everything that makes
/// it work applies here unchanged: a file under <c>data/</c>, polled on its write time so a hand
/// edit is live with no restart, saved through <see cref="AtomicFile"/>, and <b>problems reported
/// rather than items silently dropped</b> — a checklist that quietly loses a line is worse than
/// one that refuses it out loud.
/// </para>
/// <para>
/// <b>This file is the Commander's, and nothing the model can call writes it.</b> Model-callable
/// tools write <see cref="ChecklistProposalStore"/> instead, which is a second file for exactly
/// that reason: the trust boundary is inspectable by opening <c>data/</c>. Anything the model can
/// call, a hostile in-game message can attempt to invoke, and the worst that achieves here is a
/// proposal that gets declined.
/// </para>
/// <para>
/// <b>Keyed per Commander, with the key inside the document rather than in the path.</b> The
/// Frontier id comes out of the journal and journal content is untrusted input; turning it into a
/// filename buys a path-traversal surface for an organisational convenience.
/// </para>
/// </summary>
public sealed class ChecklistStore(string path, ILogger<ChecklistStore> logger)
{
    /// <summary>
    /// How a checklist is written and read. Internal rather than private since the export and
    /// import pair round-trips through it (remediation.md 10, item 15) -- one set of options, so
    /// a file d47 wrote is a file d47 reads.
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private IReadOnlyList<ChecklistDocument> _documents = [];
    private IReadOnlyList<ChecklistProblem> _problems = [];
    private DateTime _stamp;

    public string Path => path;

    /// <summary>Raised when the contents changed, whoever changed them. The panel follows this.</summary>
    public event Action? Changed;

    public IReadOnlyList<ChecklistDocument> Documents
    {
        get
        {
            lock (_gate)
            {
                return _documents;
            }
        }
    }

    /// <summary>Lines that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<ChecklistProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>
    /// The document for one Commander, or an empty one. Never null: a Commander with no checklist
    /// yet is the ordinary state on a fresh install, not a missing thing to guard against.
    /// </summary>
    public ChecklistDocument For(string? fid, string? name = null)
    {
        var key = fid ?? string.Empty;

        return Documents.FirstOrDefault(
                   document => string.Equals(document.CommanderFid, key, StringComparison.Ordinal))
               ?? ChecklistDocument.For(key, name);
    }

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core:
    /// the tick loop calls it, so a checklist edited in a text editor is live without a restart.
    /// </summary>
    public bool Poll()
    {
        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                // Not an error: no checklist is the normal state on a fresh install.
                if (_stamp == default)
                {
                    return false;
                }

                lock (_gate)
                {
                    _documents = [];
                    _problems = [];
                    _stamp = default;
                }

                Changed?.Invoke();
                return true;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat the checklist file");
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        Reload(written);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Changes one Commander's document and writes it. The single write path: the panel and the
    /// voice path both come through here, so there is one place that validates and one place that
    /// saves.
    /// </summary>
    public ChecklistChange Apply(
        string? fid,
        string? name,
        Func<ChecklistDocument, ChecklistChange> change)
    {
        // Poll first, so a hand edit made since the last tick is not overwritten by a change
        // computed against a stale copy.
        Poll();

        var before = For(fid, name);
        var result = change(before);

        if (!result.Changed)
        {
            return result;
        }

        var updated = result.Document with { CommanderName = name ?? result.Document.CommanderName };

        var others = Documents
            .Where(document => !string.Equals(document.CommanderFid, updated.CommanderFid, StringComparison.Ordinal))
            .ToList();

        Save([.. others, updated]);

        return result;
    }

    /// <summary>
    /// Writes the file. The panel writes through <see cref="Apply"/>; this is equally the shape a
    /// text editor writes, and both routes land in the same place.
    /// </summary>
    public void Save(IReadOnlyList<ChecklistDocument> documents)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(
            path,
            JsonSerializer.Serialize(new ChecklistFile { Commanders = [.. documents] }, Json));

        // Forces the next Poll to re-read rather than trusting what was just written, so the
        // in-memory set is always the validated one rather than the one that was submitted.
        _stamp = default;
        Poll();
    }

    private void Reload(DateTime written)
    {
        ChecklistFile? file;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            file = JsonSerializer.Deserialize<ChecklistFile>(stream, Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // The whole file being unreadable is one problem with one name, not a reason to
            // throw: d47 stays usable and says what is wrong with the file.
            lock (_gate)
            {
                _documents = [];
                _problems = [new ChecklistProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _stamp = written;
            }

            logger.LogWarning(ex, "The checklist file could not be read");
            return;
        }

        var accepted = new List<ChecklistDocument>();
        var problems = new List<ChecklistProblem>();
        var commanders = new HashSet<string>(StringComparer.Ordinal);

        foreach (var document in file?.Commanders ?? [])
        {
            // An empty id is a real state rather than a bad one: d47 can be running before Elite
            // is, and the Frontier id only exists once a journal has been read. Notes taken then
            // land in the unowned document and are adopted by the first Commander to appear —
            // see ChecklistService.Poll. Refusing them here would lose them silently, which is
            // the one thing this store exists not to do.
            var fid = document.CommanderFid?.Trim() ?? string.Empty;

            if (!commanders.Add(fid))
            {
                problems.Add(new ChecklistProblem(
                    fid.Length == 0 ? "a checklist with no Commander" : fid,
                    "There are two checklists for this Commander; the second was ignored."));
                continue;
            }

            var items = new List<ChecklistItem>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in document.Items ?? [])
            {
                if (ChecklistValidation.Problem(item) is { } reason)
                {
                    problems.Add(new ChecklistProblem(Describe(item), reason));
                    continue;
                }

                // Added as it is accepted, so a file holding the same item twice reports the
                // second rather than silently preferring one of them.
                if (!keys.Add($"{item.Scope.Group}|{item.Scope.Key}|{item.Key}"))
                {
                    problems.Add(new ChecklistProblem(Describe(item), "Two items share one key, so the second was ignored."));
                    continue;
                }

                items.Add(item);
            }

            accepted.Add(document with { CommanderFid = fid, Items = items });
        }

        lock (_gate)
        {
            _documents = accepted;
            _problems = problems;
            _stamp = written;
        }

        logger.LogInformation(
            "Loaded {Count} checklists from {Path} ({Items} items, {Problems} refused)",
            accepted.Count,
            path,
            accepted.Sum(document => document.Items.Count),
            problems.Count);
    }

    private static string Describe(ChecklistItem? item) =>
        item is null ? "an item" : $"\"{item.Text}\"";

    private sealed class ChecklistFile
    {
        public List<ChecklistDocument> Commanders { get; set; } = [];
    }
}

/// <summary>
/// Checking an item before it is allowed to exist, on load and on write alike.
/// <para>
/// <b>The two kinds are checked against each other</b>, which is the one rule the whole design
/// rests on: a derived item without an intent cannot be evaluated and would sit on the list
/// forever, and an authored item carrying one would look computable and be a tick nobody checked.
/// </para>
/// </summary>
public static class ChecklistValidation
{
    /// <summary>Null when the item is fine, or the reason it is not.</summary>
    public static string? Problem(ChecklistItem? item)
    {
        if (item is null)
        {
            return "The item could not be read at all.";
        }

        if (string.IsNullOrWhiteSpace(item.Key))
        {
            return "It has no key, so nothing can say which item it is across a revision.";
        }

        if (string.IsNullOrWhiteSpace(item.Text))
        {
            return $"\"{item.Key}\" has nothing written on it.";
        }

        if (item.Text.Length > ChecklistLimits.MaxTextLength)
        {
            return $"\"{item.Key}\" is {item.Text.Length} characters; the most is {ChecklistLimits.MaxTextLength}.";
        }

        if (item.Scope.Group != ChecklistGroup.Universal && string.IsNullOrWhiteSpace(item.Scope.Key))
        {
            return $"\"{item.Text}\" says it belongs to a {item.Scope.Group.ToString().ToLowerInvariant()} and does not say which.";
        }

        if (item.Kind == ChecklistItemKind.Derived && item.Intent is null)
        {
            return $"\"{item.Text}\" is worked out from the journal but says nothing about what to look for.";
        }

        if (item.Kind == ChecklistItemKind.Authored && item.Intent is not null)
        {
            return $"\"{item.Text}\" is a written line carrying a computed intent, which would make it a tick nobody checked.";
        }

        if (item.Kind == ChecklistItemKind.Authored && item.Source != ChecklistSource.Commander)
        {
            return $"\"{item.Text}\" is a written line claiming to come from a plan.";
        }

        if (item.Intent is { Grade: { } grade } && grade is < 1 or > 5)
        {
            return $"\"{item.Text}\" asks for grade {grade}, and grades run 1 to 5.";
        }

        return null;
    }
}
