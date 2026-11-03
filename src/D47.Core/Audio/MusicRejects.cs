using D47.Core.Storage;

namespace D47.Core.Audio;

/// <summary>The maintainer's token for the music library: the stored secret, else the environment variable.</summary>
public static class MusicPublishToken
{
    public const string SecretName = "music-publish-token";

    public const string Variable = "D47_MUSIC_PUBLISH_TOKEN";

    /// <summary>The stored token, else <see cref="Variable"/>; null when neither holds one.</summary>
    public static string? Resolve(string? stored, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        var fromEnvironment = environment(Variable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment.Trim();
    }

    /// <summary>Whether a token is stored or in <see cref="Variable"/>.</summary>
    public static bool Present(bool stored, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return stored || !string.IsNullOrWhiteSpace(environment(Variable));
    }
}

/// <summary>
/// The library tracks the maintainer rejected, one row per track in <c>music-rejects.tsv</c>: folder, a tab,
/// then the file name. Read afresh on each call, since another process removes rows. Safe to call from any thread.
/// </summary>
public sealed class MusicRejects(string path, IFileSystem files)
{
    private readonly Lock _gate = new();

    /// <summary>Raised after a row is added or removed, on the calling thread.</summary>
    public event Action? Changed;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return Load().Count;
            }
        }
    }

    public bool Contains(string folder, string file)
    {
        lock (_gate)
        {
            return Load().Contains((folder, file));
        }
    }

    /// <summary>Queues a track. False when it was already queued.</summary>
    public bool Add(string folder, string file)
    {
        lock (_gate)
        {
            if (!Load().Add((folder, file)))
            {
                return false;
            }

            files.AppendText(path, $"{folder}\t{file}\n");
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Every queued track.</summary>
    public IReadOnlyList<(string Folder, string File)> Rows
    {
        get
        {
            lock (_gate)
            {
                return Load().ToList();
            }
        }
    }

    /// <summary>Removes these rows and keeps every other, including rows added since they were read.</summary>
    public void Remove(IEnumerable<(string Folder, string File)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        lock (_gate)
        {
            var gone = new HashSet<(string Folder, string File)>(rows, new RowComparer());

            if (gone.Count == 0)
            {
                return;
            }

            var kept = Load().Where(row => !gone.Contains(row)).ToList();
            files.WriteText(path, string.Concat(kept.Select(row => $"{row.Folder}\t{row.File}\n")));
        }

        Changed?.Invoke();
    }

    private HashSet<(string Folder, string File)> Load()
    {
        var rows = new HashSet<(string Folder, string File)>(new RowComparer());

        foreach (var line in (files.ReadText(path) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var cells = line.TrimEnd('\r').Split('\t');

            if (cells.Length == 2 && cells[0].Length > 0 && cells[1].Length > 0)
            {
                rows.Add((cells[0], cells[1]));
            }
        }

        return rows;
    }

    private sealed class RowComparer : IEqualityComparer<(string Folder, string File)>
    {
        public bool Equals((string Folder, string File) x, (string Folder, string File) y) =>
            string.Equals(x.Folder, y.Folder, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.File, y.File, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Folder, string File) row) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(row.Folder),
                StringComparer.OrdinalIgnoreCase.GetHashCode(row.File));
    }
}
