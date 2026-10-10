using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>What the language model judged a name to suggest.</summary>
public sealed record NameReading(
    [property: System.Text.Json.Serialization.JsonPropertyName("accent")] string Accent,
    [property: System.Text.Json.Serialization.JsonPropertyName("sex")] string Sex)
{
    public const string Female = "female";
    public const string Male = "male";
    public const string Unknown = "unknown";
}

/// <summary>
/// The accent and sex the language model judged a sender's name to suggest, kept per voice provider.
/// Asking runs off the caller's thread; a lookup never waits.
/// </summary>
public sealed class NameAccents
{
    /// <summary>The most names one request carries.</summary>
    public const int NamesPerRequest = 40;

    /// <summary>The longest name that is asked about.</summary>
    public const int LongestName = 60;

    /// <summary>Asks the model about names; null when no answer could be had.</summary>
    public delegate Task<IReadOnlyDictionary<string, NameReading>?> Asker(
        IReadOnlyList<string> names,
        IReadOnlyList<string> accents,
        CancellationToken cancellationToken);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly IFileSystem? _files;
    private readonly string? _file;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, Dictionary<string, NameReading>> _answers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _accents = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Provider, string Name)> _pending = [];
    private readonly HashSet<(string Provider, string Name)> _tried = [];
    private Task _draining = Task.CompletedTask;
    private bool _running;

    /// <summary>Answers are read from <paramref name="file"/> if it exists and written back to it; null keeps them in memory.</summary>
    public NameAccents(IFileSystem? files = null, string? file = null, ILogger? logger = null)
    {
        _files = files;
        _file = file;
        _logger = logger;
        Load();
    }

    /// <summary>The model to ask; while null nothing is asked.</summary>
    public Asker? Ask { get; set; }

    /// <summary>
    /// The reading recorded for a name, or null where it has not been answered. The accent is empty where
    /// the answer was none.
    /// </summary>
    public NameReading? Get(string provider, string name)
    {
        lock (_gate)
        {
            return _answers.TryGetValue(provider, out var names) && names.TryGetValue(name, out var reading)
                ? reading
                : null;
        }
    }

    /// <summary>Queues the names not yet answered for one provider and returns at once.</summary>
    public void Enqueue(string provider, IReadOnlyList<string> accents, IEnumerable<string> names)
    {
        if (accents.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            _accents[provider] = accents;

            foreach (var name in names)
            {
                if (name.Length is 0 or > LongestName
                    || (_answers.TryGetValue(provider, out var known) && known.ContainsKey(name))
                    || _pending.Contains((provider, name))
                    || !_tried.Add((provider, name)))
                {
                    continue;
                }

                _pending.Add((provider, name));
            }

            if (_pending.Count == 0 || _running)
            {
                return;
            }

            _running = true;
            _draining = Task.Run(DrainAsync);
        }
    }

    /// <summary>Completes once every queued name has been asked.</summary>
    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            return _draining;
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            string provider;
            string[] batch;
            IReadOnlyList<string> accents;

            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _running = false;
                    return;
                }

                provider = _pending[0].Provider;
                batch = [.. _pending
                    .Where(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase))
                    .Take(NamesPerRequest)
                    .Select(item => item.Name)];

                _pending.RemoveAll(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase)
                    && batch.Contains(item.Name, StringComparer.OrdinalIgnoreCase));

                accents = _accents[provider];
            }

            IReadOnlyDictionary<string, NameReading>? answered = null;

            try
            {
                if (Ask is { } ask)
                {
                    answered = await ask(batch, accents, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not ask which accents {Count} names suggest", batch.Length);
            }

            if (answered is null)
            {
                continue;
            }

            lock (_gate)
            {
                if (!_answers.TryGetValue(provider, out var names))
                {
                    names = new Dictionary<string, NameReading>(StringComparer.OrdinalIgnoreCase);
                    _answers[provider] = names;
                }

                foreach (var name in batch)
                {
                    var said = answered.GetValueOrDefault(name);

                    names[name] = new NameReading(
                        accents.FirstOrDefault(accent => string.Equals(accent, said?.Accent, StringComparison.OrdinalIgnoreCase))
                            ?? string.Empty,
                        said?.Sex is NameReading.Female or NameReading.Male ? said.Sex : NameReading.Unknown);
                }

                Save();
            }
        }
    }

    private void Load()
    {
        if (_files is null || _file is null)
        {
            return;
        }

        try
        {
            if (_files.ReadText(_file) is not { } text)
            {
                return;
            }

            var stored = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, NameReading>>>(text);

            foreach (var (provider, names) in stored ?? [])
            {
                _answers[provider] = new Dictionary<string, NameReading>(names, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Ignoring the stored name accents in {File}", _file);
        }
    }

    private void Save()
    {
        if (_files is null || _file is null)
        {
            return;
        }

        try
        {
            _files.WriteText(_file, JsonSerializer.Serialize(_answers, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Could not save the name accents to {File}", _file);
        }
    }
}
