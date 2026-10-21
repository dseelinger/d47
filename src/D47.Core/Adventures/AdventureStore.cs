using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>Why a record of the file was refused, naming the record and the problem.</summary>
public sealed record AdventureProblem(string Where, string Reason);

/// <summary>The adventures on disk — <c>data/adventures.json</c> (Phase 47).</summary>
public sealed class AdventureStore(string initialPath, IFileSystem files, ILogger<AdventureStore> logger)
{
    /// <summary>The key used where the journals never said who was flying.</summary>
    public const string NoCommander = "";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();

    /// <summary>Held across each read and write of the file, so a <see cref="Repoint"/> never interleaves with one.</summary>
    private readonly Lock _fileGate = new();

    private string _path = initialPath;
    private Dictionary<string, IReadOnlyList<Adventure>> _byCommander = new(StringComparer.Ordinal);
    private IReadOnlyList<AdventureProblem> _problems = [];
    private string? _seen;
    private readonly FileStamp _stamp = new();

    public string Path
    {
        get
        {
            lock (_gate)
            {
                return _path;
            }
        }
    }

    public IFileSystem Files => files;

    public event Action? Changed;

    public IReadOnlyList<AdventureProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>This Commander's adventures, in the order they were written.</summary>
    public IReadOnlyList<Adventure> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    public Adventure? Find(string? frontierId, string key) =>
        For(frontierId).FirstOrDefault(adventure => string.Equals(adventure.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every Commander with anything on file.</summary>
    public IReadOnlyList<string> Commanders
    {
        get
        {
            lock (_gate)
            {
                return [.. _byCommander.Keys];
            }
        }
    }

    public bool Poll()
    {
        bool changed;

        lock (_fileGate)
        {
            changed = Read();
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return changed;
    }

    /// <summary>Reads adventures from <paramref name="path"/> from now on, dropping everything read from the old file, and raises <see cref="Changed"/>.</summary>
    public void Repoint(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_fileGate)
        {
            lock (_gate)
            {
                _path = path;
                _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(StringComparer.Ordinal);
                _problems = [];
                _seen = null;
            }

            _stamp.Record(null);
            Read();
        }

        Changed?.Invoke();
    }

    /// <summary>Rereads the file when its stamp moved; true when what it holds changed. Called holding <see cref="_fileGate"/>.</summary>
    private bool Read()
    {
        var path = _path;
        var stamp = files.Stat(path);

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

                lock (_gate)
                {
                    _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(StringComparer.Ordinal);
                    _problems = [];
                    _seen = null;
                    _stamp.Record(null);
                }

                return true;
            }

            var read = files.ReadText(path);

            if (read is null)
            {
                return false;
            }

            text = read;
            _stamp.Record(stamp);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read {Path}", path);
            return false;
        }

        if (string.Equals(text, _seen, StringComparison.Ordinal))
        {
            return false;
        }

        Reload(path, text);
        return true;
    }

