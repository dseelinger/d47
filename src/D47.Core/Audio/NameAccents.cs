using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>What the language model judged a name to suggest. Sex is read only from files written before sex was kept by given name.</summary>
public sealed record NameReading(
    [property: System.Text.Json.Serialization.JsonPropertyName("accent")] string Accent,
    [property: System.Text.Json.Serialization.JsonPropertyName("sex")] string Sex)
{
    public const string Female = "female";
    public const string Male = "male";
    public const string Unknown = "unknown";
    public const string Indeterminate = "indeterminate";
}

/// <summary>What the model said to one request: accent by full name, sex by given name.</summary>
public sealed record NameAnswers(
    IReadOnlyDictionary<string, NameReading> Names,
    IReadOnlyDictionary<string, string> GivenNames);

/// <summary>
/// The accent the language model judged a sender's name to suggest, kept per full name and voice provider, and the
/// sex it judged a given name to suggest, kept per given name. Asking runs off the caller's thread; a lookup never
/// waits.
/// </summary>
public sealed class NameAccents
{
    /// <summary>The most names one request carries.</summary>
    public const int NamesPerRequest = 40;

    /// <summary>The longest name that is asked about.</summary>
    public const int LongestName = 60;

    /// <summary>The longest a line waits for the sex of its sender's given name.</summary>
    public static readonly TimeSpan AnswerCeiling = TimeSpan.FromSeconds(5);

