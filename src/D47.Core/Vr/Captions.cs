namespace D47.Core.Vr;

/// <summary>
/// How big the caption text is drawn. Three sizes rather than a number, because this is a
/// caption standard's own vocabulary and because a caption is either legible at a glance or it
/// is not — there is nothing useful between two adjacent values.
/// </summary>
public enum CaptionSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// What <em>Configure the captions</em> configures (Phase 9). Bounded by the standard:
/// everything here is something the CC specification leaves to the viewer, and nothing here is
/// something it fixes.
/// <para>
/// <em>Placement</em> is still not on this list, and <see cref="Lock"/> is not placement — see
/// there. Characters per line is not on it either: 42 is the standard's number, not a
/// preference.
/// </para>
/// </summary>
public sealed record CaptionSettings
{
    public bool Enabled { get; init; } = true;

    public CaptionSize Size { get; init; } = CaptionSize.Medium;

    /// <summary>
    /// Whether the band rides the view or sits in the cockpit
    /// (<a href="https://github.com/dseelinger/d47/issues/204">#204</a>).
    /// <para>
    /// <b>This is a choice between two computed positions, not a placement.</b> The rule it
    /// narrows — <em>a caption you can drag somewhere you will not see it is not a caption</em> —
    /// is about free placement, and neither of these is free: head-locked is the band the layer
    /// has always drawn, and world-locked is a fixed area low in the cockpit worked out from the
    /// seated origin. Captions still gain no distance, no curve and no grab-to-move, and
    /// <em>Overlay Positioning &amp; Look</em> still cannot reach them.
    /// </para>
    /// <para>
    /// <b>Head-locked is the default, and world-locked is the comfortable one.</b> A band bolted
    /// to the view is always legible and is also a standing visual–vestibular disagreement: the
    /// cockpit sweeps past when the Commander turns and the caption does not, which is the
    /// mismatch that makes people ill. World-locked costs having to look down for it. That trade
    /// is the Commander's, so it is a row rather than a ruling.
    /// </para>
    /// <para>
    /// <b>A string, spelled the way the panel's lock is spelled</b>
    /// (<see cref="Configuration.VrSurfaceSettings.Lock"/>), and read through
    /// <see cref="Locking"/>. A <see cref="SurfaceLock"/> here would be written to
    /// <c>settings.json</c> as <c>headLocked</c> beside the panel's <c>head</c>, and — the part
    /// that matters — a file hand-edited to the word the row, the docs and the panel all use
    /// would <em>throw</em> on load rather than being read.
    /// </para>
    /// </summary>
    public string Lock { get; init; } = "head";

    /// <summary>
    /// <see cref="Lock"/> read as the lock it names. Parsed at the use site rather than on the
    /// way in, exactly as <see cref="Configuration.VrSurfaceSettings.ToPlacement"/> does it, so a
    /// word nobody recognises reads as head-locked — the band that is always in view — instead of
    /// as a settings file that will not load.
    /// <para>
    /// <b>Not written.</b> A derived property with no setter is still serialised, and a
    /// <c>settings.json</c> that gained a <c>locking</c> beside its <c>lock</c> would carry a key
    /// nothing ever reads — permanently, since the file is append-only.
    /// </para>
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SurfaceLock Locking => string.Equals(Lock, "world", StringComparison.OrdinalIgnoreCase)
        ? SurfaceLock.WorldLocked
        : SurfaceLock.HeadLocked;

    /// <summary>
    /// How opaque the box behind the text is. Not fully: a caption sits over a starfield, a
    /// station's floodlights and the cockpit's own instruments, and text with nothing behind it
    /// is unreadable against half of those — which is why broadcast captioning has always put
    /// it on a box. Enough to read against anything, little enough to see through.
    /// <para>
    /// Floored at <see cref="Caption.MinimumBackgroundOpacity"/>, which is a contrast floor
    /// rather than a taste one — see there.
    /// </para>
    /// </summary>
    public double BackgroundOpacity { get; init; } = 0.78;

    /// <summary>
    /// Reading speed in characters per second, which is what decides how long a caption stays
    /// up after the speech ends. The standard's adult rate is 20 and its children's rate is 17;
    /// slower is offered because reading speed is the one thing about a caption that is a
    /// property of the reader.
    /// </summary>
    public double CharactersPerSecond { get; init; } = Caption.AdultReadingSpeed;

    public CaptionSettings Sane() => this with
    {
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, Caption.MinimumBackgroundOpacity, 1.0),
        CharactersPerSecond = Math.Clamp(CharactersPerSecond, 8, 30),
    };
}

/// <summary>
/// The caption standard's numbers, in one place, so that "follows the CC standard" is a thing
/// the code says rather than a thing the documentation claims.
/// </summary>
public static class Caption
{
    /// <summary>Maximum characters on one line.</summary>
    public const int CharactersPerLine = 42;

    /// <summary>Maximum lines one caption event may occupy.</summary>
    public const int LinesPerEvent = 2;

    /// <summary>
    /// How many lines the layer holds at once.
    /// <para>
    /// Two, matching <see cref="LinesPerEvent"/> rather than exceeding it. It was three — the
    /// roll-up form live captioning uses, and what the checklist originally asked for — and three
    /// lines across the middle of a cockpit was reported as too much of the view
    /// (remediation.md 9, "2 lines only for captions"). The broadcast and streaming specs both
    /// cap one caption at two lines; holding two is the screen agreeing with the event.
    /// </para>
    /// </summary>
    public const int WindowLines = 2;

