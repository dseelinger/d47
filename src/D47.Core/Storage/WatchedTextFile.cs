using Microsoft.Extensions.Logging;

namespace D47.Core.Storage;

/// <summary>Follows one text file: skips a poll while its stamp is unchanged, and reloads only when its text differs from the last text seen.</summary>
public sealed class WatchedTextFile(IFileSystem files, string path, ILogger logger, string readFailure = "Could not read {Path}")
{
    private readonly FileStamp _stamp = new();
    private string? _seen;

    /// <summary>The file followed; changing it does not forget the stamp, so call <see cref="Forget"/> too.</summary>
    public string Path { get; set; } = path;

    /// <summary>Records <paramref name="text"/> as seen, so a write this instance made does not read back as another editor's.</summary>
    public void Seen(string text) => _seen = text;

    /// <summary>Forgets the stamp and the text, so the next poll reads the file afresh.</summary>
    public void Forget()
    {
        _seen = null;
        _stamp.Record(null);
    }

    /// <summary>
    /// Calls <paramref name="cleared"/> when a file that was read has gone, and <paramref name="reload"/> with the
    /// text when it changed. Returns true after <paramref name="cleared"/>, and what <paramref name="reload"/> returns otherwise.
    /// </summary>
    public bool Poll(Action cleared, Func<string, bool> reload)
    {
        var file = Path;
        var stamp = files.Stat(file);

        if (_stamp.Matches(stamp))
        {
            return false;
        }

        string text;

        try
        {
            if (stamp is null)
            {
                if (_seen is null)
                {
                    return false;
                }

                Forget();
                cleared();
                return true;
            }

            var read = files.ReadText(file);

            if (read is null)
            {
                return false;
            }

            text = read;
            _stamp.Record(stamp);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, readFailure, file);
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && reload(text);
    }
}
