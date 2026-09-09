using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Interface;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A Settings row that opens a window over the panel is a row the headset must refuse: its offscreen host
/// is a Window that is never shown, and a dialog over one of those is a dialog nobody can answer.
/// </summary>
public class ARowThatOpensAWindowRefusesTheRayTests
{
    private static readonly PixelSize Quad = new(1180, 880);

    private readonly string _folder = TempFolders.Create("d47-desktop-only");

    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    /// <summary>Every method that ends in a dialog over the panel, read out of the compiled IL once.</summary>
    private static readonly Lazy<IReadOnlyCollection<string>> Opens =
        new(() => AssemblyCalls.Reaching(typeof(SettingsView).Assembly, "Dialogs", "Over"));

    private static IReadOnlyCollection<string> Opening() => Opens.Value;

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>A type and everything written inside it, closures included.</summary>
    private static IEnumerable<Type> Within(Type type)
    {
        yield return type;

        foreach (var nested in type.GetNestedTypes(Declared).SelectMany(Within))
        {
            yield return nested;
        }
    }

    /// <summary>Every lambda written in a type, as the compiler named it.</summary>
    private static IEnumerable<MethodInfo> Lambdas(Type type) =>
        Within(type)
            .SelectMany(nested => nested.GetMethods(Declared))
            .Where(method => method.Name.StartsWith('<') && method.Name.Contains("b__", StringComparison.Ordinal));

    /// <summary>The method a lambda was written in.</summary>
    private static string Owner(string lambda) => lambda[1..lambda.IndexOf('>', StringComparison.Ordinal)];

    /// <summary>The type a closure was written in.</summary>
    private static Type Root(Type type) => type.DeclaringType is { } outer ? Root(outer) : type;

    /// <summary>
    /// Found by what a handler calls rather than by a list kept by hand: a row added later that opens a
    /// window and does not mark its control fails the build rather than the headset.
    /// </summary>
    [Fact]
    public void EveryHandlerThatOpensAWindowMarksTheControlItHangsOff()
    {
        var assembly = typeof(SettingsView).Assembly;
        var opening = Opening();

        var handlers = Lambdas(typeof(SettingsView))
            .Concat(Lambdas(typeof(SecretEditor)))
            .Where(method => opening.Contains($"{method.DeclaringType!.Name}.{method.Name}"))
            .Select(method => (Type: Root(method.DeclaringType!).Name, Owner: Owner(method.Name)))
            .Distinct()
            .ToList();

        // The rows that reach the ray today, so a sweep that quietly finds nothing fails here.
        Assert.Superset(
            new HashSet<string>
            {
                "BuildAudioRecording",
                "BuildCoverage",
                "BuildDebrief",
                "BuildLogbook",
                "BuildLore",
                "BuildMacros",
                "BuildMemories",
                "BuildOwnPersonas",
                "BuildSwitches",
            },
            handlers.Select(handler => handler.Owner).ToHashSet());

        Assert.Empty(
            handlers
                .Where(handler => !AssemblyCalls.Calls(
                    assembly, handler.Type, handler.Owner, nameof(OffscreenSurface.OpensAWindow)))
                .Select(handler => $"{handler.Type}.{handler.Owner}"));
    }

    /// <summary>The settings surface, wired as the window wires it and with every row shown.</summary>
    private SettingsView Attached()
    {
        Directory.CreateDirectory(_folder);

        var recording = new D47.Core.Diagnostics.Recording.RecordingLog(_folder, NullLogger.Instance);

        var (settings, viewState, paths) = TestSurface.Create(
            coverage: () => "One line, never exercised.",
            recording: recording);

        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var view = new SettingsView();

        view.Attach(
            settings,
            viewState,
            paths,
            () => new CoverageReport([]),
            new D47.Core.Actions.MacroStore(
                Path.Combine(paths.Data, "macros.json"),
                NullLogger<D47.Core.Actions.MacroStore>.Instance),
            switches: Switches(paths),
            ownPersonas: new OwnPersonaStore(
                Path.Combine(_folder, "personas.json"),
                NullLogger<OwnPersonaStore>.Instance),
            recording: (recording, () => DateTimeOffset.UnixEpoch));

        return view;
    }

    /// <summary>A controller seam with nothing plugged in — enough to compose the row.</summary>
    private static SwitchEditing Switches(D47.Core.AppPaths paths) => new(
        new D47.Core.Hotas.SwitchStore(
            Path.Combine(paths.Data, "switches.json"),
            NullLogger<D47.Core.Hotas.SwitchStore>.Instance),
        new D47.Core.Hotas.FakeHotasReader(),
        new D47.Core.Hotas.SwitchReconciler(NullLogger<D47.Core.Hotas.SwitchReconciler>.Instance),
        () => DateTimeOffset.UnixEpoch,
        Path.Combine(paths.Data, "switch-capture.txt"),
        () => []);