    /// <summary>
    /// How see-through the box behind the text may be made
    /// (<a href="https://github.com/dseelinger/d47/issues/201">#201</a>).
    /// <para>
    /// <b>0.6 is a contrast floor and not a preference.</b> The clamp used to be 0.2, which is
    /// the value it was added to prevent: against a bright scene — a station floodlight, an
    /// ice ring, a white hangar wall — a box that see-through leaves an effective backdrop near
    /// rgb(204,204,204), and <c>#F2F2F2</c> text on that is about <b>1.4:1</b>. WCAG's floor for
    /// normal text is 4.5:1. That is not a dim caption, it is an invisible one, and a Commander
    /// who reached it by dragging a slider would have no way to tell it from the captions having
    /// stopped.
    /// </para>
    /// <para>
    /// Measured against white, which is the worst case rather than the usual one: 0.5 gives about
    /// 3.5:1 and is still short, 0.6 gives about <b>5.1:1</b> and clears AA. The default stays
    /// 0.78 — around 9.7:1 — so this only binds on a Commander who deliberately turned it down,
    /// and it stops them turning it down past the point where there is nothing to read.
    /// </para>
    /// </summary>
    public const double MinimumBackgroundOpacity = 0.6;

    public const double AdultReadingSpeed = 20.0;

    public const double ChildrensReadingSpeed = 17.0;

    /// <summary>Nothing stays up for less than five sixths of a second.</summary>
    public static readonly TimeSpan MinimumDwell = TimeSpan.FromSeconds(5.0 / 6.0);

    /// <summary>And nothing for more than seven.</summary>
    public static readonly TimeSpan MaximumDwell = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Words that a line should break <em>before</em> rather than after, so a line ending does
    /// not separate a phrase from the thing it attaches to.
    /// </summary>
    private static readonly HashSet<string> BreakBefore = new(StringComparer.OrdinalIgnoreCase)
    {
        // Conjunctions.
        "and", "but", "or", "nor", "so", "yet", "because", "although", "though", "while",
        "unless", "until", "whereas", "if", "when",

        // Prepositions.
        "at", "by", "for", "from", "in", "into", "of", "on", "onto", "over", "to", "toward",
        "towards", "under", "with", "within", "without", "after", "before", "through",
    };

    /// <summary>
    /// How long a caption stays up once the speech has stopped. Characters over reading speed,
    /// floored and ceilinged by the standard.
    /// <para>
    /// Timed from the end of speech rather than from its start, which the checklist is explicit
    /// about and which is the whole difference between a caption and a subtitle track: nobody
    /// is reading along with a voice they can hear, they are catching the last line after it
    /// has gone.
    /// </para>
    /// </summary>
    public static TimeSpan DwellFor(string text, double charactersPerSecond)
    {
        if (charactersPerSecond <= 0)
        {
            return MaximumDwell;
        }

        var seconds = text.Length / charactersPerSecond;

        return TimeSpan.FromSeconds(Math.Clamp(
            seconds,
            MinimumDwell.TotalSeconds,
            MaximumDwell.TotalSeconds));
    }

    /// <summary>
    /// Wraps one utterance to the standard's line length.
    /// <para>
    /// Text stays on one line unless it exceeds the limit. When it has to break, the break goes
    /// after punctuation or before a conjunction or preposition, and the result is
    /// bottom-heavy — a longer last line rather than a longer first one — because two words
    /// alone on top reads as a mistake.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, int charactersPerLine = CharactersPerLine)
    {
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (collapsed.Length == 0)
        {
            return [];
        }

        if (collapsed.Length <= charactersPerLine)
        {
            return [collapsed];
        }

        var words = collapsed.Split(' ');
        var lines = new List<string>();
        var line = new List<string>();

        foreach (var word in words)
        {
            var wouldBe = line.Count == 0 ? word.Length : Length(line) + 1 + word.Length;

            if (wouldBe > charactersPerLine && line.Count > 0)
            {
                lines.Add(string.Join(' ', line));
                line.Clear();
            }

            line.Add(word);
        }

        if (line.Count > 0)
        {
            lines.Add(string.Join(' ', line));
        }

        return Balance(lines, charactersPerLine);
    }

    private static int Length(List<string> words) => words.Sum(word => word.Length) + words.Count - 1;

    /// <summary>
    /// Moves the break to a better word and leaves the last line the longer one.
    /// <para>
    /// Only applied to a two-line result, because that is the shape the rule is about: a longer
    /// utterance is already several lines and moving one word between the middle two buys
    /// nothing a reader would notice.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> Balance(List<string> lines, int charactersPerLine)
    {
        if (lines.Count != 2)
        {
            return lines;
        }

        var words = $"{lines[0]} {lines[1]}".Split(' ');
        var best = lines;
        var bestScore = int.MinValue;

        for (var split = 1; split < words.Length; split++)
        {
            var top = string.Join(' ', words[..split]);
            var bottom = string.Join(' ', words[split..]);

            if (top.Length > charactersPerLine || bottom.Length > charactersPerLine)
            {
                continue;
            }

            var score = 0;

            // After punctuation is the best place to break.
            if (top.Length > 0 && ".,;:!?".Contains(top[^1], StringComparison.Ordinal))
            {
                score += 6;
            }

            // Before a conjunction or a preposition is the next best.
            if (BreakBefore.Contains(words[split]))
            {
                score += 4;
            }

            // Bottom-heavy, and never two words alone on top.
            if (bottom.Length >= top.Length)
            {
                score += 2;
            }

            if (split <= 2)
            {
                score -= 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = [top, bottom];
            }
        }

        return best;
    }
}
