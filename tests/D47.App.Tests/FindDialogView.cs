using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.LogicalTree;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Tests;

/// <summary>Reads and drives a <see cref="FindDialog"/> through its drawn controls.</summary>
internal static class FindDialogView
{
    extension(FindDialog dialog)
    {
        public string Query
        {
            get => Named<TextBox>(dialog, FindDialog.FieldName).Text ?? string.Empty;
            set => Named<TextBox>(dialog, FindDialog.FieldName).Text = value;
        }

        public bool HintShown => Named<TextBlock>(dialog, FindDialog.HintName).IsVisible;

        public string? Count => Footer(dialog).Text;

        public string? TileText => Named<Button>(dialog, FindDialog.TileName) is { IsVisible: true } tile
            ? tile.GetLogicalDescendants().OfType<TextBlock>().Last().Text
            : null;

        public int SelectedIndex
        {
            get
            {
                var knock = Application.Current!.FindResource(ThemeManager.KnockKey);
                var rows = Rows(dialog);
                return rows.FindIndex(row => Title(row).Foreground is ISolidColorBrush ink && knock is ISolidColorBrush target && ink.Color == target.Color);
            }
        }

        public IReadOnlyList<Run> PathRuns(int index) => [.. Path(Rows(dialog)[index]).Inlines!.OfType<Run>()];
    }

    private static T Named<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().Single(control => control.Name == name);

    private static TextBlock Footer(Control root) =>
        Named<Control>(root, FindDialog.FooterName).GetLogicalDescendants().OfType<TextBlock>().Last();

    private static List<Border> Rows(Control root) => [.. root.GetLogicalDescendants().OfType<Border>()
        .Where(border => border.Name?.StartsWith(FindDialog.ResultPrefix, StringComparison.Ordinal) == true)
        .OrderBy(border => int.Parse(border.Name![FindDialog.ResultPrefix.Length..], System.Globalization.CultureInfo.InvariantCulture))];

    private static TextBlock Title(Border row) => row.GetLogicalDescendants().OfType<TextBlock>().ElementAt(1);

    private static TextBlock Path(Border row) => row.GetLogicalDescendants().OfType<TextBlock>().ElementAt(2);
}
