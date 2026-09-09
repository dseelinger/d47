using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The help pages name the Transcript readings the panel actually has.</summary>
public sealed class HelpNamesTheReadingsTests
{
    /// <summary>Retired Transcript reading names.</summary>
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

        // The raw journal is furnished by a host rather than registered for every surface, and it is a
        // reading a Commander can reach — so the page that explains the readings has to explain it, and this
        // test has to know about it.
        panel.EnableRawJournal();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var words = panel.Nav.Roots(PanelTab.Transcript).Select(crumb => crumb.Word).ToList();

        window.Close();

        return words;
    }

    [AvaloniaFact]
    public void EveryReadingIsNamedOnItsOwnHelpPage()
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["In Ship"] = "in-ship.md",
            ["Log File"] = "log-file.md",
            ["Journal File"] = "journal-file.md",

            // Both journal readings share one page, which is the same reason Raw Journal is not an entry in
            // the picker: the same events seen another way, not a fourth subject.
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

                // A comment line has no literal on it worth reading, and these files are full of comments
                // about exactly these names.
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
    /// The published pages, matching <c>DocumentationGateTests.PublishedPages</c> — the spike write-ups
    /// are contributor material rather than help, and a note in one about a reading that has since been
    /// retired is a record rather than a wrong instruction.
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
