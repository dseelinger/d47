using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Home system box lists the system names that start with what is typed and stores a picked one (#1090).
/// No other text box lists names.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheHomeSystemBoxSuggestsSystemNamesTests
{
    private static readonly string[] Names = ["Diaguandri", "Dian", "Diabozo"];

    private sealed record Shown(SettingsService Settings, SettingsView View, Window Window, List<string> Asked)
    {
        public TextBox Box(string key) =>
            View.ControlFor(key) is TextBox box
                ? box
                : View.ControlFor(key)!.GetVisualDescendants().OfType<TextBox>().First();

        public IEnumerable<Button> Listed(string key) =>
            View.ControlFor(key)!.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == NameSuggestions.ListName && border.IsEffectivelyVisible)
                .SelectMany(border => border.GetVisualDescendants().OfType<Button>());
    }

    private static Shown Open(bool lookups)
    {
        var (settings, viewState, _) = TestSurface.Create();
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        var asked = new List<string>();
        var view = new SettingsView();

        view.Attach(
            settings,
            viewState,
            systemNames: new SystemNameLookup(
                (typed, _) =>
                {
                    lock (asked)
                    {
                        asked.Add(typed);
                    }

                    return Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                        [.. Names
                            .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                            .Select(name => new SystemNameMatch(name, name.Length, new StarPosition(0, 0, 0)))]);
                },
                () => lookups,
                TimeSpan.Zero));

        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Shown(settings, view, window, asked);
    }

    private static void Type(TextBox box, string text)
    {
        box.Focus();
        box.Text = text;
        Dispatcher.UIThread.RunJobs();

        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void PickingANameFromTheHomeSystemListStoresIt()
    {
        var shown = Open(lookups: true);
        var box = shown.Box(CalloutCapability.HomeSystemKey);

        Type(box, "Dia");

        Assert.Equal(["Diaguandri", "Dian", "Diabozo"], shown.Listed(CalloutCapability.HomeSystemKey).Select(b => b.Content is TextBlock t ? t.Text : null));

        shown.Listed(CalloutCapability.HomeSystemKey)
            .First(b => (b.Content as TextBlock)?.Text == "Diaguandri")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Diaguandri", box.Text);
        Assert.Equal("Diaguandri", shown.Settings.Current.Callouts.HomeSystem);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffTheHomeSystemBoxListsNothingAndStillSaves()
    {
        var shown = Open(lookups: false);
        var box = shown.Box(CalloutCapability.HomeSystemKey);

        Type(box, "Dia");

        Assert.Empty(shown.Listed(CalloutCapability.HomeSystemKey));
        Assert.Empty(shown.Asked);

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Dia", shown.Settings.Current.Callouts.HomeSystem);

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void AnotherTextBoxDrawsNoList()
    {
        var shown = Open(lookups: true);
        var box = shown.Box(ListeningCapability.WakeWordsKey);

        Type(box, "Dia");

        Assert.Empty(shown.Listed(ListeningCapability.WakeWordsKey));
        Assert.Empty(shown.Asked);

        shown.Window.Close();
    }
}
