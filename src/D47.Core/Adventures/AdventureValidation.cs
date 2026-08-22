using System.Globalization;
using System.Text;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>The career ladders a rank beat may name, and how each is said.</summary>
public static class Careers
{
    /// <summary>The journal's own keys, which are what a <c>Promotion</c> event carries.</summary>
    public static IReadOnlyList<string> Keys => RankState.Careers;

    /// <summary>The journal's key for a spoken or typed career word, or null if it is not one.</summary>
    public static string? Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var wanted = text.Trim();

        return Keys.FirstOrDefault(key =>
            string.Equals(key, wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Word(key), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// How a Commander says the career. Naming the <em>career</em> is not naming the rank — the
    /// ladder's words stay uncounted, which is Phase 34's rule.
    /// </summary>
    public static string Word(string? career) => career switch
    {
        "Explore" => "Exploration",
        "Soldier" => "Mercenary",
        "Exobiologist" => "Exobiology",
        null => "an unknown career",
        _ => career,
    };
}

/// <summary>
/// What the file and the form both check, so a hand-edited adventure and a form-built one are
/// refused identically (list.md Phase 47, "The trigger vocabulary is closed and the prose is
/// free").
/// <para>
/// Two lists, because there are two questions. <see cref="Problems"/> is whether this is an
/// adventure at all — a beat naming an event that does not exist is refused by name with the
/// reason rather than becoming a story that silently can never finish. <see cref="NotReady"/> is
/// whether it can <em>begin</em>: a beat written with a name and no id, offline or from the model,
/// is a real beat that cannot yet fire, and the Commander is told which one rather than handed a
/// grey button.
/// </para>
/// </summary>
public static class AdventureValidation
{
    /// <summary>The five, in the words the file and the chooser use.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["arrive", "dock", "land", "scan", "rank"];

    public static bool TryKind(string? text, out TriggerKind kind)
    {
        kind = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Enum.TryParse(text.Trim(), ignoreCase: true, out kind) && Enum.IsDefined(kind);
    }

    /// <summary>What stops this being stored or begun, each naming where and why. Empty is valid.</summary>
    public static IReadOnlyList<string> Problems(Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(adventure.Key))
        {
            problems.Add("An adventure needs a key.");
        }

        if (string.IsNullOrWhiteSpace(adventure.Name))
        {
            problems.Add("An adventure needs a name.");
        }
        else if (adventure.Name.Trim().Length > AdventureLimits.MaxNameLength)
        {
            problems.Add(
                $"The name is {adventure.Name.Trim().Length} characters; at most {AdventureLimits.MaxNameLength}.");
        }

        if (adventure.Opening is { } opening && opening.Trim().Length > AdventureLimits.MaxLineLength)
        {
            problems.Add($"The opening is {opening.Trim().Length} characters; at most {AdventureLimits.MaxLineLength}.");
        }

        if (adventure.Spine is { } spine)
        {
            foreach (var (field, text) in new[]
                     {
                         ("premise", spine.Premise), ("want", spine.Want), ("stake", spine.Stake),
                         ("turn", spine.Turn), ("ending", spine.Ending),
                     })
            {
                if (text is { } value && value.Trim().Length > AdventureLimits.MaxSpineLength)
                {
                    problems.Add($"The {field} is {value.Trim().Length} characters; at most {AdventureLimits.MaxSpineLength}.");
                }
            }
        }

        if (adventure.Beats.Count == 0)
        {
            problems.Add("An adventure needs at least one beat.");
        }
        else if (adventure.Beats.Count > AdventureLimits.MaxBeats)
        {
            problems.Add($"{adventure.Beats.Count} beats; at most {AdventureLimits.MaxBeats}.");
        }

        for (var index = 0; index < adventure.Beats.Count; index++)
        {
            var beat = adventure.Beats[index];
            var where = Where(index, beat);

            if (string.IsNullOrWhiteSpace(beat.Title))
            {
                problems.Add($"{where} has no title.");
            }
            else if (beat.Title.Trim().Length > AdventureLimits.MaxTitleLength)
            {
                problems.Add($"{where}'s title is {beat.Title.Trim().Length} characters; at most {AdventureLimits.MaxTitleLength}.");
            }

            if (string.IsNullOrWhiteSpace(beat.Line))
            {
                problems.Add($"{where} has no line.");
            }
            else if (beat.Line.Trim().Length > AdventureLimits.MaxLineLength)
            {
                problems.Add($"{where}'s line is {beat.Line.Trim().Length} characters; at most {AdventureLimits.MaxLineLength}.");
            }

            if (!Enum.IsDefined(beat.Trigger.Kind))
            {
                problems.Add($"{where} names a trigger that is not one of {string.Join(", ", Kinds)}.");
                continue;
            }

            switch (beat.Trigger.Kind)
            {
                case TriggerKind.Rank:
                    if (Careers.Match(beat.Trigger.Career) is null)
                    {
                        problems.Add(
                            $"{where} names a career \"{beat.Trigger.Career ?? string.Empty}\"; the careers are "
                            + string.Join(", ", Careers.Keys.Select(Careers.Word)) + ".");
                    }

                    if (beat.Trigger.Rank is not (>= 1 and <= RankStanding.Elite))
                    {
                        problems.Add($"{where} names rank {beat.Trigger.Rank?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}; ranks run 1 to {RankStanding.Elite}.");
                    }

                    break;

                case TriggerKind.Arrive:
                    if (beat.Trigger.SystemAddress is null && string.IsNullOrWhiteSpace(beat.Trigger.System))
                    {
                        problems.Add($"{where} arrives nowhere: it names no system.");
                    }

                    break;

                case TriggerKind.Dock:
                    if (beat.Trigger.MarketId is null && string.IsNullOrWhiteSpace(beat.Trigger.Station))
                    {
                        problems.Add($"{where} docks nowhere: it names no station.");
                    }

                    break;

                case TriggerKind.Land:
                case TriggerKind.Scan:
                    if ((beat.Trigger.SystemAddress is null || beat.Trigger.BodyId is null)
                        && string.IsNullOrWhiteSpace(beat.Trigger.Body))
                    {
                        problems.Add($"{where} names no body.");
                    }

                    break;
            }
        }

        return problems;
    }

    /// <summary>
    /// Why Begin is shut, once <see cref="Problems"/> is empty: every beat whose place has a name
    /// and no id yet. Printed under the button, never a silently grey one.
    /// </summary>
    public static IReadOnlyList<string> NotReady(Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        var reasons = new List<string>();

        for (var index = 0; index < adventure.Beats.Count; index++)
        {
            var beat = adventure.Beats[index];

            if (!beat.Trigger.IsResolved)
            {
                reasons.Add($"{Where(index, beat)} — {beat.Trigger.Describe()} — is not yet a real place d47 can recognise.");
            }
        }

        return reasons;
    }

    /// <summary>A stable key from a name: lower case, dashes, nothing else. The arc's own rule.</summary>
    public static string Key(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var key = new StringBuilder();
        var dash = false;

        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                key.Append(character);
                dash = false;
            }
            else if (!dash && key.Length > 0)
            {
                key.Append('-');
                dash = true;
            }
        }

        return key.ToString().TrimEnd('-');
    }

    private static string Where(int index, AdventureBeat beat) =>
        string.IsNullOrWhiteSpace(beat.Title)
            ? $"Beat {(index + 1).ToString(CultureInfo.InvariantCulture)}"
            : $"Beat {(index + 1).ToString(CultureInfo.InvariantCulture)} ({beat.Title.Trim()})";
}
