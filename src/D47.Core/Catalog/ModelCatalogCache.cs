using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Catalog;

/// <summary>
/// Keeps the fetched model catalog in <c>data\model-catalog.json</c> and puts the newest valid copy
/// in use: fetched, cached or embedded, by <see cref="ModelCatalog.Published"/>.
/// </summary>
/// <remarks>Safe to call from the thread pool while a tick or a turn reads the source.</remarks>
public sealed class ModelCatalogCache(
    ModelCatalogSource source,
    ModelCatalog embedded,
    IFileSystem files,
    string path,
    ILogger<ModelCatalogCache> logger)
{
    public const string FileName = "model-catalog.json";

    private readonly Lock _gate = new();

    private string? _lastRefusal;

    /// <summary>Puts the newer of the cached and embedded catalogs in use; the cached one wins a tie.</summary>
    public void Load()
    {
        lock (_gate)
        {
            var cached = ReadCached();
            var chosen = cached is not null && cached.Published >= embedded.Published ? cached : embedded;

            source.Replace(chosen);
        }
    }

    /// <summary>
    /// Takes a fetched catalog: refused and logged if <see cref="ModelCatalog.Parse"/> refuses it,
    /// otherwise written to disk unless the cached copy is newer, and put in use unless the one in use
    /// is newer.
    /// </summary>
    /// <returns>Whether the fetched catalog was valid.</returns>
    public bool Offer(string json)
    {
        ModelCatalog fetched;

        try
        {
            fetched = ModelCatalog.Parse(json);
        }
        catch (FormatException ex)
        {
            lock (_gate)
            {
                if (ex.Message != _lastRefusal)
                {
                    _lastRefusal = ex.Message;
                    logger.LogWarning("The fetched model catalog was not used: {Reason}", ex.Message);
                }
            }

            return false;
        }

        lock (_gate)
        {
            _lastRefusal = null;

            if (ReadCached() is not { } cached || fetched.Published >= cached.Published)
            {
                Write(json);
            }

            if (fetched.Published >= source.Current.Published)
            {
                source.Replace(fetched);
                logger.LogInformation("Using the model catalog published {Published}", fetched.Published);
            }
        }

        return true;
    }

    private ModelCatalog? ReadCached()
    {
        try
        {
            return files.ReadText(path) is { } text ? ModelCatalog.Parse(text) : null;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("The cached model catalog at {Path} was not used: {Reason}", path, ex.Message);
            return null;
        }
    }

    private void Write(string json)
    {
        try
        {
            files.WriteText(path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The fetched model catalog could not be written to {Path}", path);
        }
    }
}
