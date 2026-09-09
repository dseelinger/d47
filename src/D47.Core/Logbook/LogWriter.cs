using System.Text;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Logbook;

/// <summary>What one request to the provider produced.</summary>
public record LogAttempt
{
    public string Text { get; init; } = string.Empty;

    public LlmUsage Usage { get; init; } = LlmUsage.Unreported;

    /// <summary>Why there is no log, or null.</summary>
    public string? Failure { get; init; }

    /// <summary>The model ran out of output budget.</summary>
    public bool Truncated { get; init; }

    public bool Ok => Failure is null && Text.Trim().Length > 0;
}

/// <summary>Drives the provider for one log (Phase 33).</summary>
public sealed class LogWriter(ILogger<LogWriter> logger)
{
    /// <summary>Two attempts, and three minutes each.</summary>
    public RetryPolicy Retry { get; set; } = RetryPolicy.Default with
    {
        Attempts = 2,
        AttemptTimeout = TimeSpan.FromMinutes(3),
    };

    public async Task<LogAttempt> RunAsync(
        ILlmProvider provider,
        LlmRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        LogAttempt? last = null;

        for (var attempt = 1; attempt <= Math.Max(1, Retry.Attempts); attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(Retry.WaitBefore(attempt), cancellationToken).ConfigureAwait(false);
            }

            var result = await OnceAsync(provider, request, cancellationToken).ConfigureAwait(false);

            if (result.Ok || result.Text.Length > 0)
            {
                return result;
            }

            last = result;

            if (!result.Retryable)
            {
                break;
            }

            logger.LogInformation("Retrying the Commander's log after a transient failure: {Why}", result.Failure);
        }

        return last ?? new LogAttempt { Failure = "Nothing came back." };
    }

    private async Task<Attempted> OnceAsync(
        ILlmProvider provider,
        LlmRequest request,
        CancellationToken cancellationToken)
    {
        var prose = new StringBuilder();
        var usage = LlmUsage.Unreported;
        var stop = LlmStopReason.Completed;
        string? failure = null;
        var retryable = false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Retry.AttemptTimeout);

        try
        {
            await foreach (var streamed in provider.StreamAsync(request, timeout.Token).ConfigureAwait(false))
            {
                switch (streamed)
                {
                    case LlmStreamEvent.TextDelta text:
                        prose.Append(text.Text);
                        break;

                    case LlmStreamEvent.Completed completed:
                        usage = completed.Usage;
                        stop = completed.StopReason;
                        break;

                    case LlmStreamEvent.Failed failed:
                        failure = failed.Message;
                        retryable = failed.Transient;
                        break;

                    default:
                        // Thinking, and a tool call that cannot happen because none were offered.
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            failure = $"The provider took longer than {Retry.AttemptTimeout.TotalMinutes:0.#} minutes.";
            retryable = true;
        }
        catch (OperationCanceledException)
        {
            return new Attempted { Failure = "Cancelled.", Retryable = false };
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(ex, "The Commander's log request failed");
            failure = ex.Message;
            retryable = true;
        }

        if (failure is null && stop is LlmStopReason.Refusal)
        {
            failure = "The model declined to write it.";
        }

        if (failure is null && prose.Length == 0)
        {
            failure = "The model returned nothing.";
        }

        return new Attempted
        {
            Text = prose.ToString(),
            Usage = usage,
            Failure = failure,
            Truncated = stop is LlmStopReason.MaxTokens,
            Retryable = retryable,
        };
    }

    /// <summary>One attempt, with whether trying again is worth anything.</summary>
    private sealed record Attempted : LogAttempt
    {
        public bool Retryable { get; init; }
    }
}