    /// <summary>Every card open, so a row inside a shut one is still a control the sweep can see.</summary>
    private static void Expand(SettingsView view)
    {
        view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "ExpandAll")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Jobs();
    }

    /// <summary>The settings surface the way the headset builds it: offscreen, in a window never shown.</summary>
    private (OffscreenSurface Surface, SettingsView View) Headset()
    {
        var view = Attached();

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSettings(() => view);
        panel.Tab = PanelTab.Settings;

        var surface = new OffscreenSurface(panel, Quad);

        surface.Render();
        Jobs();
        Expand(view);
        surface.Render();

        return (surface, view);
    }

    /// <summary>What is subscribed to a control's Click, whatever subscribed it.</summary>
    private static IEnumerable<Delegate> Handlers(Interactive control)
    {
        var field = typeof(Interactive).GetField("_eventHandlers", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        if (field!.GetValue(control) is not IDictionary subscribed)
        {
            yield break;
        }

        foreach (DictionaryEntry entry in subscribed)
        {
            if (!ReferenceEquals(entry.Key, Button.ClickEvent) || entry.Value is not IEnumerable subscriptions)
            {
                continue;
            }

            foreach (var subscription in subscriptions)
            {
                foreach (var held in subscription.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (held.GetValue(subscription) is Delegate handler)
                    {
                        yield return handler;
                    }
                }
            }
        }
    }

    /// <summary>Whether pressing this button ends in a window over the panel.</summary>
    private static bool OpensAWindow(Button button, IReadOnlyCollection<string> opening) =>
        Handlers(button).Any(handler =>
            opening.Contains($"{handler.Method.DeclaringType?.Name}.{handler.Method.Name}"));

    private static List<Button> Openers(SettingsView view)
    {
        var opening = Opening();

        return [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => OpensAWindow(button, opening))];
    }

    /// <summary>What to call a button in a failure: its name, or what it says about itself.</summary>
    private static string Describe(Button button) =>
        button.Name ?? Avalonia.Automation.AutomationProperties.GetName(button)
        ?? button.Content as string ?? button.GetType().Name;

    /// <summary>What this host builds, by name, so the two press tests state what they covered.</summary>
    private static readonly string[] Built =
    [
        "Clear the key",
        "OpenAudioRecorder",
        "OpenCoverage",
        "OpenMacros",
        "OpenOwnPersonas",
        "OpenSwitches",
    ];

    /// <summary>The same, less the one that is not on the page until there is a key to clear.</summary>
    private static readonly string[] Shown = [.. Built.Skip(1)];

    /// <summary>Every one of them carries the class, on the surface as it is actually built.</summary>
    [AvaloniaFact]
    public void EveryButtonThatOpensAWindowCarriesTheClass()
    {
        var view = Attached();
        var window = new Window { Content = view, Width = Quad.Width, Height = Quad.Height };

        window.Show();
        Jobs();
        Expand(view);

        var openers = Openers(view);

        Assert.Equal(Built, openers.Select(Describe).Distinct().Order().ToArray());

        Assert.Empty(
            openers.Where(button => !button.Classes.Contains(OffscreenSurface.DesktopOnly)).Select(Describe));

        window.Close();
    }

    /// <summary>Puts a control where the ray can reach it, and says where that is on the surface.</summary>
    private static Point Reach(OffscreenSurface surface, Control control)
    {
        var viewer = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();

        if (viewer is not null
            && control.TranslatePoint(new Point(0, 0), surface.View) is { } where)
        {
            // Scrolled to by hand: there is no layout manager offscreen to answer BringIntoView.
            viewer.Offset = new Vector(
                viewer.Offset.X,
                Math.Clamp(
                    viewer.Offset.Y + where.Y - (viewer.Viewport.Height / 2),
                    0,
                    Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height)));

            surface.Render();
        }

        var at = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            surface.View);

        Assert.NotNull(at);

        Assert.InRange(at!.Value.Y, 0, Quad.Height);
        Assert.InRange(at.Value.X, 0, Quad.Width);

        return at.Value;
    }

    /// <summary>A press on one of those rows presses nothing and opens nothing.</summary>
    [AvaloniaFact]
    public void APressOnEachOfThemIsRefused()
    {
        var (surface, view) = Headset();
        using var _ = surface;

        var host = (Window)surface.Root;

        // The ones a ray can land on: the secret editor's clear button is hidden until there is a key to
        // clear, and a press where it is not lands on the box behind it.
        var openers = Openers(view).Where(button => button.IsVisible).ToList();

        Assert.Equal(Shown, openers.Select(Describe).Distinct().Order().ToArray());

        foreach (var button in openers)
        {
            Assert.False(surface.Click(Reach(surface, button)), $"{Describe(button)} was pressed");

            Jobs();

            Assert.Empty(host.OwnedWindows);

            surface.Dismiss();
        }
    }

    /// <summary>And the panel says why, rather than appearing to have missed the press.</summary>
    [AvaloniaFact]
    public void TheRefusalIsSaidOnThePanel()
    {
        var (surface, view) = Headset();
        using var _ = surface;

        var coverage = Openers(view).Single(button => button.Name == "OpenCoverage");

        Assert.False(surface.Click(Reach(surface, coverage)));

        surface.Render();

        Assert.True(surface.IsChoosing, "the refusal is drawn over the panel");

        Assert.Contains(
            surface.Root.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == OffscreenSurface.Refusal);
    }

    /// <summary>On the desktop the same row opens the window it always did.</summary>
    [AvaloniaFact]
    public void TheDesktopWindowStillOpensIt()
    {
        var view = Attached();
        var window = new Window { Content = view, Width = Quad.Width, Height = Quad.Height };

        window.Show();
        Jobs();
        Expand(view);

        view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "OpenCoverage")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Jobs();

        var opened = Assert.Single(window.OwnedWindows);

        Assert.IsType<D47.App.Controls.CoverageWindow>(opened);

        opened.Close();
        Jobs();
        window.Close();
    }
}
