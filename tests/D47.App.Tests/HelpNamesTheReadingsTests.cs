using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The help pages name the Transcript readings the panel actually has
/// (<a href="https://github.com/dseelinger/d47/issues/251">#251</a>).
/// <para>
/// <b>This is the gate that was missing, and its absence is the whole of that bug.</b>
/// <c>DocumentationGateTests</c> asserts every registered <em>capability</em> has a page quoting
/// its current tool schema — but a general help page has no schema to quote, and a
/// <see cref="NavCrumb.Word"/> was written down nowhere a test could compare against prose. So
/// the readings could be renamed, and one of them removed outright, with a green suite and no
/// warning: <c>docs/transcript.md</c> went on describing Thread, Details and D47 Log for two
/// releases after none of them existed, and sent a Commander in a headset to a reading nobody
/// registered.
/// </para>
/// <para>
/// <b>A string search rather than a schema check</b>, deliberately. It cannot ask for prose to be
/// rewritten and it does not try to — it says only that a name on the screen appears on the page
/// that screen's <c>?</c> opens, and that a name which is no longer on any screen appears
/// nowhere. Everything else about how a page reads is a person's judgement.
/// </para>
/// </summary>
public sealed class HelpNamesTheReadingsTests
{
    /// <summary>
    /// Names the Transcript readings used to carry. Each is checked below against the live
    /// registration too, so a name that ever comes back fails here and says to take it off this
    /// list rather than silently passing.
    /// </summary>
    /// <remarks>
    /// Only names distinctive enough to search for. "Thread" and "Details" were readings and are
    /// ordinary words as well — <c>docs/transcript.md</c> legitimately says the banknote "was the
    /// word <em>Details</em> until 0.93.0" — so gating them would fail on prose that is telling
    /// the truth. That is the cost of a string search, taken knowingly: this catches the
    /// misleading half, which is a page sending a Commander somewhere that is not there.
    /// </remarks>
    private static readonly string[] Retired =
    [
        "Technical",
        "D47 Log",
        "Elite Dangerous Journal File",
    ];

