using System.Collections.Concurrent;
using System.Globalization;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>
/// Sends the shown Commander's live colonisation events to the planner while both
/// <see cref="KnowledgeSettings.ColonisationPlanner"/> and <see cref="KnowledgeSettings.ColonisationProgress"/>
/// are on, and only while the shown Commander is the one the key belongs to. The key is the shown
/// Commander's own secret, or the installation secret until its Commander is shown, when it moves to them.
/// <see cref="Tick"/> is for the tick thread and never waits; <see cref="KeyChanged"/> may be called from
/// any thread. The queue is built the first time the switches are found on.
/// </summary>
public sealed class ColonisationProgress
{
    public static readonly TimeSpan AskAgainAfter = TimeSpan.FromMinutes(1);

    private readonly Func<string, IColonisationPlanWriter> _writerFactory;
    private readonly ColonisationPlans _plans;
    private readonly Func<D47Settings> _settings;
    private readonly Func<string, bool> _hasKey;
    private readonly Func<string, string, bool> _move;
    private readonly ILogger<PlanWriteQueue> _queueLogger;

    private readonly ConcurrentQueue<(int Generation, PlanCommander Answer)> _answers = new();

    private PlanWriteQueue? _queue;
    private volatile bool _keyChanged;
    private bool _on;
    private string? _fid;
    private string? _secretName;
    private int _generation;
    private bool _asking;
    private DateTimeOffset _askAt = DateTimeOffset.MinValue;
    private DateTimeOffset? _askFailedAt;
    private bool _keyRefused;
    private string? _keyCommander;
    private volatile string? _note;

    public ColonisationProgress(
        Func<string, IColonisationPlanWriter> writer,
        ColonisationPlans plans,
        Func<D47Settings> settings,
        Func<string, bool> hasKey,
        Func<string, string, bool> move,
        ILogger<PlanWriteQueue> queueLogger)
    {
        _writerFactory = writer;
        _plans = plans;
        _settings = settings;
        _hasKey = hasKey;
        _move = move;
        _queueLogger = queueLogger;
    }

    /// <summary>Starts the key lookup and every send off the tick thread; must return without waiting.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The switch row's status line, or null when there is nothing to say.</summary>
    public string? Note => _note;

    /// <summary>Raised on the tick thread when <see cref="Note"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Forgets whose key it is, so the next tick asks the planner again.</summary>
    public void KeyChanged() => _keyChanged = true;

    /// <summary>
    /// One tick. <paramref name="fid"/> is the shown Commander's FID; a different one from the last tick
    /// forgets the held answer and writes. <paramref name="arrived"/> is the shown Commander's events from
    /// this poll.
    /// </summary>
    public void Tick(
        bool priming,
        bool offDuty,
        string? shown,
        string? fid,
        IReadOnlyList<JournalEvent> arrived,
        Func<long, string?> systemOf,
        DateTimeOffset now)
    {
        if (_settings().Knowledge is not { ColonisationPlanner: true, ColonisationProgress: true })
        {
            _on = false;
            Publish(null);
            return;
        }

        var changed = !_on || _keyChanged || !string.Equals(fid, _fid, StringComparison.Ordinal);
        _on = true;
        _keyChanged = false;
        _fid = fid;

        var name = fid is null ? null : SecretNameFor(fid);

        if (changed || !string.Equals(name, _secretName, StringComparison.Ordinal))
        {
            _secretName = name;
            Forget();
        }

        if (fid is null)
        {
            Publish(null);
            return;
        }

        if (_secretName is null)
        {
            Publish("No Raven Colonial key is set");
            return;
        }

        Answered(now, shown, fid);

        if (_keyCommander is null && !_keyRefused && !_asking && now >= _askAt)
        {
            Ask();
        }

        if (_keyCommander is { } commander
            && !priming
            && !offDuty
            && string.Equals(shown, commander, StringComparison.OrdinalIgnoreCase)
            && PlanWrites.From(arrived, systemOf) is { Count: > 0 } writes)
        {
            Queue().Add(writes, commander, now);
        }

        _queue?.Poll(now);
        Publish(NoteFor(shown));
    }

    private void Forget()
    {
        _generation++;
        _asking = false;
        _askAt = DateTimeOffset.MinValue;
        _askFailedAt = null;
        _keyRefused = false;
        _keyCommander = null;

        // Held writes carry the old key's Commander name, so they are dropped.
        _queue = null;
    }

    private string? SecretNameFor(string fid)
    {
        var own = CommanderSecret.Name(ColonisationCapability.KeySecretName, fid);

        if (_hasKey(own))
        {
            return own;
        }

        return _hasKey(ColonisationCapability.KeySecretName) ? ColonisationCapability.KeySecretName : null;
    }

    private PlanWriteQueue Queue() =>
        _queue ??= new PlanWriteQueue(_writerFactory(_secretName!), _plans, _queueLogger) { Dispatch = Dispatch };

    private void Ask()
    {
        var writer = _writerFactory(_secretName!);
        var generation = _generation;
        _asking = true;

        Dispatch(async () =>
        {
            PlanCommander answer;

            try
            {
                answer = await writer.CommanderAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                answer = new PlanCommander(PlanWriteOutcome.Failed, null);
            }

            _answers.Enqueue((generation, answer));
        });
    }

    private void Answered(DateTimeOffset now, string? shown, string fid)
    {
        while (_answers.TryDequeue(out var answered))
        {
            if (answered.Generation != _generation)
            {
                continue;
            }

            _asking = false;

            switch (answered.Answer)
            {
                case { Outcome: PlanWriteOutcome.Sent, Name: { Length: > 0 } name }:
                    _keyCommander = name;
                    _askFailedAt = null;
                    MoveInstallationKey(name, shown, fid);
                    break;

                case { Outcome: PlanWriteOutcome.KeyRefused }:
                    _keyRefused = true;
                    break;

                default:
                    _askFailedAt = now;
                    _askAt = now + AskAgainAfter;
                    break;
            }
        }
    }

    private void MoveInstallationKey(string answeredName, string? shown, string fid)
    {
        if (!string.Equals(_secretName, ColonisationCapability.KeySecretName, StringComparison.Ordinal)
            || !string.Equals(answeredName, shown, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var own = CommanderSecret.Name(ColonisationCapability.KeySecretName, fid);

        if (_move(ColonisationCapability.KeySecretName, own))
        {
            _secretName = own;
            _queue = null;
        }
    }

    private string? NoteFor(string? shown)
    {
        if (_keyRefused || _queue?.Status.KeyRefused == true)
        {
            return "Raven Colonial refused the key";
        }

        if (_keyCommander is not { } commander)
        {
            return _askFailedAt is { } failedAt
                ? string.Create(CultureInfo.InvariantCulture, $"Could not ask Raven Colonial whose key this is at {failedAt.UtcDateTime:HH:mm} UTC")
                : null;
        }

        if (shown is not null && !string.Equals(shown, commander, StringComparison.OrdinalIgnoreCase))
        {
            return $"This key belongs to CMDR {commander}, so nothing is sent for CMDR {shown}";
        }

        if (_queue?.Status is { LastFailure: { } failure, LastFailureAt: { } at } status
            && (status.LastSentAt is not { } sent || sent < at))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Last write failed at {at.UtcDateTime:HH:mm} UTC: {failure}");
        }

        return $"Sending as CMDR {commander}";
    }

    private void Publish(string? note)
    {
        if (string.Equals(note, _note, StringComparison.Ordinal))
        {
            return;
        }

        _note = note;
        Changed?.Invoke();
    }
}
