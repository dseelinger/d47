using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Settings lists every saved binding profile with a Delete button that removes it (#80).</summary>
[Trait("Category", "Integration")]
public sealed class SavedBindingProfilesAreListedWithADeleteTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void EachSavedProfileHasADeleteThatRemovesIt()
    {
        var folder = TempFolders.Create("d47-binding-profiles");
        var (settings, viewState, paths) = TestSurface.Create(root: folder);
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var bindings = TestSurface.BindingsFolder(paths);
        Directory.CreateDirectory(bindings);
        File.WriteAllText(Path.Combine(bindings, "StartPreset.4.start"), "Custom\nCustom\nCustom\nCustom");
        File.WriteAllText(Path.Combine(bindings, "Custom.4.2.binds"), "<Root />");

        var store = TestSurface.BindingProfilesFor(paths);
        Assert.True(store.Save("sim pit").Done);
        Assert.True(store.Save("desk").Done);

        var host = SettingsHost.Open(settings, viewState, paths, bindingProfiles: store);
        host.ShowPlaceOf(BindingProfilesCapability.ListKey);
        Jobs();

        var row = Assert.IsAssignableFrom<Control>(host.ControlFor(BindingProfilesCapability.ListKey));
        Assert.Contains("2 saved: desk, sim pit.", Texts(row));
        Assert.Equal(2, Deletes(row).Count);

        Capture(host.Window, "binding-profiles-two-saved");

        Deletes(row)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Equal(["sim pit"], store.Names);
        Assert.Contains("1 saved: sim pit.", Texts(row));
        Assert.Single(Deletes(row));

        Capture(host.Window, "binding-profiles-after-delete");

        host.Close();
    }

    [AvaloniaFact]
    public void SaveAndLoadButtonsActOnTheStoreAndShowTheReply()
    {
        var folder = TempFolders.Create("d47-binding-profiles-buttons");
        var (settings, viewState, paths) = TestSurface.Create(root: folder);
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var bindings = TestSurface.BindingsFolder(paths);
        Directory.CreateDirectory(bindings);
        File.WriteAllText(Path.Combine(bindings, "StartPreset.4.start"), "Custom\nCustom\nCustom\nCustom");
        File.WriteAllText(Path.Combine(bindings, "Custom.4.2.binds"), "<Root />");

        var running = false;
        var store = new D47.Core.Input.BindingProfiles(bindings, paths.BindingProfiles, () => running, NullLogger.Instance);

        var host = SettingsHost.Open(settings, viewState, paths, bindingProfiles: store);
        host.ShowPlaceOf(BindingProfilesCapability.ListKey);
        Jobs();

        var row = Assert.IsAssignableFrom<Control>(host.ControlFor(BindingProfilesCapability.ListKey));
        var box = row.GetVisualDescendants().OfType<TextBox>().Single();
        var save = Buttons(row, "Save").Single();
        Assert.False(save.IsEnabled);

        box.Text = "   ";
        Jobs();
        Assert.False(save.IsEnabled);

        running = true;
        box.Text = "desk";
        Jobs();
        Assert.True(save.IsEnabled);
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        Assert.Contains(D47.Core.Input.BindingProfiles.EliteIsRunning, Texts(row));
        Assert.Empty(store.Names);

        running = false;
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        Assert.Equal(["desk"], store.Names);
        Assert.Contains("Saved your bindings as \"desk\".", Texts(row));
        Assert.Single(Buttons(row, "Load"));

        running = true;
        Buttons(row, "Load")[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        Assert.Contains(D47.Core.Input.BindingProfiles.EliteIsRunning, Texts(row));

        running = false;
        Buttons(row, "Load")[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
        Assert.Contains(Texts(row), text => text.StartsWith("Loaded the \"desk\" bindings.", StringComparison.Ordinal));

        host.Close();
    }

    private static List<Button> Buttons(Control row, string label) =>
        [.. row.GetVisualDescendants().OfType<Button>().Where(button => button.Content as string == label)];

    private static List<Button> Deletes(Control row) =>
        [.. row.GetVisualDescendants().OfType<Button>().Where(button => button.Content as string == "Delete")];

    private static List<string> Texts(Control row) =>
        [.. row.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static void Capture(Window window, string name)
    {
        Jobs();

        window.CaptureRenderedFrame()!.SaveCapture($"{name}.png");
    }
}
