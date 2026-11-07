using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Knowledge;

namespace D47.App.Tests;

/// <summary>Reads and drives the search pages through their drawn controls, as a Commander does.</summary>
internal static class SearchPageView
{
    /// <summary>The names a type-ahead lists, read from its drawn rows.</summary>
    internal sealed class Offered(Control page, string fieldName)
    {
        public IReadOnlyList<string> Shown => [.. Rows().Select(row => AutomationProperties.GetName(row) ?? string.Empty)];

        public Border List => page.GetLogicalDescendants().OfType<TextBox>().Single(box => box.Name == fieldName)
            .GetLogicalSiblings().OfType<Border>().Single(border => border.Name == NameSuggestions.ListName);

        public void Choose(string name) => Click(Rows().First(row => AutomationProperties.GetName(row) == name));

        private IEnumerable<Button> Rows() => List.GetLogicalDescendants().OfType<Button>();
    }

    /// <summary>The module and ship rows a type-ahead lists.</summary>
    internal sealed class OfferedItems(Control page)
    {
        private readonly Offered _names = new(page, OutfittingSearchPage.AddName);

        public IReadOnlyList<OutfittingItem> Shown => [.. _names.Shown.Select(name => OutfittingItem.All.First(item => item.Display == name))];

        public void Choose(OutfittingItem item) => _names.Choose(item.Display);
    }

    extension(RouteMarketPage page)
    {
        public Offered Suggestions => new(page, RouteMarketPage.AddName);

        public IReadOnlyList<CommodityWant> Items => [.. page.FormArguments().Values["commodity"]
            .Split(", ", StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split(": ") is [var name, var tonnes]
                ? new CommodityWant(name, int.Parse(tonnes, CultureInfo.InvariantCulture))
                : new CommodityWant(entry, null))];

        public void Weigh(int? tonnes) =>
            page.GetLogicalDescendants().OfType<Amount>().Single(amount => amount.Name == RouteMarketPage.TonnesName).Value = tonnes;
    }

    extension(OutfittingSearchPage page)
    {
        public OfferedItems Suggestions => new(page);

        public void TickSurface(bool ticked) => Box(page, "SURFACE STATIONS").IsChecked = ticked;

        public void TickCarriers(bool ticked) => Box(page, "FLEET CARRIERS").IsChecked = ticked;
    }

    extension(GalaxySearchPage page)
    {
        /// <summary>Whether the answer is drawn in place of the form.</summary>
        public bool ShowsAnswer => !page.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == GalaxySearchPage.RunName).IsEffectivelyVisible;
    }

    extension(BodySearchPage page)
    {
        public void Pick(string name, string value) => PickIn(page, name.Replace('_', ' '), value);

        public void TickFlag(string name, bool ticked) => Box(page, name switch
        {
            "landable" => "LANDABLE",
            "terraformable" => "TERRAFORMING CANDIDATE",
            "tidally_locked" => "TIDALLY LOCKED",
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such flag."),
        }).IsChecked = ticked;
    }

    extension(SystemSearchPage page)
    {
        public void Pick(string name, string value) => PickIn(page, name.Replace('_', ' '), value);

        public int MaxReportAgeHours
        {
            set => PickAge(page, value);
        }
    }

    extension(StationSearchPage page)
    {
        public void Pick(string name, string value) => PickIn(page, name.Replace('_', ' '), value);

        public int MaxReportAgeHours
        {
            set => PickAge(page, value);
        }

        public void TickType(string type, bool ticked) => Box(page, type.ToUpperInvariant()).IsChecked = ticked;
    }

    /// <summary>The picker the signal kind is chosen from.</summary>
    internal static InlinePicker SignalPicker(SignalSearchPage page) => PickerLabelled(page, "Signal");

    private static void PickAge(Control page, int hours) => PickIn(page, "max report age", MaxReportAgeLabel(hours));

    private static string MaxReportAgeLabel(int hours) => MaxReportAge.Label(hours);

    private static CheckBox Box(Control page, string label) =>
        page.GetLogicalDescendants().OfType<CheckBox>().First(box => box.Content is string text && string.Equals(text, label, StringComparison.OrdinalIgnoreCase));

    private static InlinePicker PickerLabelled(Control page, string label) =>
        page.GetLogicalDescendants().OfType<InlinePicker>().First(picker =>
            picker.GetLogicalDescendants().OfType<Button>().First(button => button.Name == InlinePicker.ButtonName) is var opener
            && string.Equals(AutomationProperties.GetName(opener), label, StringComparison.OrdinalIgnoreCase));

    private static void PickIn(Control page, string label, string value)
    {
        var option = PickerLabelled(page, label).GetLogicalDescendants().OfType<Button>()
            .First(button => button.Name != InlinePicker.ButtonName && string.Equals(AutomationProperties.GetName(button), value, StringComparison.OrdinalIgnoreCase));
        Click(option);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
