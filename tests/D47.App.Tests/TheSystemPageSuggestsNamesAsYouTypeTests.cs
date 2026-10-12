using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// SEARCH › SYSTEM lists the systems that start with what is typed, and picking one opens it (#1087). The pause
/// before asking is set to zero, so nothing here waits for it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSystemPageSuggestsNamesAsYouTypeTests
{
    private const long Ltt7786 = 633608311522;
    private const long Diaguandri = 670417429889;
    private const long Dian = 7268560741769;

    private static readonly string[] DiaNames =
    [
        "Diaguandri", "Dian", "Diaguthivar", "Diabozo", "Diabingo", "Diabanda",
        "Diadem", "Diaguan", "Diaba", "Diabak", "Diakas", "Dian Cecht",
    ];

    private sealed class Systems : IStarSystemService
    {
        private readonly List<long> _asked = [];
        private readonly List<string> _typed = [];
        private readonly List<string> _prefixes = [];

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            lock (_asked)
            {
                _asked.Add(systemAddress);
            }

            return Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });
        }

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken)
        {
            lock (_typed)
            {
                _typed.Add(typed);
            }

            return Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);
        }

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken)
        {
            lock (_prefixes)
            {
                _prefixes.Add(typed);
            }

            return Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                [.. DiaNames
                    .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                    .Select(name => new SystemNameMatch(
                        name,
                        name switch { "Diaguandri" => Diaguandri, "Dian" => Dian, _ => name.Length },
                        new StarPosition(0, 0, 0)))]);
        }

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));

        public long[] Asked()
        {
            lock (_asked)
            {
                return [.. _asked];
            }
        }

        public string[] Typed()
        {
            lock (_typed)
            {
                return [.. _typed];
            }
        }

        public string[] Prefixes()
        {
            lock (_prefixes)
            {
                return [.. _prefixes];
            }
        }
    }

    private sealed record Shown(PanelView Panel, Window Window, Systems Systems)
    {
        public TextBox Field => Panel.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "System name");

        public Border List => Panel.GetVisualDescendants().OfType<Border>().Single(border => border.Name == NameSuggestions.ListName);

        public string[] Listed =>
            List.IsEffectivelyVisible
                ? [.. List.GetVisualDescendants().OfType<Button>().Select(button => AutomationProperties.GetName(button) ?? string.Empty)]
                : [];

        public StarSystemPage Page => Panel.GetVisualDescendants().OfType<StarSystemPage>().Single();
    }

    private static Shown Open()
    {
        var systems = new Systems();
        JournalLocation here = new("LTT 7786", null, false, null) { SystemAddress = Ltt7786 };
        var surface = new StarSystemSurface(systems, () => here, () => true) { SuggestionPause = TimeSpan.Zero };

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();
        Until(() => systems.Asked().Length == 1);

        return new Shown(panel, window, systems);
    }

    /// <summary>Pumps the UI thread until <paramref name="done"/> holds, for work that comes back from the pool.</summary>
    private static void Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "the page did not settle in time");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Shown shown, Key key)
    {
        shown.Field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Typed(Shown shown, string text)
    {
        shown.Field.Text = text;
        Until(() => shown.Listed.Length > 0);
    }

    [AvaloniaFact]
    public void ThreeLettersListUpToEightSystemsThatStartWithThem()
    {
        var shown = Open();

        Typed(shown, "Dia");

        Assert.Equal(["Dia"], shown.Systems.Prefixes());
        Assert.Equal(NameSuggestions.Most, shown.Listed.Length);
        Assert.Equal("Diaguandri", shown.Listed[0]);
        Assert.Contains("Dian", shown.Listed);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void TheListIsDrawnUnderTheFieldWithTheHighlightedName()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);
        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var shown = Open();
        Typed(shown, "Dia");
        Press(shown, Key.Down);
        Press(shown, Key.Down);

        using var frame = shown.Window.CaptureRenderedFrame()!;
        frame.SaveCapture("system-name-suggestions.png");

        Assert.Equal(1, shown.List.GetVisualDescendants().OfType<Button>().Count(button => button.Classes.Contains(ListRow.SelectedClass)));

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void TwoLettersAskNothing()
    {
        var shown = Open();

        shown.Field.Text = "Di";
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(shown.Systems.Prefixes());
        Assert.Empty(shown.Listed);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void DownAndEnterOpenTheHighlightedName()
    {
        var shown = Open();
        Typed(shown, "Dia");

        Press(shown, Key.Down);
        Press(shown, Key.Down);
        Press(shown, Key.Up);
        Press(shown, Key.Down);
        Press(shown, Key.Enter);
        Until(() => shown.Systems.Asked().Length == 2);

        Assert.Equal([Ltt7786, Dian], shown.Systems.Asked());
        Assert.Equal("Dian", shown.Field.Text);
        Assert.Empty(shown.Listed);
        Assert.Empty(shown.Systems.Typed());
        Assert.Equal(["Dia"], shown.Systems.Prefixes());
        Assert.False(shown.Page.Following);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ClickingANameOpensIt()
    {
        var shown = Open();
        Typed(shown, "Dia");

        var first = shown.List.GetVisualDescendants().OfType<Button>().First();
        first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(() => shown.Systems.Asked().Length == 2);

        Assert.Equal([Ltt7786, Diaguandri], shown.Systems.Asked());
        Assert.Equal("Diaguandri", shown.Field.Text);
        Assert.Empty(shown.Listed);
        Assert.Equal(["Dia"], shown.Systems.Prefixes());

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void EscapeClosesTheList()
    {
        var shown = Open();
        Typed(shown, "Dia");

        Press(shown, Key.Escape);

        Assert.Empty(shown.Listed);
        Assert.Equal("Dia", shown.Field.Text);
        Assert.Equal([Ltt7786], shown.Systems.Asked());

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void EnterWithNothingHighlightedLooksTheNameUpAsBefore()
    {
        var shown = Open();
        Typed(shown, "Dia");

        Press(shown, Key.Enter);
        Until(() => shown.Systems.Typed().Length == 1);

        Assert.Equal(["Dia"], shown.Systems.Typed());
        Assert.Empty(shown.Listed);

        shown.Window.Close();
    }
}
