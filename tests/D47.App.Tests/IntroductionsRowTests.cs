using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Configuration;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The button that puts every core back to its opening line. Driven through the real settings
/// window, because the whole point of the row is that a Commander can press it without
/// restarting d47 — and a row that clears the state but never says so leaves them pressing it
/// again to find out whether it worked.
/// </summary>
public class IntroductionsRowTests
{
    [AvaloniaFact]
    public void PressingItForgetsEveryIntroductionAndTheRowSaysSo()
    {
        var personas = new PersonaHost();
        personas.Apply(new PersonaSettings { Id = "cora" });
        personas.Apply(new PersonaSettings { Id = "kex" });

        var (settings, viewState, paths) = TestSurface.Create(personas: personas);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.Contains("Cora, Kex", Disclosures(host.View));

        var forget = host.View.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Name == "Press_persona_introductions");

        forget.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(personas.Introduced);
        Assert.Contains("No core has introduced itself yet", Disclosures(host.View));

        host.Close();
    }

    private static string Disclosures(Visual surface) =>
        string.Join(
            "\n",
            surface.GetVisualDescendants().OfType<SelectableTextBlock>().Select(block => block.Text));
}
