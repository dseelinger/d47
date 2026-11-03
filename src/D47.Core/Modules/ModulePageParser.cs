using System.Globalization;
using System.Text.Json;

namespace D47.Core.Modules;

/// <summary>A module page as parsed: the page, or the reason the whole page was refused.</summary>
public sealed record ModulePageParse(ModulePageContent? Page, string? Refusal)
{
    public static ModulePageParse Refused(string reason) => new(null, reason);
}

/// <summary>
/// Reads a page a module sent as the resource <c>d47://pages/&lt;id&gt;</c>. Every object has a closed set of
/// properties, so a page that gives a colour, font, image or size, names an unknown component or tone, holds a
/// third notice, or names a tool or page that is not the module's own, is refused whole.
/// </summary>
public static class ModulePageParser
{
    public const string UriPrefix = "d47://pages/";

    public const int MaxNotices = 2;

    public const int MaxLength = 256 * 1024;

    public const int MaxParts = 200;

    public const int MaxRows = 500;

    public const int MaxText = 2000;

    public const int MaxColumns = 8;

    public const int MaxButtons = 8;

    public const int MaxStepperOptions = 100;

    /// <summary>Properties that would set how a part looks; a module names a tone instead.</summary>
    private static readonly HashSet<string> Presentation = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "colour", "background", "backgroundColor", "foreground", "ink",
        "font", "fontFamily", "fontSize", "fontWeight", "typeface",
        "image", "icon", "picture", "src", "url",
        "size", "width", "height", "minWidth", "maxWidth", "minHeight", "maxHeight", "padding", "margin",
        "style", "class", "css", "html",
    };

    public static string Uri(string pageId) => UriPrefix + pageId;

    /// <param name="tools">The module's own tool names, as it lists them.</param>
    /// <param name="pages">The page ids in the module's manifest.</param>
    public static ModulePageParse Parse(string json, IReadOnlySet<string> tools, IReadOnlySet<string> pages)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > MaxLength)
        {
            return ModulePageParse.Refused($"the page is longer than {MaxLength} characters");
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            return new ModulePageParse(new Reader(tools, pages).Page(document.RootElement), null);
        }
        catch (JsonException ex)
        {
            return ModulePageParse.Refused("the page is not JSON: " + ex.Message);
        }
        catch (PageRefusal refusal)
        {
            return ModulePageParse.Refused(refusal.Message);
        }
    }

    private sealed class PageRefusal(string reason) : Exception(reason);

    private sealed class Reader(IReadOnlySet<string> tools, IReadOnlySet<string> pages)
    {
        private int _notices;

        public ModulePageContent Page(JsonElement root)
        {
            Object(root, "the page", "parts");
            var parts = Array(root, "parts", "the page", 1, MaxParts);

            return new ModulePageContent([.. parts.Select((part, index) => Part(part, index + 1))]);
        }

        private ModulePart Part(JsonElement part, int number)
        {
            if (part.ValueKind != JsonValueKind.Object)
            {
                throw new PageRefusal($"part {number} is not an object");
            }

            var type = part.TryGetProperty("type", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString()! : null;
            var what = $"part {number} ({type})";

            return type switch
            {
                "GroupHead" => GroupHead(part, what),
                "DataBlock" => DataBlock(part, what),
                "Notice" => Notice(part, what),
                "SettingsRow" => SettingsRow(part, what),
                "CheckboxTiles" => CheckboxTiles(part, what),
                "List" => List(part, what),
                "TileButtons" => TileButtons(part, what),
                "Table" => Table(part, what),
                "Message" => Message(part, what),
                null => throw new PageRefusal($"part {number} has no type"),
                _ => throw new PageRefusal($"part {number} is an unknown component \"{type}\""),
            };
        }

        private static ModuleGroupHead GroupHead(JsonElement part, string what)
        {
            Object(part, what, "type", "title", "description");
            return new ModuleGroupHead(Text(part, "title", what)!, Text(part, "description", what, required: false));
        }

        private static ModuleDataBlock DataBlock(JsonElement part, string what)
        {
            Object(part, what, "type", "cells", "prose", "tone");

            if (Text(part, "prose", what, required: false) is { } prose)
            {
                if (part.TryGetProperty("cells", out _))
                {
                    throw new PageRefusal($"{what} has both cells and prose");
                }

                return new ModuleDataBlock([], prose, Tone(part, what));
            }

            if (part.TryGetProperty("tone", out _))
            {
                throw new PageRefusal($"{what} gives a tone for the block; each cell has its own");
            }

            var cells = Array(part, "cells", what, 1, 3).Select((cell, index) =>
            {
                var where = $"{what} cell {index + 1}";
                Object(cell, where, "label", "value", "tone");
                return new ModuleDatum(Text(cell, "label", where)!, Text(cell, "value", where)!, Tone(cell, where));
            });

            return new ModuleDataBlock([.. cells], null, null);
        }

        private ModulePageNotice Notice(JsonElement part, string what)
        {
            Object(part, what, "type", "tone", "label", "text");

            if (++_notices > MaxNotices)
            {
                throw new PageRefusal($"{what} is a third notice; a page holds at most {MaxNotices}");
            }

            var tone = Tone(part, what);

            if (tone is not (ModuleTone.Warning or ModuleTone.Error))
            {
                throw new PageRefusal($"{what} must be a warning or an error");
            }

            return new ModulePageNotice(tone.Value, Text(part, "label", what)!, Text(part, "text", what)!);
        }

        private ModuleSettingsRow SettingsRow(JsonElement part, string what)
        {
            Object(part, what, "type", "label", "status", "segmented", "stepper");

            var label = Text(part, "label", what)!;
            var given = new[] { "status", "segmented", "stepper" }.Where(name => part.TryGetProperty(name, out _)).ToList();

            if (given.Count != 1)
            {
                throw new PageRefusal($"{what} must hold one of status, segmented or stepper");
            }

            var control = part.GetProperty(given[0]);

            return new ModuleSettingsRow(label, given[0] switch
            {
                "status" => Status(control, what + " status"),
                "segmented" => Choice(control, what + " segmented", stepper: false),
                _ => Choice(control, what + " stepper", stepper: true),
            });
        }

        private ModuleStatusBlock Status(JsonElement status, string what)
        {
            Object(status, what, "text", "tone", "button");

            var button = status.TryGetProperty("button", out var tile) ? TileButton(tile, what + " button") : null;

            return new ModuleStatusBlock(Text(status, "text", what)!, Tone(status, what), button);
        }

        private ModuleChoice Choice(JsonElement choice, string what, bool stepper)
        {
            Object(choice, what, "tool", "argument", "options", "selected");

            var sends = Sends(choice, what);
            var options = Array(choice, "options", what, 2, stepper ? MaxStepperOptions : 4);
            var parsed = options.Select((option, index) =>
            {
                var where = $"{what} option {index + 1}";
                Object(option, where, "label", "value");
                return new ModuleOption(Text(option, "label", where)!, Scalar(option, "value", where));
            }).ToList();

            if (!choice.TryGetProperty("selected", out var selected))
            {
                throw new PageRefusal($"{what} has no selected value");
            }

            var index = options.FindIndex(option => JsonElement.DeepEquals(option.GetProperty("value"), selected));

            if (index < 0)
            {
                throw new PageRefusal($"{what} selects a value none of its options has");
            }

            return new ModuleChoice(stepper, sends, parsed, index);
        }

        private ModuleCheckboxTiles CheckboxTiles(JsonElement part, string what)
        {
            Object(part, what, "type", "columns", "tiles");

            var columns = 2;

            if (part.TryGetProperty("columns", out var given))
            {
                if (given.ValueKind != JsonValueKind.Number || !given.TryGetInt32(out columns) || columns is not (2 or 4))
                {
                    throw new PageRefusal($"{what} columns must be 2 or 4");
                }
            }

            var tiles = Array(part, "tiles", what, 1, MaxRows).Select((tile, index) =>
            {
                var where = $"{what} tile {index + 1}";
                Object(tile, where, "label", "checked", "tool", "argument");
                return new ModuleCheckboxTile(Text(tile, "label", where)!, Bool(tile, "checked", where), Sends(tile, where));
            });

            return new ModuleCheckboxTiles(columns, [.. tiles]);
        }

        private ModuleList List(JsonElement part, string what)
        {
            Object(part, what, "type", "head", "rows");

            var rows = Array(part, "rows", what, 1, MaxRows).Select((row, index) =>
            {
                var where = $"{what} row {index + 1}";
                Object(row, where, "name", "sub", "aside", "page");
                return new ModuleListRow(
                    Text(row, "name", where)!,
                    Text(row, "sub", where, required: false),
                    Text(row, "aside", where, required: false),
                    Text(row, "page", where, required: false) is { } page ? Page(page, where) : null);
            });

            return new ModuleList(Text(part, "head", what, required: false), [.. rows]);
        }

        private ModuleTileButtons TileButtons(JsonElement part, string what)
        {
            Object(part, what, "type", "buttons");

            var buttons = Array(part, "buttons", what, 1, MaxButtons).Select((button, index) => TileButton(button, $"{what} button {index + 1}"));

            return new ModuleTileButtons([.. buttons]);
        }

        private ModuleTileButton TileButton(JsonElement button, string what)
        {
            Object(button, what, "label", "destructive", "tool", "arguments", "page");

            var label = Text(button, "label", what)!;
            var destructive = Bool(button, "destructive", what);
            var tool = Text(button, "tool", what, required: false);
            var page = Text(button, "page", what, required: false);

            if ((tool is null) == (page is null))
            {
                throw new PageRefusal($"{what} must give either a tool or a page");
            }

            if (page is not null)
            {
                if (button.TryGetProperty("arguments", out _))
                {
                    throw new PageRefusal($"{what} gives arguments to a page");
                }

                return new ModuleTileButton(label, destructive, null, Page(page, what));
            }

            var arguments = "{}";

            if (button.TryGetProperty("arguments", out var given))
            {
                if (given.ValueKind != JsonValueKind.Object)
                {
                    throw new PageRefusal($"{what} arguments must be an object");
                }

                arguments = given.GetRawText();
            }

            return new ModuleTileButton(label, destructive, new ModuleToolCall(Tool(tool!, what), arguments), null);
        }

        private static ModuleTable Table(JsonElement part, string what)
        {
            Object(part, what, "type", "columns", "rows");

            var columns = Array(part, "columns", what, 1, MaxColumns).Select((column, index) =>
            {
                var where = $"{what} column {index + 1}";
                Object(column, where, "label", "numeric");
                return new ModuleColumn(Text(column, "label", where)!, Bool(column, "numeric", where));
            }).ToList();

            var rows = Array(part, "rows", what, 0, MaxRows).Select((row, index) =>
            {
                var where = $"{what} row {index + 1}";

                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != columns.Count)
                {
                    throw new PageRefusal($"{where} must be a list of {columns.Count} cells");
                }

                return (IReadOnlyList<ModuleCell>)[.. row.EnumerateArray().Select((cell, column) => Cell(cell, $"{where} cell {column + 1}"))];
            });

            return new ModuleTable(columns, [.. rows]);
        }

        private static ModuleCell Cell(JsonElement cell, string what)
        {
            switch (cell.ValueKind)
            {
                case JsonValueKind.String:
                    return new ModuleCell(Bounded(cell.GetString()!, what), null);

                case JsonValueKind.Number:
                    return new ModuleCell(cell.GetRawText(), null);

                case JsonValueKind.Object:
                    Object(cell, what, "text", "tone");
                    return new ModuleCell(Text(cell, "text", what, required: false) ?? string.Empty, Tone(cell, what));

                default:
                    throw new PageRefusal($"{what} must be text, a number or {{text, tone}}");
            }
        }

        private static ModuleMessage Message(JsonElement part, string what)
        {
            Object(part, what, "type", "text", "at");

            DateTimeOffset? at = null;

            if (Text(part, "at", what, required: false) is { } given)
            {
                at = DateTimeOffset.TryParse(given, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : throw new PageRefusal($"{what} at is not a date and time");
            }

            return new ModuleMessage(Text(part, "text", what)!, at);
        }

        private ModuleSends Sends(JsonElement element, string what)
        {
            var tool = Text(element, "tool", what)!;
            var argument = Text(element, "argument", what)!;

            if (argument.Length > 64)
            {
                throw new PageRefusal($"{what} argument is longer than 64 characters");
            }

            return new ModuleSends(Tool(tool, what), argument);
        }

        private string Tool(string tool, string what) =>
            tools.Contains(tool) ? tool : throw new PageRefusal($"{what} names the tool \"{tool}\", which is not one of this module's tools");

        private string Page(string page, string what) =>
            pages.Contains(page) ? page : throw new PageRefusal($"{what} opens \"{page}\", which is not one of this module's pages");

        /// <summary>Refuses anything but an object whose properties are all in <paramref name="allowed"/>.</summary>
        private static void Object(JsonElement element, string what, params string[] allowed)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new PageRefusal($"{what} is not an object");
            }

            foreach (var property in element.EnumerateObject())
            {
                if (Presentation.Contains(property.Name))
                {
                    throw new PageRefusal($"{what} gives a colour, font, image or size (\"{property.Name}\"), which a module page may not");
                }

                if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                {
                    throw new PageRefusal($"{what} has an unknown property \"{property.Name}\"");
                }
            }
        }

        private static List<JsonElement> Array(JsonElement element, string name, string what, int least, int most)
        {
            if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                throw new PageRefusal($"{what} has no list \"{name}\"");
            }

            var count = array.GetArrayLength();

            if (count < least || count > most)
            {
                throw new PageRefusal($"{what} {name} must hold {least} to {most} entries; it holds {count}");
            }

            return [.. array.EnumerateArray()];
        }

        private static string? Text(JsonElement element, string name, string what, bool required = true)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return required ? throw new PageRefusal($"{what} has no {name}") : null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new PageRefusal($"{what} {name} is not text");
            }

            var text = value.GetString()!;

            if (required && string.IsNullOrWhiteSpace(text))
            {
                throw new PageRefusal($"{what} {name} is empty");
            }

            return Bounded(text, what);
        }

        private static string Bounded(string text, string what) =>
            text.Length <= MaxText ? text : throw new PageRefusal($"{what} has text longer than {MaxText} characters");

        private static bool Bool(JsonElement element, string name, string what) =>
            !element.TryGetProperty(name, out var value)
                ? false
                : value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new PageRefusal($"{what} {name} is not true or false"),
                };

        private static string Scalar(JsonElement element, string name, string what) =>
            element.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? value.GetRawText()
                : throw new PageRefusal($"{what} {name} must be text, a number, or true or false");

        private static ModuleTone? Tone(JsonElement element, string what)
        {
            if (!element.TryGetProperty("tone", out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString() switch
                {
                    "value" => ModuleTone.Value,
                    "name" => ModuleTone.Name,
                    "here" => ModuleTone.Here,
                    "stored" => ModuleTone.Stored,
                    "warning" => ModuleTone.Warning,
                    "error" => ModuleTone.Error,
                    var other => throw new PageRefusal($"{what} has an unknown tone \"{other}\""),
                }
                : throw new PageRefusal($"{what} tone is not text");
        }
    }
}
