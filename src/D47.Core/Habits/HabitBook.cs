using System.Text;

namespace D47.Core.Habits;

/// <summary>
/// Everything d47 has noticed about the Commander, read as one thing (Phase 32).
/// <para>
/// The store knows about a file; this knows who is flying, which claims they have refused, and
/// which one was said last. Same arrangement as <see cref="Memory.MemoryBook"/> over
/// <see cref="Memory.MemoryStore"/>, and for the same reason.
/// </para>
/// <para>
/// <b>The last claim spoken lives here.</b> It is what makes the two phrases that matter
/// argument-free — <em>why did you say that</em> and <em>stop telling me that</em> both mean "the
/// one you just said" — and the keyword router's grammar is closed, so an argument-free phrase is
/// the only kind it can carry. Held in memory rather than on disk deliberately: it is about this
/// session, and a claim d47 mentioned last March is not what "that" means.
/// </para>
/// </summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said. Read per call rather
/// than captured, because a Commander can log into a second character without restarting d47.
/// </param>
public sealed class HabitBook(HabitStore store, Func<string?> commander)
{
    private readonly Lock _gate = new();

    private HabitClaim? _lastSpoken;

    public HabitStore Store => store;

    /// <summary>Who the book is pointed at right now, or null before the journal has said.</summary>
    public string? CommanderId => commander();

    /// <summary>The last mining run for whoever is aboard, or null where none has been done.</summary>
    public HabitReport? Mine => store.For(commander());

    /// <summary>
    /// The claims that may be spoken: everything mined, less everything refused. <b>The one place
    /// dismissal is applied</b>, so the callout and the readback cannot disagree about what has
    /// been refused.
    /// </summary>
    public IReadOnlyList<HabitClaim> Claims
    {
        get
        {
            var dismissed = store.DismissedBy(commander());

            return
            [
                .. (Mine?.Claims ?? [])
                    .Where(claim => !dismissed.Contains(claim.Key, StringComparer.Ordinal)),
            ];
        }
    }

    /// <summary>The claim most recently said out loud, or null if none has been this session.</summary>
    public HabitClaim? LastSpoken
    {
        get
        {
            lock (_gate)
            {
                return _lastSpoken;
            }
        }
    }

    /// <summary>Records that a claim was said. Called by the callout, which is the only thing that says one.</summary>
    public void Spoke(HabitClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        lock (_gate)
        {
            _lastSpoken = claim;
        }
    }

    /// <summary>
    /// Refuses a claim by key, or — with no key — the one just said. Returns what was refused, or
    /// null where there was nothing to refuse.
    /// </summary>
    public HabitClaim? Dismiss(string? key)
    {
        var claim = string.IsNullOrWhiteSpace(key)
            ? LastSpoken
            : Claims.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

        if (claim is null)
        {
            return null;
        }

        store.Dismiss(commander(), claim.Key);

        lock (_gate)
        {
            if (string.Equals(_lastSpoken?.Key, claim.Key, StringComparison.Ordinal))
            {
                _lastSpoken = null;
            }
        }

        return claim;
    }

    /// <summary>
    /// The whole report in words — the claims, then the detectors that declined, then the
    /// dismissals. What <c>get_habits</c> answers and what the panel's row summarises.
    /// <para>
    /// <b>All three, always.</b> Reporting only the claims would let a run that found nothing read
    /// as a Commander with no habits, when the truth may be that there is not enough of them to say
    /// — the distinction item 2 spends its length on.
    /// </para>
    /// </summary>
    public string Describe()
    {
        var report = Mine;

        if (report is null)
        {
            return "I have not read through your journals yet. Ask me to, and it takes a few seconds.";
        }

        if (report.Refusal is { Length: > 0 } refusal)
        {
            return refusal;
        }

        var dismissed = store.DismissedBy(commander());
        var text = new StringBuilder();

        text.Append("I read ")
            .Append(report.Journals)
            .Append(report.Journals == 1 ? " journal" : " journals");

        if (report.From is { } from && report.To is { } to)
        {
            text.Append($", {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
        }

        text.AppendLine(".").AppendLine();

        var speakable = Claims;

        if (speakable.Count == 0)
        {
            text.AppendLine("Nothing came out of it that I would say out loud.");
        }
        else
        {
            text.AppendLine("What I noticed:");

            foreach (var claim in speakable)
            {
                text.Append("- ").AppendLine(claim.Explained());
            }
        }

        if (report.Quiet.Count > 0)
        {
            text.AppendLine().AppendLine("What I looked at and would not claim:");

            foreach (var silence in report.Quiet)
            {
                text.Append("- ").Append(silence.Subject).Append(" — ").AppendLine(silence.Why);
            }
        }

        if (dismissed.Count > 0)
        {
            text.AppendLine()
                .Append("You have told me to drop ")
                .Append(dismissed.Count)
                .AppendLine(dismissed.Count == 1 ? " of these, and I have." : " of them, and I have.");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>The one line the panel row shows. Deliberately a count rather than a claim.</summary>
    public string Summarise()
    {
        var report = Mine;

        if (report is null)
        {
            return "Nothing read yet. D47 has not looked through your journals.";
        }

        if (report.Refusal is { Length: > 0 } refusal)
        {
            return refusal;
        }

        var claims = Claims.Count;
        var quiet = report.Quiet.Count;

        return $"{claims} {(claims == 1 ? "habit" : "habits")} from {report.Journals} journals, "
            + $"{quiet} {(quiet == 1 ? "pattern" : "patterns")} looked at and not claimed, "
            + $"read {report.MinedAt:yyyy-MM-dd}.";
    }
}