    /// <summary>Asks the model for the accent of full names and the sex of given names; null when no answer could be had.</summary>
    public delegate Task<NameAnswers?> Asker(
        IReadOnlyList<string> names,
        IReadOnlyList<string> givenNames,
        IReadOnlyList<string> accents,
        CancellationToken cancellationToken);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly IFileSystem? _files;
    private readonly string? _file;
    private readonly string? _givenFile;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, Dictionary<string, NameReading>> _answers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _accents = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Provider, string Name)> _pending = [];
    private readonly HashSet<(string Provider, string Name)> _tried = [];
    private readonly List<string> _pendingGiven = [];
    private readonly HashSet<string> _givenTried = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskCompletionSource> _givenWaits = new(StringComparer.OrdinalIgnoreCase);
    private string _givenProvider = string.Empty;
    private Task _draining = Task.CompletedTask;
    private bool _running;

    /// <summary>
    /// Accents are read from <paramref name="file"/> and sexes from <paramref name="givenFile"/> if they exist, and
    /// written back; null keeps them in memory. A sex stored in <paramref name="file"/> seeds its given name.
    /// </summary>
    public NameAccents(IFileSystem? files = null, string? file = null, ILogger? logger = null, string? givenFile = null)
    {
        _files = files;
        _file = file;
        _givenFile = givenFile;
        _logger = logger;
        Load();
    }

    /// <summary>The model to ask; while null nothing is asked.</summary>
    public Asker? Ask { get; set; }

    /// <summary>The first word of a name of two or more words; a one-word name is its own given name.</summary>
    public static string GivenNameOf(string name) =>
        name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is [var first, ..]
            ? first.Trim('.', ',', '"', '\'', '`')
            : string.Empty;

    /// <summary>The sex recorded for a given name (male, female or indeterminate), or null where unanswered.</summary>
    public string? SexOfGivenName(string givenName)
    {
        lock (_gate)
        {
            return _sexes.TryGetValue(givenName, out var sex) ? sex : null;
        }
    }

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

    /// <summary>
    /// Null where a line from <paramref name="fullName"/> need not wait: no model, the given name already answered,
    /// or asked and not answered. Otherwise a task that completes when the given name's sex is answered or the
    /// question fails; the question is queued if it is not already.
    /// </summary>
    public Task? WhenGivenNameAnswered(string provider, IReadOnlyList<string> accents, string fullName)
    {
        var given = GivenNameOf(fullName);

        if (given.Length is 0 or > LongestName || accents.Count == 0 || Ask is null)
        {
            return null;
        }

        lock (_gate)
        {
            if (_sexes.ContainsKey(given))
            {
                return null;
            }

            if (_givenWaits.TryGetValue(given, out var waiting))
            {
                return waiting.Task;
            }

            if (_givenTried.Contains(given))
            {
                return null;
            }

            EnqueueLocked(provider, accents, [fullName]);

            return _givenWaits.TryGetValue(given, out var queued) ? queued.Task : null;
        }
    }

    /// <summary>Queues the names not yet answered for one provider, with their given names, and returns at once.</summary>
    public void Enqueue(string provider, IReadOnlyList<string> accents, IEnumerable<string> names)
    {
        lock (_gate)
        {
            EnqueueLocked(provider, accents, names);
        }
    }

    private void EnqueueLocked(string provider, IReadOnlyList<string> accents, IEnumerable<string> names)
    {
        if (accents.Count == 0)
        {
            return;
        }

        _accents[provider] = accents;

        foreach (var name in names)
        {
            var given = GivenNameOf(name);

            if (name.Length is > 0 and <= LongestName
                && given.Length > 0
                && !_sexes.ContainsKey(given)
                && _givenTried.Add(given))
            {
                _pendingGiven.Add(given);
                _givenProvider = provider;
                _givenWaits[given] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            if (name.Length is 0 or > LongestName
                || (_answers.TryGetValue(provider, out var known) && known.ContainsKey(name))
                || _pending.Contains((provider, name))
                || !_tried.Add((provider, name)))
            {
                continue;
            }

            _pending.Add((provider, name));
        }

        if ((_pending.Count == 0 && _pendingGiven.Count == 0) || _running)
        {
            return;
        }

        _running = true;
        _draining = Task.Run(DrainAsync);
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
            string[] givenBatch;
            IReadOnlyList<string> accents;

            lock (_gate)
            {
                if (_pending.Count == 0 && _pendingGiven.Count == 0)
                {
                    _running = false;
                    return;
                }

                provider = _pending.Count > 0 ? _pending[0].Provider : _givenProvider;
                batch = [.. _pending
                    .Where(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase))
                    .Take(NamesPerRequest)
                    .Select(item => item.Name)];

                _pending.RemoveAll(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase)
                    && batch.Contains(item.Name, StringComparer.OrdinalIgnoreCase));

                givenBatch = [.. _pendingGiven.Take(NamesPerRequest)];
                _pendingGiven.RemoveRange(0, givenBatch.Length);

                accents = _accents[provider];
            }

            NameAnswers? answered = null;

            try
            {
                if (Ask is { } ask)
                {
                    answered = await ask(batch, givenBatch, accents, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not ask which accents {Count} names suggest", batch.Length);
            }

            lock (_gate)
            {
                if (answered is not null)
                {
                    Record(provider, batch, givenBatch, accents, answered);
                }

                foreach (var given in givenBatch)
                {
                    if (_givenWaits.Remove(given, out var waiting))
                    {
                        waiting.TrySetResult();
                    }
                }
            }
        }
    }

    private void Record(
        string provider,
        string[] batch,
        string[] givenBatch,
        IReadOnlyList<string> accents,
        NameAnswers answered)
    {
        if (!_answers.TryGetValue(provider, out var names))
        {
            names = new Dictionary<string, NameReading>(StringComparer.OrdinalIgnoreCase);
            _answers[provider] = names;
        }

        foreach (var name in batch)
        {
            var said = answered.Names.GetValueOrDefault(name);

            names[name] = new NameReading(
                accents.FirstOrDefault(accent => string.Equals(accent, said?.Accent, StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty,
                NameReading.Unknown);
        }

        foreach (var given in givenBatch)
        {
            var sex = answered.GivenNames.GetValueOrDefault(given);

            _sexes[given] = sex is NameReading.Female or NameReading.Male ? sex : NameReading.Indeterminate;
        }

        Save();
    }

    private void Load()
    {
        if (_files is null)
        {
            return;
        }

        try
        {
            if (_file is not null && _files.ReadText(_file) is { } text)
            {
                var stored = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, NameReading>>>(text);

                foreach (var (provider, names) in stored ?? [])
                {
                    _answers[provider] = new Dictionary<string, NameReading>(names, StringComparer.OrdinalIgnoreCase);

                    foreach (var (name, reading) in names)
                    {
                        if (reading.Sex is NameReading.Female or NameReading.Male
                            && GivenNameOf(name) is { Length: > 0 } given)
                        {
                            _sexes.TryAdd(given, reading.Sex);
                        }
                    }
                }
            }

            if (_givenFile is not null && _files.ReadText(_givenFile) is { } givenText)
            {
                foreach (var (given, sex) in JsonSerializer.Deserialize<Dictionary<string, string>>(givenText) ?? [])
                {
                    _sexes[given] = sex;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Ignoring the stored name accents in {File}", _file);
        }
    }

    private void Save()
    {
        if (_files is null)
        {
            return;
        }

        try
        {
            if (_file is not null)
            {
                _files.WriteText(_file, JsonSerializer.Serialize(_answers, Json));
            }

            if (_givenFile is not null)
            {
                _files.WriteText(_givenFile, JsonSerializer.Serialize(_sexes, Json));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Could not save the name accents to {File}", _file);
        }
    }
}
