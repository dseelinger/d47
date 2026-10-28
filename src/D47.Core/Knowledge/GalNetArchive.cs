using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>Every GalNet article fetched, kept in one file. Safe to call from any thread.</summary>
public sealed class GalNetArchive(string path, IFileSystem files, ILogger<GalNetArchive> logger)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Lock _gate = new();

    private Dictionary<string, GalNetArticle>? _articles;

    /// <summary>All articles, newest first.</summary>
    public IReadOnlyList<GalNetArticle> All()
    {
        lock (_gate)
        {
            return Sorted(Load());
        }
    }

    /// <summary>Whether the archive holds no article.</summary>
    public bool IsEmpty
    {
        get
        {
            lock (_gate)
            {
                return Load().Count == 0;
            }
        }
    }

    /// <summary>Whether an article with this id is held.</summary>
    public bool Contains(string id)
    {
        lock (_gate)
        {
            return Load().ContainsKey(id);
        }
    }

    /// <summary>Adds the articles, replacing any held under the same id, and saves. Returns those whose id was not held.</summary>
    public IReadOnlyList<GalNetArticle> Merge(IEnumerable<GalNetArticle> incoming)
    {
        lock (_gate)
        {
            var held = Load();
            var added = new List<GalNetArticle>();

            foreach (var article in incoming)
            {
                if (held.TryAdd(article.Id, article))
                {
                    added.Add(article);
                }
                else
                {
                    held[article.Id] = article;
                }
            }

            files.WriteText(path, JsonSerializer.Serialize(Sorted(held), Json));
            return [.. added.OrderByDescending(article => article.Published)];
        }
    }

    private static List<GalNetArticle> Sorted(Dictionary<string, GalNetArticle> held) =>
        [.. held.Values.OrderByDescending(article => article.Published).ThenBy(article => article.Id, StringComparer.Ordinal)];

    /// <summary>Copies an unreadable file aside so the next save does not replace it.</summary>
    private void Keep()
    {
        try
        {
            files.Copy(path, path + ".unreadable");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("The unreadable GalNet archive could not be kept: {Reason}", ex.GetType().Name);
        }
    }

    private Dictionary<string, GalNetArticle> Load()
    {
        if (_articles is not null)
        {
            return _articles;
        }

        var held = new Dictionary<string, GalNetArticle>(StringComparer.Ordinal);

        try
        {
            if (files.ReadText(path) is { } text
                && JsonSerializer.Deserialize<List<GalNetArticle>>(text, Json) is { } stored)
            {
                foreach (var article in stored.Where(article => !string.IsNullOrEmpty(article.Id)))
                {
                    held[article.Id] = article;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("The GalNet archive could not be read: {Reason}", ex.GetType().Name);
            Keep();
        }

        return _articles = held;
    }
}
