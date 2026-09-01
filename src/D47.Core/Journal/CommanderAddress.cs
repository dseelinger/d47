namespace D47.Core.Journal;

/// <summary>
/// How the Commander is addressed out loud
/// (<a href="https://github.com/dseelinger/d47/issues/247">#247</a>): rank and surname.
/// <para>
/// "Commander DeParagon", never "Commander John DeParagon" — a crew addresses their owner by
/// rank and surname, and rank plus full name is how a form letter talks. The surname is the last
/// whitespace-separated word of the name the journal header states; a single-word name is used
/// as it is, and no name at all is the bare rank, which is still correct and merely less
/// familiar. Casing stays as the journal wrote it, because "DEPARAGON" cannot be re-cased to
/// "DeParagon" without guessing.
/// </para>
/// <para>
/// One helper so the rule lives once: the spoken sites route through here, and the drawn journal
/// records deliberately do not — a reading is a faithful record of what the event said.
/// </para>
/// </summary>
public static class CommanderAddress
{
    public static string Said(string? name)
    {
        if (name is null || name.Trim() is not { Length: > 0 } whole)
        {
            return "Commander";
        }

        var at = whole.LastIndexOf(' ');

        return $"Commander {(at < 0 ? whole : whole[(at + 1)..])}";
    }
}
