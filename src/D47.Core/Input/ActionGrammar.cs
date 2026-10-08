namespace D47.Core.Input;

/// <summary>How an action's phrases are built from its names.</summary>
public enum PhraseShape
{
    /// <summary>Done once: each name, and each verb before each name.</summary>
    OneShot,

    /// <summary>On or off, with a state Elite reports: toggled by name, set by "on", "off", a verb or a particle.</summary>
    Switch,

    /// <summary>Two named modes: each mode's name sets it, "switch {name}" toggles between them.</summary>
    Modes,

    /// <summary>A panel-navigation key: the bare word.</summary>
    Key,
}

/// <summary>Builds an action's phrases, and the patterns that list them, from its names and shape.</summary>
public static class ActionGrammar
{
    /// <summary>Every phrase the action answers to; the first for each state is the one an acknowledgement says.</summary>
    public static IReadOnlyList<(string Phrase, DesiredState State)> Phrases(GameAction action)
    {
        if (action.Names.Count == 0)
        {
            return [];
        }

        IEnumerable<(string, DesiredState)> phrases = action.Shape switch
        {
            PhraseShape.Switch => Switch(action),
            PhraseShape.Modes => Modes(action),
            PhraseShape.Key => [(action.Names[0], DesiredState.Toggle)],
            _ => OneShot(action),
        };

        return [.. phrases.DistinctBy(phrase => phrase.Item1, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The phrases written as patterns, square brackets giving the choices: "[gear|landing gear] [on|off]".</summary>
    public static IReadOnlyList<string> Patterns(GameAction action)
    {
        if (action.Names.Count == 0)
        {
            return [];
        }

        var name = Choice(action.Names);

        switch (action.Shape)
        {
            case PhraseShape.Switch:
            {
                List<string> patterns =
                [
                    name,
                    $"toggle {name}",
                    $"{name} [on|off]",
                    $"[turn|switch] [on|off] {name}",
                    $"[turn|switch] {name} [on|off]",
                ];

                if (action.OnVerbs.Count + action.OffVerbs.Count > 0)
                {
                    patterns.Add($"{Choice([.. action.OnVerbs, .. action.OffVerbs])} {name}");
                }

                if (Particles(action) is { Count: > 0 } particles)
                {
                    patterns.Add($"{name} {Choice(particles)}");
                    patterns.Add($"put {name} {Choice(particles)}");
                }

                return patterns;
            }

            case PhraseShape.Modes when action.Modes is ({ } on, { } off):
                return [Choice([on, off]), $"switch to {Choice([on, off])}", $"switch {name}"];

            case PhraseShape.Key:
                return [action.Names[0]];

            default:
                return action.Verbs.Count > 0 ? [name, $"{Choice(action.Verbs)} {name}"] : [name];
        }
    }

    private static IEnumerable<(string, DesiredState)> Switch(GameAction action)
    {
        foreach (var name in action.Names)
        {
            yield return (name, DesiredState.Toggle);
            yield return ($"toggle {name}", DesiredState.Toggle);
        }

        foreach (var phrase in Setting(action, DesiredState.On, "on", action.OnVerbs, action.OnParticle))
        {
            yield return phrase;
        }

        foreach (var phrase in Setting(action, DesiredState.Off, "off", action.OffVerbs, action.OffParticle))
        {
            yield return phrase;
        }
    }

    private static IEnumerable<(string, DesiredState)> Setting(
        GameAction action, DesiredState state, string word, IReadOnlyList<string> verbs, string? particle)
    {
        yield return ($"{action.Names[0]} {particle ?? word}", state);

        foreach (var name in action.Names)
        {
            yield return ($"{name} {word}", state);
            yield return ($"turn {word} {name}", state);
            yield return ($"turn {name} {word}", state);
            yield return ($"switch {word} {name}", state);
            yield return ($"switch {name} {word}", state);

            foreach (var verb in verbs)
            {
                yield return ($"{verb} {name}", state);
            }

            if (particle is not null)
            {
                yield return ($"{name} {particle}", state);
                yield return ($"put {name} {particle}", state);
            }
        }
    }

    private static IEnumerable<(string, DesiredState)> Modes(GameAction action)
    {
        if (action.Modes is not ({ } on, { } off))
        {
            yield break;
        }

        yield return (on, DesiredState.On);
        yield return ($"switch to {on}", DesiredState.On);
        yield return (off, DesiredState.Off);
        yield return ($"switch to {off}", DesiredState.Off);

        foreach (var name in action.Names)
        {
            yield return ($"switch {name}", DesiredState.Toggle);
        }
    }

    private static IEnumerable<(string, DesiredState)> OneShot(GameAction action)
    {
        foreach (var name in action.Names)
        {
            yield return (name, DesiredState.Toggle);
        }

        foreach (var verb in action.Verbs)
        {
            foreach (var name in action.Names)
            {
                yield return ($"{verb} {name}", DesiredState.Toggle);
            }
        }
    }

    private static List<string> Particles(GameAction action) =>
        [.. new[] { action.OnParticle, action.OffParticle }.OfType<string>()];

    private static string Choice(IReadOnlyList<string> words) =>
        words.Count == 1 ? words[0] : $"[{string.Join('|', words)}]";
}
