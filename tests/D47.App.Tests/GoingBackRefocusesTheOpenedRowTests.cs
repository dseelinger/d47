using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Back puts keyboard focus on the row that opened the level just left (#1517).</summary>
[Trait("Category", "Integration")]
public class GoingBackRefocusesTheOpenedRowTests
{
    private sealed class RowsPage(Func<int> count, Action<NavCrumb> open, bool rebuild) : UserControl
    {
        private readonly StackPanel _column = new();

        public RowsPage Fill()
        {
            Content = _column;
            Build();
            return this;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (rebuild)
            {
                Build();
            }
        }

        public IReadOnlyList<Control> Rows => [.. _column.Children.OfType<Control>()];

        private void Build()
        {
            _column.Children.Clear();

            for (var i = 0; i < count(); i++)
            {
                var n = i;
                _column.Children.Add(ListRow.Opens(ListRow.Dress(new Border { Height = 40, Background = Avalonia.Media.Brushes.Transparent }), () => open(new NavCrumb($"child{n}", "Child"))));
            }
        }
    }

    private static (SettingsHost Host, Func<RowsPage> Root, Func<int> Count, Action<int> SetCount) Open(bool rebuild)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 500, height: 800);
        var count = 4;
        RowsPage? root = null;

        host.Panel.Furnish(
            PanelTab.Navigation,
            crumb =>
            {
                if (crumb.Key == "root")
                {
                    return root = new RowsPage(() => count, c => host.Panel.Nav.Drill(c), rebuild).Fill();
                }

                return new TextBlock { Text = crumb.Key };
            },
            new NavCrumb("root", "Root"));

        host.Panel.Tab = PanelTab.Navigation;
        Dispatcher.UIThread.RunJobs();

        return (host, () => root!, () => count, n => count = n);
    }

    private static void Press(SettingsHost host, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        host.Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Back(SettingsHost host) => Press(host, Key.Left, RawInputModifiers.Alt);

    private static Control? Focused(SettingsHost host) => host.Window.FocusManager?.GetFocusedElement() as Control;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackFocusesTheSecondRowAfterOpeningItFromTheKeyboard(bool rebuild)
    {
        var (host, root, _, _) = Open(rebuild);

        Press(host, Key.Down);
        Press(host, Key.Down);
        Press(host, Key.Enter);
        Assert.Equal(2, host.Panel.Nav.Trail.Count);

        Back(host);

        Assert.Single(host.Panel.Nav.Trail);
        Assert.Same(root().Rows[1], Focused(host));

        host.Close();
    }

    [AvaloniaFact]
    public void BackFocusesTheNewLastRowWhenTheListLostOneAndTheLastWasOpened()
    {
        var (host, root, _, setCount) = Open(rebuild: true);

        Press(host, Key.Up);
        Press(host, Key.Enter);

        setCount(3);
        Back(host);

        Assert.Equal(3, root().Rows.Count);
        Assert.Same(root().Rows[^1], Focused(host));

        host.Close();
    }

    [AvaloniaFact]
    public void BackAfterOpeningWithNoRowFocusedMovesNoFocus()
    {
        var (host, root, _, _) = Open(rebuild: true);

        Assert.True(host.Panel.Nav.Drill(new NavCrumb("child0", "Child")));
        Dispatcher.UIThread.RunJobs();
        Assert.True(host.Panel.GoBack());
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Focused(host), root().Rows);

        host.Close();
    }

    [AvaloniaFact]
    public void BackAfterClickingARowMovesNoFocus()
    {
        var (host, root, _, _) = Open(rebuild: false);

        var row = root().Rows[1];
        var at = row.TranslatePoint(new Avalonia.Point(10, 10), host.Window)!.Value;
        host.Window.MouseDown(at, Avalonia.Input.MouseButton.Left);
        host.Window.MouseUp(at, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, host.Panel.Nav.Trail.Count);

        Back(host);

        Assert.DoesNotContain(Focused(host), root().Rows);

        host.Close();
    }

    [AvaloniaFact]
    public void ForgettingTheLevelDropsWhatWasRemembered()
    {
        var (host, root, _, _) = Open(rebuild: false);

        Press(host, Key.Down);
        Press(host, Key.Enter);

        host.Panel.Forget(PanelTab.Navigation, key => key == "root");
        Back(host);

        Assert.DoesNotContain(Focused(host), root().Rows);

        host.Close();
    }
}
