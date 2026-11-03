using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Modules;

namespace D47.App.Panel;

/// <summary>Modules › Installed: every installed module with its author, page count and status.</summary>
internal sealed class ModulesInstalledPage : UserControl, IPageSummary
{
    public const string RowName = "ModuleRow";
    public const string SettingsName = "ModulesSettings";

    private readonly ModulesSource _source;
    private readonly Action<InstalledModule> _open;
    private readonly Action _openSettings;
    private readonly StackPanel _body = new() { Spacing = 20, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };

    public ModulesInstalledPage(ModulesSource source, Action<InstalledModule> open, Action openSettings)
    {
        _source = source;
        _open = open;
        _openSettings = openSettings;

        Content = new DockPanel { Margin = new Thickness(14), Children = { LoadoutPages.Scrolling(_body) } };
        Draw();
    }

    public string Summary => ModulesTab.Summary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public void Draw()
    {
        _body.Children.Clear();

        var rows = new StackPanel { Spacing = 2 };

        foreach (var module in _source.Installed)
        {
            rows.Children.Add(Row(module));
        }

        _body.Children.Add(rows);

        var settings = new Button { Name = SettingsName, Content = "MODULES SETTINGS", MinHeight = TypeScale.MinimumTarget };
        settings.Click += (_, _) => _openSettings();

        var footer = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(settings, Dock.Right);
        footer.Children.Add(settings);
        var note = ModuleKit.Text(ModulesTab.ListFooter, Fonts.ProseFamily, 14, ThemeManager.GreyKey);
        note.Margin = new Thickness(0, 0, 16, 0);
        note.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(note);
        _body.Children.Add(footer);
    }

    private Button Row(InstalledModule module)
    {
        var status = ModulesTab.StatusOf(module, _source.Statuses);
        var name = ModulesTab.NameOf(module);

        var main = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ModuleKit.Text(name, Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey, FontWeight.SemiBold),
                ModuleKit.Text(ModulesTab.RowSub(module), Fonts.ProseFamily, TypeScale.Secondary, ThemeManager.GreyKey),
            },
        };

        var aside = ModuleKit.Text(
            ModulesTab.StatusWord(status),
            Fonts.ChromeFamily,
            TypeScale.Meta,
            status is ModuleStatus.StoppedAfterRepeatedCrashes or ModuleStatus.Refused ? ThemeManager.RedKey
            : status == ModuleStatus.Running ? ThemeManager.CyanKey
            : ThemeManager.GreyKey);
        aside.Margin = new Thickness(12, 0, 0, 0);
        aside.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(aside, 1);

        var button = new Button
        {
            Name = RowName + "." + name,
            MinHeight = 52,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = ModulesTab.Openable(module),
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Children = { main, aside },
            },
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => _open(module);

        return button;
    }
}

/// <summary>A module's page: its state until the module sends the page itself.</summary>
internal sealed class ModulePageView : UserControl, IPageSummary
{
    public const string NoticeName = "ModulePageNotice";
    public const string RestartName = "ModulePageRestart";
    public const string TryAgainName = "ModulePageTryAgain";
    public const string SettingsName = "ModulePageSettings";
    public const string StartingName = "ModulePageStarting";
    public const string OffName = "ModulePageOff";

    private readonly ModulesSource _source;
    private readonly string _id;
    private readonly Action<string> _openSettingsPlace;
    private readonly StackPanel _body = new() { Spacing = 20, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };

    public ModulePageView(ModulesSource source, string id, Action<string> openSettingsPlace)
    {
        _source = source;
        _id = id;
        _openSettingsPlace = openSettingsPlace;

        Content = new DockPanel { Margin = new Thickness(14), Children = { LoadoutPages.Scrolling(_body) } };
        Draw();
    }

    public string Summary =>
        Module() is { } module ? ModulesTab.PageSummary(module.Manifest.Author) : string.Empty;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    private ReadyModule? Module() =>
        _source.Installed.OfType<ReadyModule>().FirstOrDefault(m => m.Manifest.Id == _id);

    public void Draw()
    {
        _body.Children.Clear();

        if (Module() is not { } module)
        {
            return;
        }

        var name = module.Manifest.Name;
        var status = ModulesTab.StatusOf(module, _source.Statuses);
        var restart = () => _source.Restart(_id);

        switch (ModulesTab.StateOf(status))
        {
            case ModulePageState.Starting:
                _body.Children.Add(ModuleKit.Prose(ModulesTab.StartingText(name), StartingName));
                break;

            case ModulePageState.NotResponding:
                var last = ModulesTab.ReportOf(module, _source.Statuses)?.LastAnswer;
                _body.Children.Add(Notice(
                    NoticeLevel.Warning,
                    ModulesTab.NotRespondingLabel(name),
                    ModulesTab.NotRespondingText,
                    ModulesTab.LastAnswer(last),
                    Tile("RESTART", RestartName, restart)));
                break;

            case ModulePageState.Stopped:
                _body.Children.Add(Notice(
                    NoticeLevel.Error, ModulesTab.StoppedLabel(name), ModulesTab.StoppedText, null, Tile("RESTART", RestartName, restart)));
                break;

            case ModulePageState.Off:
                var settings = Tile("MODULES SETTINGS", SettingsName, () => _openSettingsPlace(D47.Core.Configuration.SettingsLayout.ModulesPlace));
                settings.Margin = new Thickness(2, 0, 0, 0);
                settings.VerticalAlignment = VerticalAlignment.Stretch;

                var row = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(settings, Dock.Right);
                row.Children.Add(settings);
                row.Children.Add(ModuleKit.Prose(ModulesTab.OffText(name), OffName));
                _body.Children.Add(row);
                break;

            default:
                _body.Children.Add(Notice(
                    NoticeLevel.Error,
                    ModulesTab.NotArrivedLabel,
                    ModulesTab.NotArrivedText(name),
                    null,
                    Tile("TRY AGAIN", TryAgainName, Draw),
                    Tile("RESTART", RestartName, restart)));
                break;
        }
    }

    private static Button Tile(string label, string name, Action press)
    {
        var button = new Button { Name = name, Content = label, MinHeight = TypeScale.MinimumTarget, VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => press();
        return button;
    }

    private static Notice Notice(NoticeLevel level, string label, string text, string? detail, params Button[] actions)
    {
        var notice = new Notice(level) { Name = NoticeName, Label = label.ToUpperInvariant(), Text = text, Detail = detail };

        foreach (var action in actions)
        {
            notice.Actions.Add(action);
        }

        return notice;
    }
}

internal static class ModuleKit
{
    public static TextBlock Text(string text, string family, double size, string key, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    /// <summary>A line of white prose on the slab ground.</summary>
    public static Border Prose(string text, string name)
    {
        var block = Text(text, Fonts.ProseFamily, 14, ThemeManager.WhiteKey);
        block.VerticalAlignment = VerticalAlignment.Center;

        var border = new Border { Name = name, Padding = new Thickness(14, 10), MinHeight = TypeScale.MinimumTarget, Child = block };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }
}
