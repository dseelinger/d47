using System.Globalization;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Logbook;

/// <summary>
/// What the App supplies at the moment a log is written — the things Core cannot know and must not
/// hold.
/// </summary>
public sealed record LogbookContext
{
    public ILlmProvider? Provider { get; init; }

    public string? Model { get; init; }

    /// <summary>Whether there is a personality at all.</summary>
    public bool PersonalityEnabled { get; init; } = true;

    /// <summary>The rendered persona block, or null.</summary>
    public string? Persona { get; init; }

    public string? AboutMe { get; init; }

    /// <summary>Where the charge goes afterwards.</summary>
    public SpendLedger? Ledger { get; init; }

    public string Version { get; init; } = string.Empty;

    public PriceTable Prices { get; init; } = PriceTable.Default;
}

/// <summary>What one attempt at a whole log came to, in the words the caller says out loud.</summary>
public sealed record LogOutcome(bool Ok, string Message, LogWritten? Written = null);

/// <summary>The Commander's log, end to end (Phase 33).</summary>
public sealed class LogbookBook(
    LogFolder folder,
    LogDigestBuilder digests,
    LogWriter writer,
    Func<LogbookSettings> settings,
    Func<IReadOnlyList<string>> journals,
    Func<DateTimeOffset> now,
    Func<LogbookContext> context,
    ILogger<LogbookBook> logger)
{
    private readonly Lock _gate = new();

    private LogEstimate? _armed;

    public LogFolder Folder => folder;

    /// <summary>The quote a write would spend, or null where nothing has been quoted.</summary>
    public LogEstimate? Armed
    {
        get
        {
            lock (_gate)
            {
                return _armed;
            }
        }
    }

    /// <summary>Raised when the arming or the folder changed.</summary>
    public event Action? Changed;

    /// <summary>Reads the window, prices it, and arms the writer.</summary>
    private static string? NotReady(LogbookContext host) =>
        host.Provider is null
            ? "There is no provider selected, so I cannot write anything. Choose one in Settings."
            : string.IsNullOrWhiteSpace(host.Model)
                ? "There is no model selected, so I cannot write anything. Your provider is set — "
                  + "it is the model beneath it that is not."
                : null;

    public LogEstimate? Estimate(string? spanId, DateTimeOffset? from, DateTimeOffset? to, out string message)
    {
        var current = settings();
        var files = journals();

        var range = from is { } start && to is { } end
            ? LogRanges.Between(start, end)
            : LogRanges.Resolve(LogRanges.Parse(spanId ?? current.Range), now(), files, logger);

        var digest = digests.Build(files, range);
        var host = context();

        if (NotReady(host) is { } why)
        {
            Disarm();
            message = why;
            return null;
        }

        var provider = host.Provider!;

        var asked = LogVoices.Parse(current.Voice);
        var used = LogVoices.Resolve(asked, host.PersonalityEnabled);
        var length = LogLengths.Parse(current.Length);

        var prompt = LogPrompt.Build(digest, used, length, host.Persona, host.AboutMe);
        var local = provider.RunsOnThisMachine;

        var estimate = LogEstimate.For(
            digest,
            prompt,
            asked,
            used,
            length,
            provider.Id,
            host.Model!,
            local ? PriceTable.Free : host.Prices.For(provider.Id, host.Model!),
            local);

        lock (_gate)
        {
            // A window with nothing in it never arms.
            _armed = digest.Any ? estimate : null;
        }

        Changed?.Invoke();
        message = estimate.Describe();
        return estimate;
    }

    public void Disarm()
    {
        lock (_gate)
        {
            _armed = null;
        }

        Changed?.Invoke();
    }

    /// <summary>Writes the armed log.</summary>
    public async Task<LogOutcome> WriteAsync(CancellationToken cancellationToken)
    {
        LogEstimate? estimate;

        lock (_gate)
        {
            estimate = _armed;
            _armed = null;
        }

        Changed?.Invoke();

        if (estimate is null)
        {
            return new LogOutcome(
                false,
                "I have not worked out what that would cost yet. Ask me for a log first and I will tell you, "
                + "and then you can tell me to write it.");
        }

        var host = context();

        if (NotReady(host) is { } why)
        {
            return new LogOutcome(false, why);
        }

        var provider = host.Provider!;

        if (!string.Equals(provider.Id, estimate.ProviderId, StringComparison.Ordinal) ||
            !string.Equals(host.Model, estimate.Model, StringComparison.Ordinal))
        {
            return new LogOutcome(
                false,
                $"The model changed after I quoted you — that was for {estimate.Model} and it is now "
                + $"{host.Model}. Ask me again and I will re-price it.");
        }

        var prompt = LogPrompt.Build(
            estimate.Digest,
            estimate.Used,
            estimate.Length,
            host.Persona,
            host.AboutMe);

        var attempt = await writer
            .RunAsync(provider, LogPrompt.Request(estimate.Model, prompt, estimate.Length), cancellationToken)
            .ConfigureAwait(false);

        if (!attempt.Ok)
        {
            return new LogOutcome(false, $"I could not write it. {attempt.Failure}");
        }

        var cost = Price(attempt.Usage, provider, estimate, host);
        Charge(cost, provider.Id, estimate.Model, host);

        var audit = LogAudit.Check(attempt.Text, estimate.Digest);

        var written = new LogWritten
        {
            Digest = estimate.Digest,
            Audit = audit,
            Asked = estimate.Asked,
            Used = estimate.Used,
            Length = estimate.Length,
            ProviderId = provider.Id,
            Model = estimate.Model,
            WrittenAt = now(),
            Cost = cost,
            Local = estimate.Local,
            Version = host.Version,
        };

        string path;

        try
        {
            path = folder.Write(LogRenderer.Render(written), estimate.Digest.Range, written.WrittenAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the Commander's log");

            // The money is already spent, so the prose is not lost quietly: it goes back to the caller, which
            // is the panel and the transcript.
            return new LogOutcome(false, $"I wrote it and could not save it — {ex.Message}", written);
        }

        Changed?.Invoke();

        return new LogOutcome(true, Report(written with { Path = path }, attempt), written with { Path = path });
    }

    /// <summary>What the panel row and the readback tool say about the folder.</summary>
    public string Describe()
    {
        var text = folder.Describe();

        return Armed is { } estimate
            ? text + Environment.NewLine + Environment.NewLine + "Quoted and ready: " + estimate.Price + "."
            : text;
    }

    private static TurnCost Price(LlmUsage usage, ILlmProvider provider, LogEstimate estimate, LogbookContext host)
    {
        if (!usage.Reported)
        {
            return TurnCost.Unpriced(usage);
        }

        var price = estimate.Local ? PriceTable.Free : host.Prices.For(provider.Id, estimate.Model);

        return price is null ? TurnCost.Unpriced(usage) : new TurnCost(usage, price.DollarsFor(usage), Priced: true);
    }

    /// <summary>The charge, into <see cref="SpendLedger"/> and nowhere else.</summary>
    private static void Charge(TurnCost cost, string providerId, string model, LogbookContext host) =>
        host.Ledger?.Append(new SpendEntry
        {
            Kind = SpendKind.Model,
            ProviderId = providerId,
            Model = model,
            Dollars = cost.Dollars,
            Priced = cost.Priced,
            InputTokens = cost.Usage.InputTokens,
            CacheWriteTokens = cost.Usage.CacheCreationInputTokens,
            CacheReadTokens = cost.Usage.CacheReadInputTokens,
            OutputTokens = cost.Usage.OutputTokens,
        });

    private static string Report(LogWritten written, LogAttempt attempt)
    {
        var text = new System.Text.StringBuilder();

        text.Append("Written to ").Append(System.IO.Path.GetFileName(written.Path)).Append(". ")
            .Append(written.Audit.Summary());

        if (!written.Audit.Clean)
        {
            text.Append(" The ones that do not are marked in the file.");
        }

        if (attempt.Truncated)
        {
            text.Append(" It ran out of room and stops mid-thought — a longer setting would finish it.");
        }

        text.Append(written.Local
            ? " It cost nothing; that endpoint is on this machine."
            : written.Cost is { Priced: true } cost
                ? $" It cost {cost.Dollars.ToString("C2", CultureInfo.GetCultureInfo("en-US"))}."
                : " I cannot say what it cost — that model has no published price.");

        return text.ToString();
    }
}