    /// <summary>Every reading the Transcript tab registers, on a surface that furnished them all.</summary>
    private static IReadOnlyList<string> Readings()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        // The raw journal is furnished by a host rather than registered for every surface, and it
        // is a reading a Commander can reach — so the page that explains the readings has to
        // explain it, and this test has to know about it.
        panel.EnableRawJournal();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var words = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Word).ToList();

        window.Close();

        return words;
    }

    /// <summary>
    /// Every reading is named on the page its own <c>?</c> opens. This is the self-maintaining
    /// half: rename a reading without touching its help and it fails, naming the word that went
    /// missing.
    /// <para>
    /// <b>Its own page since #262.</b> There was one page for three readings and this asserted
    /// against that one file — so a reading could be named on a page about a different reading and
    /// pass. The map below is the same wiring <c>PanelView</c> registers, written out, because a
    /// test that read the wiring could not catch the two disagreeing.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void EveryReadingIsNamedOnItsOwnHelpPage()
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["In Ship"] = "in-ship.md",
            ["Log File"] = "log-file.md",
            ["Journal File"] = "journal-file.md",

            // Both journal readings share one page, which is the same reason Raw Journal is not
            // an entry in the picker: the same events seen another way, not a fourth subject.
            ["Raw Journal"] = "journal-file.md",
        };

        foreach (var reading in Readings())
        {
            Assert.True(
                pages.TryGetValue(reading, out var file),
                $"""
                 The Transcript reading "{reading}" has no help page in this test's map. A new
                 reading needs one — see #262, which split the single crammed page into one per
                 reading.
                 """);

            var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", file!));

            Assert.True(
                page.Contains(reading, StringComparison.Ordinal),
                $"""
                 The Transcript reading "{reading}" is not named anywhere in docs/{file}, which is
                 the page its ? opens. Rename the reading in the help too, or say why the page does
                 not mention it.
                 """);
        }
    }

    /// <summary>
    /// And each reading's mark opens its own page rather than one shared one (#262). Asserted
    /// through the registration, because the mark is only ever as context-sensitive as what the
    /// crumb was given.
    /// </summary>
    [AvaloniaFact]
    public void EachReadingsMarkOpensItsOwnPage()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        panel.EnableRawJournal();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var help = panel.Nav.Roots(PanelTab.Transcript)
            .ToDictionary(crumb => crumb.Word, crumb => crumb.Help, StringComparer.Ordinal);

        window.Close();

        Assert.Equal(PanelView.InShipHelp, help["In Ship"]);
        Assert.Equal(PanelView.LogFileHelp, help["Log File"]);
        Assert.Equal(PanelView.JournalHelp, help["Journal File"]);
        Assert.Equal(PanelView.JournalHelp, help["Raw Journal"]);

        // Three pages for four readings, and no reading left on somebody else's.
        Assert.Equal(3, help.Values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// And no page names a reading that is gone. The Raw Journal crumb is registered as "Raw
    /// Journal" and drawn as a switch labelled "Raw", so the check is against what is registered
    /// — the same source the panel draws from.
    /// </summary>
    [AvaloniaFact]
    public void NoHelpPageNamesARetiredReading()
    {
        var readings = Readings();

        foreach (var retired in Retired)
        {
            Assert.False(
                readings.Contains(retired, StringComparer.Ordinal),
                $"""
                 "{retired}" is a registered reading again, so it is not retired. Take it off
                 HelpNamesTheReadingsTests.Retired — leaving it there would fail every page that
                 correctly names it.
                 """);
        }

        var offences = new List<string>();

        foreach (var (path, relative) in Pages())
        {
            var text = File.ReadAllText(path);

            offences.AddRange(
                Retired
                    .Where(retired => text.Contains(retired, StringComparison.Ordinal))
                    .Select(retired => $"{relative} names \"{retired}\""));
        }

        Assert.True(
            offences.Count == 0,
            $"""
             A help page names a Transcript reading that no longer exists, so it is telling a
             Commander to go somewhere that is not there:

             {string.Join(Environment.NewLine, offences)}
             """);
    }

    /// <summary>
    /// And nothing d47 <em>says</em> names a retired reading either
    /// (<a href="https://github.com/dseelinger/d47/issues/260">#260</a>).
    /// <para>
    /// <b>The help was not the only place this hid.</b> A response that threw answered
    /// <em>"I couldn't answer that. The details are on the Technical page"</em> — drawn in the
    /// conversation, read by the Commander, and naming a page withdrawn two releases earlier. The
    /// docs gate above could never have seen it, because it is a string literal rather than prose
    /// in <c>docs/</c>.
    /// </para>
    /// <para>
    /// <b>Literals only, never comments.</b> The source discusses these names constantly and
    /// should — every removal above is explained where it happened. What is checked is the text
    /// that reaches a Commander, so a line is scanned only from its first double quote.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingTheAppSaysNamesARetiredReading()
    {
        var offences = new List<string>();

        var sources = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        foreach (var source in sources)
        {
            var line = 0;

            foreach (var text in File.ReadLines(source))
            {
                line++;

                var trimmed = text.TrimStart();

                // A comment line has no literal on it worth reading, and these files are full of
                // comments about exactly these names.
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                var quote = text.IndexOf('"');

                if (quote < 0)
                {
                    continue;
                }

                var literal = text[quote..];

                offences.AddRange(
                    Retired
                        .Where(retired => literal.Contains(retired, StringComparison.Ordinal))
                        .Select(retired =>
                            $"{Path.GetFileName(source)}:{line} says \"{retired}\""));
            }
        }

        Assert.True(
            offences.Count == 0,
            $"""
             Something d47 draws or speaks names a Transcript reading that no longer exists:

             {string.Join(Environment.NewLine, offences)}
             """);
    }

    /// <summary>
    /// The published pages, matching <c>DocumentationGateTests.PublishedPages</c> — the spike
    /// write-ups are contributor material rather than help, and a note in one about a reading
    /// that has since been retired is a record rather than a wrong instruction.
    /// </summary>
    private static IEnumerable<(string Path, string Relative)> Pages()
    {
        var docs = Path.Combine(RepositoryRoot(), "docs");

        return Directory
            .EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
            .Select(path => (path, Path.GetRelativePath(docs, path).Replace('\\', '/')))
            .Where(page => !page.Item2.StartsWith("spikes", StringComparison.OrdinalIgnoreCase)
                           && !page.Item2.StartsWith("_", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