    /// <summary>Writes one adventure, adding it or replacing the one with its key.</summary>
    public string? Save(string? frontierId, Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        if (AdventureValidation.Problems(adventure) is { Count: > 0 } problems)
        {
            return string.Join(" ", problems);
        }

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var replacing = existing.Any(other => string.Equals(other.Key, adventure.Key, StringComparison.OrdinalIgnoreCase));

            // Story chapters are not counted: a story archives its finished chapters itself.
            if (!replacing && adventure.StoryId is null && existing.Count(other => other.StoryId is null) >= AdventureLimits.MaxAdventures)
            {
                return $"You already have {AdventureLimits.MaxAdventures} stories, which is the most I hold.";
            }

            _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = replacing
                    ?
                    [
                        .. existing.Select(other =>
                            string.Equals(other.Key, adventure.Key, StringComparison.OrdinalIgnoreCase) ? adventure : other),
                    ]
                    : [.. existing, adventure],
            };
        }

        Write();
        Changed?.Invoke();
        return null;
    }

    /// <summary>Deletes one outright.</summary>
    public bool Remove(string? frontierId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var kept = existing
                .Where(adventure => !string.Equals(adventure.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (kept.Count == existing.Count)
            {
                return false;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = kept,
            };
        }

        Write();
        Changed?.Invoke();
        return true;
    }

    /// <summary>The adventure as this file writes it.</summary>
    internal static JsonElement ToJson(Adventure adventure) => JsonSerializer.SerializeToElement(ToRecord(adventure), Json);

    /// <summary>An adventure read as this file reads it, or null with the reason added to <paramref name="problems"/>.</summary>
    internal static Adventure? FromJson(JsonElement element, List<AdventureProblem> problems) =>
        element.Deserialize<AdventureRecord>(Json) is { } record ? FromRecord(record, problems) : null;

    private void Write()
    {
        lock (_fileGate)
        {
            Document document;

            lock (_gate)
            {
                document = new Document
                {
                    Commanders =
                    [
                        .. _byCommander
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Where(pair => pair.Value.Count > 0)
                            .Select(pair => new CommanderRecord
                            {
                                FrontierId = pair.Key,
                                Adventures = [.. pair.Value.Select(ToRecord)],
                            }),
                    ],
                };
            }

            var text = JsonSerializer.Serialize(document, Json);

            try
            {
                files.WriteText(_path, text);

                lock (_gate)
                {
                    _seen = text;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "Could not write {Path}", _path);
            }
        }
    }

    private void Reload(string path, string text)
    {
        var loaded = new Dictionary<string, IReadOnlyList<Adventure>>(StringComparer.Ordinal);
        var problems = new List<AdventureProblem>();

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? NoCommander;
                var kept = new List<Adventure>();

                foreach (var record in commander.Adventures ?? [])
                {
                    var adventure = FromRecord(record, problems);

                    if (adventure is null)
                    {
                        continue;
                    }

                    if (kept.Any(other => string.Equals(other.Key, adventure.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        problems.Add(new AdventureProblem(adventure.Key, "a second story with the same key"));
                        continue;
                    }

                    kept.Add(adventure);
                }

                loaded[key] = kept;
            }
        }
        catch (JsonException ex)
        {
            problems.Add(new AdventureProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            if (problems.Count == 0 || loaded.Count > 0)
            {
                _byCommander = loaded;
            }

            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded adventures for {Commanders} Commander(s) from {Path} ({Problems} refused)",
            loaded.Count,
            path,
            problems.Count);
    }

    private static Adventure? FromRecord(AdventureRecord record, List<AdventureProblem> problems)
    {
        var where = record.Name ?? record.Key ?? "(a story)";

        if (string.IsNullOrWhiteSpace(record.Key) || string.IsNullOrWhiteSpace(record.Name))
        {
            problems.Add(new AdventureProblem(where, "no key or no name"));
            return null;
        }

        var beats = new List<AdventureBeat>();

        foreach (var (beat, index) in (record.Beats ?? []).Select((beat, index) => (beat, index)))
        {
            var steps = new List<BeatStep>();
            List<StepDefRecord> written = beat.Steps is { Count: > 0 }
                ? beat.Steps
                : [new StepDefRecord { Trigger = beat.Trigger, FailsOn = beat.FailsOn, Reason = beat.Reason }];

            foreach (var (step, stepIndex) in written.Select((step, stepIndex) => (step, stepIndex)))
            {
                var at = written.Count > 1 ? $"beat {index + 1} step {stepIndex + 1}" : $"beat {index + 1}";

                if (FromStep(step, at, where, problems) is not { } read)
                {
                    return null;
                }

                steps.Add(read);
            }

            beats.Add(new AdventureBeat
            {
                Title = beat.Title?.Trim() ?? string.Empty,
                Function = beat.Function?.Trim(),
                Lines = beat.Lines is { Count: > 0 } lines
                    ? [.. lines.Select(line => new AdventureLine { Text = line.Text?.Trim() ?? string.Empty, Speaker = line.Speaker, Introduces = line.Introduces })]
                    : [new AdventureLine { Text = beat.Line?.Trim() ?? string.Empty, Speaker = beat.Speaker }],
                Steps = steps,
            });
        }

        var adventure = new Adventure
        {
            Key = record.Key.Trim(),
            Name = record.Name.Trim(),
            Source = Enum.TryParse<AdventureSource>(record.Source, ignoreCase: true, out var source) && Enum.IsDefined(source)
                ? source
                : AdventureSource.Commander,
            Written = record.Written,
            WrittenBy = record.WrittenBy?.Trim(),
            Spine = record.Spine is { } spine
                ? new AdventureSpine
                {
                    Premise = spine.Premise?.Trim(),
                    Want = spine.Want?.Trim(),
                    Stake = spine.Stake?.Trim(),
                    Turn = spine.Turn?.Trim(),
                    Ending = spine.Ending?.Trim(),
                }
                : null,
            OpeningLines = record.OpeningLines is { Count: > 0 } opening
                ? [.. opening.Select(line => new AdventureLine { Text = line.Text?.Trim() ?? string.Empty, Speaker = line.Speaker, Introduces = line.Introduces })]
                : record.Opening is { } first
                    ? [new AdventureLine { Text = first.Trim(), Speaker = record.OpeningSpeaker }]
                    : [],
            Beats = beats,
            AcceptedAt = record.AcceptedAt,
            AbandonedAt = record.AbandonedAt,
            Ending = record.Ending is { Name.Length: > 0, At: not null } ending
                ? new AdventureEnding(ending.Name.Trim(), ending.At.Value, ending.Reason?.Trim() ?? string.Empty)
                : null,
            RewrittenAt = record.RewrittenAt,
            RewrittenFrom = record.RewrittenFrom,
            RewrittenStep = Math.Max(record.RewrittenStep ?? 0, 0),
            Previous = record.Previous is { } previous ? FromRecord(previous, problems) : null,
            Follows = string.IsNullOrWhiteSpace(record.Follows) ? null : record.Follows.Trim(),
            StoryId = string.IsNullOrWhiteSpace(record.StoryId) ? null : record.StoryId.Trim(),
            Stepped = [.. (record.Stepped ?? []).Where(step => step.Beat is { } beat && beat >= 0 && beat < beats.Count && step.At is not null && Math.Max(step.Step ?? 0, 0) < beats[step.Beat!.Value].MostSteps)
                .Select(step => new AdventureStep(step.Beat!.Value, step.At!.Value, string.Equals(step.By?.Trim(), CommanderStep, StringComparison.Ordinal) ? AdventureFold.CommanderSource : null, Math.Max(step.Step ?? 0, 0)))],
            Choices = [.. (record.Choices ?? []).Where(choice => choice is { Beat: >= 0, Option: >= 0, At: not null } && choice.Beat < beats.Count && choice.Option < (beats[choice.Beat!.Value].Steps.FirstOrDefault()?.Options.Count ?? 0))
                .Select(choice => new AdventureChoice(choice.Beat!.Value, choice.Option!.Value, choice.At!.Value))],
            Events = [.. (record.Events ?? []).Where(made => made is { Name.Length: > 0, At: not null })
                .Take(AdventureLimits.MaxEvents)
                .Select(made => new D47Event(
                    made.Name!.Trim(),
                    [.. (made.Fields ?? []).Where(pair => pair.Field is not null && pair.Value is not null).Select(pair => new EventField(pair.Field!.Trim(), pair.Value!.Trim()))],
                    made.At!.Value))],
            Offered = record.Offered is { Beat: >= 0, At: not null } offered ? new AdventureOffer(offered.Beat!.Value, offered.At.Value) : null,
        };

        var refusals = AdventureValidation.Problems(adventure);

        if (refusals.Count > 0)
        {
            problems.Add(new AdventureProblem(where, string.Join(" ", refusals)));
            return null;
        }

        return adventure;
    }

    /// <summary>One step as written, with its options, or null with a problem added when a kind is not one d47 knows.</summary>
    private static BeatStep? FromStep(StepDefRecord step, string at, string where, List<AdventureProblem> problems)
    {
        if (!AdventureValidation.TryKind(step.Trigger?.Kind, out var kind))
        {
            problems.Add(new AdventureProblem(
                where,
                $"{at} names a trigger \"{step.Trigger?.Kind ?? string.Empty}\"; the kinds are "
                + string.Join(", ", AdventureValidation.Kinds)));
            return null;
        }

        var failsOn = new List<AdventureTrigger>();

        foreach (var fail in step.FailsOn ?? [])
        {
            if (!AdventureValidation.TryKind(fail.Kind, out var failKind))
            {
                problems.Add(new AdventureProblem(where, $"{at} names a failsOn \"{fail.Kind ?? string.Empty}\"; the kinds are " + string.Join(", ", AdventureValidation.Kinds)));
                return null;
            }

            failsOn.Add(FromTrigger(fail, failKind));
        }

        var options = new List<BeatOption>();

        foreach (var (option, optionIndex) in (step.Options ?? []).Select((option, optionIndex) => (option, optionIndex)))
        {
            var optionSteps = new List<BeatStep>();

            foreach (var (optionStep, stepIndex) in (option.Steps ?? []).Select((optionStep, stepIndex) => (optionStep, stepIndex)))
            {
                if (FromStep(optionStep, $"{at} option {optionIndex + 1} step {stepIndex + 1}", where, problems) is not { } read)
                {
                    return null;
                }

                optionSteps.Add(read);
            }

            options.Add(new BeatOption
            {
                Label = option.Label?.Trim() ?? string.Empty,
                Steps = optionSteps,
                Ending = string.IsNullOrWhiteSpace(option.Ending) ? null : option.Ending.Trim(),
                EndingLine = string.IsNullOrWhiteSpace(option.EndingLine) ? null : option.EndingLine.Trim(),
            });
        }

        return new BeatStep
        {
            Trigger = FromTrigger(step.Trigger!, kind),
            FailsOn = [.. failsOn],
            Reason = string.IsNullOrWhiteSpace(step.Reason) ? null : step.Reason.Trim(),
            Options = options,
        };
    }

    private static StepDefRecord ToStep(BeatStep step) => new()
    {
        Trigger = ToTrigger(step.Trigger),
        FailsOn = step.FailsOn.Count > 0 ? [.. step.FailsOn.Select(ToTrigger)] : null,
        Reason = step.Reason,
        Options = step.Options.Count > 0
            ? [.. step.Options.Select(option => new OptionRecord { Label = option.Label, Steps = option.Steps.Count > 0 ? [.. option.Steps.Select(ToStep)] : null, Ending = option.Ending, EndingLine = option.EndingLine })]
            : null,
    };

    private static AdventureTrigger FromTrigger(TriggerRecord record, TriggerKind kind) => new()
    {
        Kind = kind,
        SystemAddress = record.SystemAddress,
        MarketId = record.MarketId,
        BodyId = record.BodyId,
        Career = Careers.Match(record.Career) ?? record.Career?.Trim(),
        Rank = record.Rank,
        ShipType = EliteSpecifications.HullSymbol(record.ShipType) ?? record.ShipType?.Trim(),
        System = record.System?.Trim(),
        Station = record.Station?.Trim(),
        Body = record.Body?.Trim(),
        Count = record.Count,
        Faction = record.Faction?.Trim(),
        MissionFamily = record.MissionFamily?.Trim(),
        Handle = record.Handle?.Trim(),
        Commodity = record.Commodity?.Trim(),
        Filter = record.Filter?.Trim(),
        SiteName = record.SiteName?.Trim(),
        Organic = record.Organic,
        New = record.New,
        Engineer = record.Engineer?.Trim(),
        Stage = record.Stage?.Trim(),
        JumpRange = record.JumpRange,
        Event = record.Event?.Trim(),
        Where = new EventFilter((record.Where ?? []).Where(pair => pair.Field is not null && pair.Value is not null).Select(pair => new EventField(pair.Field!.Trim(), pair.Value!.Trim()))),
        Task = record.Task?.Trim(),
    };

    private static TriggerRecord ToTrigger(AdventureTrigger trigger) => new()
    {
        Kind = trigger.Kind.ToString().ToLowerInvariant(),
        SystemAddress = trigger.SystemAddress,
        MarketId = trigger.MarketId,
        BodyId = trigger.BodyId,
        Career = trigger.Career,
        Rank = trigger.Rank,
        ShipType = trigger.ShipType,
        System = trigger.System,
        Station = trigger.Station,
        Body = trigger.Body,
        Count = trigger.Count,
        Faction = trigger.Faction,
        MissionFamily = trigger.MissionFamily,
        Handle = trigger.Handle,
        Commodity = trigger.Commodity,
        Filter = trigger.Filter,
        SiteName = trigger.SiteName,
        Organic = trigger.Organic,
        New = trigger.New,
        Engineer = trigger.Engineer,
        Stage = trigger.Stage,
        JumpRange = trigger.JumpRange,
        Event = trigger.Event,
        Where = trigger.Where.Count > 0 ? [.. trigger.Where.Select(pair => new WhereRecord { Field = pair.Field, Value = pair.Value })] : null,
        Task = trigger.Task,
    };

    private static AdventureRecord ToRecord(Adventure adventure) => new()
    {
        Key = adventure.Key,
        Name = adventure.Name,
        Source = adventure.Source.ToString().ToLowerInvariant(),
        Written = adventure.Written,
        WrittenBy = adventure.WrittenBy,
        Spine = adventure.Spine is { IsEmpty: false } spine
            ? new SpineRecord
            {
                Premise = spine.Premise,
                Want = spine.Want,
                Stake = spine.Stake,
                Turn = spine.Turn,
                Ending = spine.Ending,
            }
            : null,
        Opening = adventure.Opening,
        OpeningSpeaker = adventure.OpeningSpeaker,
        OpeningLines = adventure.OpeningLines.Count > 1
            ? [.. adventure.OpeningLines.Select(line => new LineRecord { Text = line.Text, Speaker = line.Speaker, Introduces = line.Introduces })]
            : null,
        Beats =
        [
            .. adventure.Beats.Select(beat => new BeatRecord
            {
                Title = beat.Title,
                Function = beat.Function,
                Reason = Listed(beat) ? null : beat.Reason,
                Trigger = Listed(beat) ? null : ToTrigger(beat.Trigger),
                FailsOn = Listed(beat) || beat.FailsOn.Count == 0 ? null : [.. beat.FailsOn.Select(ToTrigger)],
                Steps = Listed(beat) ? [.. beat.Steps.Select(ToStep)] : null,
                Line = beat.Lines.Count == 1 ? beat.Line : null,
                Speaker = beat.Lines.Count == 1 ? beat.Speaker : null,
                Lines = beat.Lines.Count == 1 ? null : [.. beat.Lines.Select(line => new LineRecord { Text = line.Text, Speaker = line.Speaker, Introduces = line.Introduces })],
            }),
        ],
        AcceptedAt = adventure.AcceptedAt,
        AbandonedAt = adventure.AbandonedAt,
        Ending = adventure.Ending is { } ended ? new EndingRecord { Name = ended.Name, At = ended.At, Reason = ended.Reason } : null,
        RewrittenAt = adventure.RewrittenAt,
        RewrittenFrom = adventure.RewrittenFrom,
        RewrittenStep = adventure.RewrittenStep > 0 ? adventure.RewrittenStep : null,
        Previous = adventure.Previous is { } previous ? ToRecord(previous) : null,
        Follows = adventure.Follows,
        StoryId = adventure.StoryId,
        Stepped = adventure.Stepped.Count > 0 ? [.. adventure.Stepped.Select(step => new StepRecord { Beat = step.Beat, Step = step.Step > 0 ? step.Step : null, At = step.At, By = step.By == AdventureFold.CommanderSource ? CommanderStep : null })] : null,
        Choices = adventure.Choices.Count > 0 ? [.. adventure.Choices.Select(choice => new ChoiceRecord { Beat = choice.Beat, Option = choice.Option, At = choice.At })] : null,
        Events = adventure.Events.Count > 0
            ? [.. adventure.Events.Select(made => new EventRecord { Name = made.Name, Fields = [.. made.Fields.Select(pair => new WhereRecord { Field = pair.Field, Value = pair.Value })], At = made.At })]
            : null,
        Offered = adventure.Offered is { } offered ? new OfferRecord { Beat = offered.Beat, At = offered.At } : null,
    };

    /// <summary>Whether a beat is written as a list of steps: it has more than one, or one with options.</summary>
    private static bool Listed(AdventureBeat beat) => beat.Steps.Count > 1 || beat.Steps.Any(step => step.Options.Count > 0);

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public IReadOnlyList<AdventureRecord>? Adventures { get; set; }
    }

    private sealed class AdventureRecord
    {
        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? Source { get; set; }

        public DateTimeOffset? Written { get; set; }

        public string? WrittenBy { get; set; }

        public SpineRecord? Spine { get; set; }

        public string? Opening { get; set; }

        public string? OpeningSpeaker { get; set; }

        /// <summary>Written beside <see cref="Opening"/> and <see cref="OpeningSpeaker"/>, which hold its first line, when the opening has more than one line.</summary>
        public List<LineRecord>? OpeningLines { get; set; }

        public IReadOnlyList<BeatRecord>? Beats { get; set; }

        public DateTimeOffset? AcceptedAt { get; set; }

        public DateTimeOffset? AbandonedAt { get; set; }

        public EndingRecord? Ending { get; set; }

        public DateTimeOffset? RewrittenAt { get; set; }

        public int? RewrittenFrom { get; set; }

        public int? RewrittenStep { get; set; }

        public AdventureRecord? Previous { get; set; }

        public string? Follows { get; set; }

        public string? StoryId { get; set; }

        public List<StepRecord>? Stepped { get; set; }

        public List<ChoiceRecord>? Choices { get; set; }

        public List<EventRecord>? Events { get; set; }

        public OfferRecord? Offered { get; set; }
    }

    private sealed class EventRecord
    {
        public string? Name { get; set; }

        public List<WhereRecord>? Fields { get; set; }

        public DateTimeOffset? At { get; set; }
    }

    private sealed class ChoiceRecord
    {
        public int? Beat { get; set; }

        public int? Option { get; set; }

        public DateTimeOffset? At { get; set; }
    }

    private sealed class OfferRecord
    {
        public int? Beat { get; set; }

        public DateTimeOffset? At { get; set; }
    }

    private sealed class OptionRecord
    {
        public string? Label { get; set; }

        public List<StepDefRecord>? Steps { get; set; }

        public string? Ending { get; set; }

        public string? EndingLine { get; set; }
    }

    /// <summary>The <see cref="StepRecord.By"/> written for a step the Commander marked; a step without one is the bench's.</summary>
    private const string CommanderStep = "Commander";

    private sealed class EndingRecord
    {
        public string? Name { get; set; }

        public DateTimeOffset? At { get; set; }

        public string? Reason { get; set; }
    }

    private sealed class StepRecord
    {
        public int? Beat { get; set; }

        public int? Step { get; set; }

        public DateTimeOffset? At { get; set; }

        public string? By { get; set; }
    }

    private sealed class SpineRecord
    {
        public string? Premise { get; set; }

        public string? Want { get; set; }

        public string? Stake { get; set; }

        public string? Turn { get; set; }

        public string? Ending { get; set; }
    }

    private sealed class BeatRecord
    {
        public string? Title { get; set; }

        public string? Function { get; set; }

        public string? Reason { get; set; }

        public TriggerRecord? Trigger { get; set; }

        public List<TriggerRecord>? FailsOn { get; set; }

        /// <summary>Written instead of <see cref="Trigger"/>, <see cref="FailsOn"/> and <see cref="Reason"/> when a beat has more than one step, or one with options.</summary>
        public List<StepDefRecord>? Steps { get; set; }

        public string? Line { get; set; }

        public string? Speaker { get; set; }

        /// <summary>Written instead of <see cref="Line"/> and <see cref="Speaker"/> when a beat has more than one line.</summary>
        public List<LineRecord>? Lines { get; set; }
    }

    private sealed class StepDefRecord
    {
        public TriggerRecord? Trigger { get; set; }

        public List<TriggerRecord>? FailsOn { get; set; }

        public string? Reason { get; set; }

        public List<OptionRecord>? Options { get; set; }
    }

    private sealed class LineRecord
    {
        public string? Text { get; set; }

        public string? Speaker { get; set; }

        public string? Introduces { get; set; }
    }

    private sealed class TriggerRecord
    {
        public string? Kind { get; set; }

        public long? SystemAddress { get; set; }

        public long? MarketId { get; set; }

        public int? BodyId { get; set; }

        public string? Career { get; set; }

        public int? Rank { get; set; }

        public string? ShipType { get; set; }

        public string? System { get; set; }

        public string? Station { get; set; }

        public string? Body { get; set; }

        public int? Count { get; set; }

        public string? Faction { get; set; }

        public string? MissionFamily { get; set; }

        public string? Handle { get; set; }

        public string? Commodity { get; set; }

        public string? Filter { get; set; }

        public bool? Organic { get; set; }

        public bool? New { get; set; }

        public double? JumpRange { get; set; }

        public string? Engineer { get; set; }

        public string? Stage { get; set; }

        public string? SiteName { get; set; }

        public string? Event { get; set; }

        public List<WhereRecord>? Where { get; set; }

        public string? Task { get; set; }
    }

    private sealed class WhereRecord
    {
        public string? Field { get; set; }

        public string? Value { get; set; }
    }
}
