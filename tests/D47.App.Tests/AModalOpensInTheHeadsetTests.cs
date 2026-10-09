using D47.Core.Storage;
using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Interface;
using D47.Core.Lore;
using D47.Core.Memory;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A Settings row that opens a modal opens it on the headset's own copy of the panel, where the ray can
/// answer it (#530).
/// </summary>
[Trait("Category", "Integration")]
public class AModalOpensInTheHeadsetTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private const string KeyRow = "llm.anthropic.apiKey";

    private readonly string _folder = TempFolders.Create("d47-headset-modal");

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    /// <summary>Every method that ends in a dialog over the panel, read out of the compiled IL once.</summary>
    private static readonly Lazy<IReadOnlyCollection<string>> Opens =
        new(() => AssemblyCalls.Reaching(typeof(SettingsView).Assembly, "Dialogs", "Over"));

    /// <summary>Every opener this host builds, by name, and the modal each one opens.</summary>
    private static readonly Dictionary<string, Type> Built = new()
    {
        ["FORGET KEY"] = typeof(ConfirmDialog),
        ["OpenLore"] = typeof(LoreDialog),
        ["OpenMacros"] = typeof(MacroDialog),
        ["OpenMemories"] = typeof(MemoryDialog),
    };

    /// <summary>The settings surface with every row shown, a key stored, and lore, memory and macros wired.</summary>
    private SettingsView Attached(SettingsService settings, ViewStateStore viewState, D47.Core.AppPaths paths)
    {
        Directory.CreateDirectory(_folder);

        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);
        settings.Apply(KeyRow, "sk-not-a-real-key", SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var memories = new MemoryBook(
            new MemoryStore(Path.Combine(paths.Data, "memory.json"), new DiskFileSystem(), NullLogger<MemoryStore>.Instance),
            () => "F1",
            () => MemorySituation.Unknown);

        var lore = new LoreEditing(
            new LoreBook(new LoreStore(Path.Combine(paths.Data, "lore.json"), new DiskFileSystem(), NullLogger<LoreStore>.Instance)),
            () => new LoreCapability.LorePlace(1L, "Colonia", "F1"),
            () => false,
            (_, _) => Task.FromResult<string?>(null),
            () => Instant);

        var view = new SettingsView();

        view.Attach(
            settings,
            viewState,
            () => new CoverageReport([]),
            new D47.Core.Actions.MacroStore(
                Path.Combine(paths.Data, "macros.json"), new DiskFileSystem(),
                NullLogger<D47.Core.Actions.MacroStore>.Instance),
            lore: lore,
            memories: (memories, () => Instant),
            ownPersonas: new OwnPersonaStore(
                Path.Combine(_folder, "personas.json"), new DiskFileSystem(),
                NullLogger<OwnPersonaStore>.Instance));

        return view;
    }

    /// <summary>The headset's copy of the panel, in full, on Settings.</summary>
    private (VrPanelSurface Surface, SettingsView View, SettingsService Settings) Headset()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var view = Attached(settings, viewState, paths);

        var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null, settingsPage: () => view);

        surface.Nav.Select(PanelTab.Settings);
        Jobs();
        Render(surface);

        return (surface, view, settings);
    }

    private static void Render(VrPanelSurface surface)
    {
        surface.Board.Render();
        Jobs();
        surface.Board.Render();
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

    private static List<Button> Openers(SettingsView view) =>
        [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => Handlers(button).Any(handler =>
                Opens.Value.Contains($"{handler.Method.DeclaringType?.Name}.{handler.Method.Name}")))];

    private static string Describe(Button button) =>
        button.Name ?? button.Content as string ?? button.GetType().Name;

    /// <summary>Presses a control where it is drawn, in the 0..1 a ray answers in.</summary>
    private static bool Press(VrPanelSurface surface, Control control)
    {
        // Scrolled to by hand: there is no layout manager offscreen to answer BringIntoView.
        if (control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { Content: Visual content } viewer
            && control.TranslatePoint(new Point(0, 0), content) is { } within)
        {
            var bottom = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
            viewer.Offset = viewer.Offset.WithY(Math.Clamp(within.Y - 20, 0, bottom));

            Render(surface);
        }

        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), surface.Board.View);

        Assert.NotNull(centre);

        return At(surface, centre!.Value);
    }

    private static bool At(VrPanelSurface surface, Point at)
    {
        var (width, height) = surface.Size;
        var landed = surface.Press((float)(at.X / width), (float)(at.Y / height));

        Jobs();
        Render(surface);

        return landed;
    }

    private static Button InModal(VrPanelSurface surface, string label) =>
        surface.Modal!.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, label));

    /// <summary>Opens one opener's modal on the headset, by the ray.</summary>
    private static ModalDialog Open(VrPanelSurface surface, SettingsView view, string opener)
    {
        for (var i = 0; i < view.SectionIds.Count; i++)
        {
            view.ShowPlace(i);
            Jobs();
            Render(surface);

            if (Openers(view).FirstOrDefault(button => Describe(button) == opener && button.IsVisible) is { } found)
            {
                Assert.True(Press(surface, found), $"{opener} was not pressed");

                return Assert.IsAssignableFrom<ModalDialog>(surface.Modal);
            }
        }

        throw new InvalidOperationException($"{opener} is on no place of the page");
    }

    /// <summary>No opener carries the class that makes the headset refuse it.</summary>
    [AvaloniaFact]
    public void NoRowThatOpensAModalIsRefusedToTheRay()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var view = Attached(settings, viewState, paths);
        var window = new Window { Content = view, Width = 1180, Height = 880 };

        window.Show();
        Jobs();

        var openers = new List<Button>();

        for (var i = 0; i < view.SectionIds.Count; i++)
        {
            view.ShowPlace(i);
            Jobs();
            openers.AddRange(Openers(view));
        }

        // The sweep finds every opener, so a sweep that quietly finds nothing fails here.
        Assert.Equal(Built.Keys.Order(), openers.Select(Describe).Distinct().Order());

        Assert.Empty(openers.Where(button => button.Classes.Contains(OffscreenSurface.DesktopOnly)).Select(Describe));

        window.Close();
    }

    /// <summary>Each modal opens on the headset's panel, and its own button closes it there.</summary>
    [AvaloniaFact]
    public void EachModalOpensInTheHeadsetAndClosesThere()
    {
        var (surface, view, _) = Headset();
        using var _ = surface;

        foreach (var (opener, type) in Built)
        {
            var modal = Open(surface, view, opener);

            Assert.IsType(type, modal);
            Assert.False(surface.Board.IsChoosing, $"{opener} was refused");
            Assert.Contains(modal, surface.Board.View.GetVisualDescendants());

            Assert.True(Press(surface, InModal(surface, modal is ConfirmDialog ? "Keep it" : "Close")));

            Assert.Null(surface.Modal);
            Assert.DoesNotContain(surface.Board.View.GetVisualDescendants(), visual => visual is ModalDialog);
        }
    }

    /// <summary>The modal is centred over a scrim that covers the panel, at most 640 wide.</summary>
    [AvaloniaFact]
    public void TheModalIsCentredOverTheScrimAtMostSixFortyWide()
    {
        var (surface, view, _) = Headset();
        using var _ = surface;

        Open(surface, view, "OpenMacros");

        var scrim = surface.Board.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == ModalHost.ScrimName);
        var frame = surface.Board.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == ModalHost.FrameName);
        var (width, height) = surface.Size;

        Assert.Equal(new Size(width, height), scrim.Bounds.Size);
        Assert.InRange(frame.Bounds.Width, 1, Modal.Width + 2);

        var left = frame.TranslatePoint(new Point(0, 0), scrim)!.Value.X;
        Assert.Equal(width - frame.Bounds.Width - left, left, precision: 0);

        surface.Board.Render().SaveCapture("headset-modal-macros.png");
    }

    /// <summary>A press on the scrim closes the modal and presses nothing on the panel under it.</summary>
    [AvaloniaFact]
    public void APressOnTheScrimReachesNothingUnderIt()
    {
        var (surface, view, _) = Headset();
        using var _ = surface;

        Open(surface, view, "OpenMacros");

        var frame = surface.Board.View.GetVisualDescendants().OfType<Border>().Single(b => b.Name == ModalHost.FrameName);
        var framed = new Rect(frame.TranslatePoint(new Point(0, 0), surface.Board.View)!.Value, frame.Bounds.Size);

        var under = surface.Board.View.GetVisualDescendants().OfType<Button>()
            .Where(button => Shown(button, surface.Board.View)
                && button.Bounds.Width > 0
                && !button.GetVisualAncestors().OfType<ModalHost>().Any())
            .Select(button => (Button: button, At: button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), surface.Board.View)))
            .First(found => found.At is { } at
                && !framed.Contains(at)
                && new Rect(0, 0, surface.Size.Width, surface.Size.Height).Contains(at));

        var clicked = false;
        under.Button.Click += (_, _) => clicked = true;

        At(surface, under.At!.Value);

        Assert.False(clicked, $"{Describe(under.Button)} was pressed through the scrim");
        Assert.Null(surface.Modal);
    }

    /// <summary>Whether a control is drawn: it and everything above it up to the surface is visible.</summary>
    private static bool Shown(Visual control, Visual surface) =>
        control.GetSelfAndVisualAncestors().TakeWhile(visual => !ReferenceEquals(visual, surface)).All(visual => visual.IsVisible);

    /// <summary>A button inside the modal takes the ray: Delete key in the confirmation deletes the key.</summary>
    [AvaloniaFact]
    public void TheModalsOwnButtonsTakeTheRay()
    {
        var (surface, view, settings) = Headset();
        using var _ = surface;

        var secret = settings.Sections.SelectMany(section => section.Rows).First(row => row.Key == KeyRow).SecretName!;

        Assert.True(settings.HasSecret(secret));

        Open(surface, view, "FORGET KEY");

        Assert.True(Press(surface, InModal(surface, "Delete key")));

        Assert.Null(surface.Modal);
        Assert.False(settings.HasSecret(secret));
    }

    /// <summary>A text box in a modal takes the headset's spelled board.</summary>
    [AvaloniaFact]
    public void AModalsTextBoxTakesTheSpelledBoard()
    {
        var (surface, view, _) = Headset();
        using var _ = surface;

        var modal = Open(surface, view, "OpenMemories");
        var fact = modal.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "MemoryFact");

        Assert.True(Press(surface, fact));
        Assert.True(surface.Board.IsListening);
    }

    /// <summary>The controller's Back closes the modal before it leaves the page.</summary>
    [AvaloniaFact]
    public void BackClosesTheModalFirst()
    {
        var (surface, view, _) = Headset();
        using var _ = surface;

        Open(surface, view, "OpenLore");

        Assert.True(surface.Back());

        Assert.Null(surface.Modal);
        Assert.Equal(PanelTab.Settings, surface.Nav.Tab);
    }

    /// <summary>On the desktop the same row opens the modal over the window.</summary>
    [AvaloniaFact]
    public void TheDesktopWindowStillOpensIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var view = Attached(settings, viewState, paths);
        var window = new Window { Content = view, Width = 1180, Height = 880 };

        window.Show();
        Jobs();

        view.ShowPlaceOf(MacroCapability.ListKey);
        Jobs();

        view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "OpenMacros")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Jobs();

        var opened = Assert.Single(window.Modals().OfType<MacroDialog>());

        opened.Close();
        Jobs();
        window.Close();
    }
}
