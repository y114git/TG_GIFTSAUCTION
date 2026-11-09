using System.Text.RegularExpressions;

namespace G3MStudio.Core.Settings;

/// <summary>
/// The colors a user can change. Each has one hex value (#RRGGBB) that stands for a few theme tokens: the accent also sets the border and a lighter
/// accent text, the keyword color also colors true and false.
/// </summary>
public static partial class ThemeColors
{
    public const string Accent = "Accent";

    /// <summary>The colors the user can change, by category (the key of the category heading and its settings keys), in the order the Settings page lists them.</summary>
    public static readonly (string Group, string[] Keys)[] Groups =
    [
        ("colors.group.interface", [Accent, "Border.Subtle", "Hover", "Pressed", "Select", "Selection.Bg"]),
        ("colors.group.surfaces", ["Bg.Window", "Bg.Surface", "Bg.Sunken", "Bg.Raised"]),
        ("colors.group.text", ["Text.Main", "Text.Muted", "Text.OnAccent"]),
        ("colors.group.status", ["Danger", "Warning", "Success", "Info"]),
        ("colors.group.canvas", ["Canvas.Bg", "Canvas.Stage", "Canvas.Line", "Canvas.Point", "Canvas.Guide", "Canvas.Handle", "Canvas.Hover", "Canvas.Muted"]),
        ("colors.group.diff", ["Diff.Added", "Diff.Removed", "Diff.Changed"]),
        ("colors.group.code", ["Syntax.Keyword", "Syntax.Function", "Syntax.Variable", "Syntax.Local", "Syntax.Global", "Syntax.Constant", "Syntax.Asset", "Syntax.String", "Syntax.Number", "Syntax.Comment", "Syntax.Operator", "Syntax.Preprocessor", "Syntax.Bracket1", "Syntax.Bracket2", "Syntax.Bracket3"]),
    ];

    /// <summary>Every settings key of <see cref="Groups"/>.</summary>
    public static readonly string[] Editable = [.. Groups.SelectMany(g => g.Keys)];

    public static bool IsValid(string? hex) => hex is not null && HexColor().IsMatch(hex);

    /// <summary>The theme tokens that take this color, with the value each gets.</summary>
    public static IEnumerable<(string Token, string Hex)> Expand(string key, string hex) => key switch
    {
        Accent => [("Accent.Fill", hex), ("Border", hex), ("Text.Accent", Lighten(hex))],
        "Syntax.Keyword" => [("Syntax.Keyword", hex), ("Syntax.Boolean", hex)],
        "Text.Main" => [("Text", hex)], // the key is not "Text" so that the language file can hold "Text.Muted" beside it
        _ => [(key, hex)],
    };

    /// <summary>
    /// The colors of the Fluent control theme that must follow a key, as #AARRGGBB. The controls (selected rows, check boxes, text boxes, menus) read these
    /// "System" colors, not our tokens, so changing only a token would leave them in the old color.
    /// </summary>
    public static IEnumerable<(string Key, string Argb)> FluentColors(string key, string hex)
    {
        static string Alpha(string hex, int a) => $"#{a:X2}{hex[1..]}";
        switch (key)
        {
            case Accent:
                yield return ("SystemAccentColor", Alpha(hex, 255));
                for (var i = 1; i <= 3; i++)
                {
                    yield return ($"SystemAccentColorLight{i}", Alpha(Mix(hex, "#FFFFFF", 0.2 * i), 255));
                    yield return ($"SystemAccentColorDark{i}", Alpha(Mix(hex, "#000000", 0.15 * i), 255));
                }

                break;
            case "Bg.Window":
                yield return ("SystemRegionColor", Alpha(hex, 255));
                break;
            case "Bg.Surface":
                foreach (var name in new[] { "SystemAltHighColor", "SystemChromeLowColor", "SystemChromeAltLowColor" }) yield return (name, Alpha(hex, 255));
                yield return ("SystemAltMediumHighColor", Alpha(hex, 0xCC));
                yield return ("SystemAltMediumColor", Alpha(hex, 0x99));
                yield return ("SystemAltMediumLowColor", Alpha(hex, 0x66));
                yield return ("SystemAltLowColor", Alpha(hex, 0x33));
                break;
            case "Bg.Sunken":
                yield return ("SystemChromeBlackLowColor", Alpha(hex, 255));
                break;
            case "Bg.Raised":
                yield return ("SystemChromeMediumLowColor", Alpha(hex, 255));
                break;
            case "Border.Subtle":
                yield return ("SystemChromeMediumColor", Alpha(hex, 255));
                yield return ("SystemListLowColor", Alpha(hex, 255));
                break;
            case "Hover":
                yield return ("SystemChromeHighColor", Alpha(hex, 255));
                break;
            case "Pressed":
                yield return ("SystemListMediumColor", Alpha(hex, 255));
                break;
            case "Text.Main":
                yield return ("SystemBaseHighColor", Alpha(hex, 255));
                yield return ("SystemBaseMediumHighColor", Alpha(hex, 0xCC));
                break;
            case "Text.Muted":
                yield return ("SystemBaseMediumColor", Alpha(hex, 255));
                yield return ("SystemChromeGrayColor", Alpha(hex, 255));
                yield return ("SystemBaseMediumLowColor", Alpha(hex, 0x66));
                yield return ("SystemBaseLowColor", Alpha(hex, 0x33));
                break;
        }
    }

    /// <summary>Every Fluent color key a key can set, so a reset can take them away again.</summary>
    public static IEnumerable<string> FluentKeys(string key) => FluentColors(key, "#000000").Select(c => c.Key);

    /// <summary>The color mixed towards another by a share (0 is the first color, 1 the second).</summary>
    public static string Mix(string hex, string towards, double share)
    {
        static int Channel(string hex, int at) => Convert.ToInt32(hex.Substring(at, 2), 16);
        static string Part(string a, string b, int at, double share) => ((int)Math.Round(Channel(a, at) + (Channel(b, at) - Channel(a, at)) * share)).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
        return "#" + Part(hex, towards, 1, share) + Part(hex, towards, 3, share) + Part(hex, towards, 5, share);
    }

    /// <summary>Every token a key can set, so an override can be taken away again.</summary>
    public static IEnumerable<string> Tokens(string key) => Expand(key, "#000000").Select(t => t.Token);

    /// <summary>The color mixed 45% towards white, for text that sits on the dark background.</summary>
    public static string Lighten(string hex) => Mix(hex, "#FFFFFF", 0.45);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}

/// <summary>A theme as a file: the colors of <see cref="ThemeColors.Editable"/> as JSON (<c>{"colors": {"Accent": "#039D5B"}}</c>).</summary>
public static class ThemeFile
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Write(IReadOnlyDictionary<string, string> colors) => System.Text.Json.JsonSerializer.Serialize(new { colors }, Options);

    /// <summary>The theme in a file: a G3MStudio theme or G3M theme as JSON, or a G3M theme zip (theme_config.json at its root or one folder down).</summary>
    public static Dictionary<string, string> ReadFile(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return Read(File.ReadAllText(path));
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals("theme_config.json", StringComparison.OrdinalIgnoreCase))
                ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/theme_config.json", StringComparison.OrdinalIgnoreCase) && e.FullName.Count(c => c == '/') == 1)
                ?? throw new InvalidDataException("The theme file needs theme_config.json.");
            if (entry.Length > 1_000_000) throw new InvalidDataException("The theme file is too large.");
            using var reader = new StreamReader(entry.Open());
            return Read(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The theme file could not be read: " + e.Message, e);
        }
    }

    /// <summary>The valid colors of a theme file. Unknown keys and values that are not #RRGGBB are left out; a file that is not a theme throws <see cref="InvalidDataException"/>.</summary>
    public static Dictionary<string, string> Read(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            // A G3M theme (theme_config.json): its border color is the green outline, which is the accent here. Its other colors paint G3M's own
            // surfaces, which G3MStudio keeps; an empty color means G3M's default, so the accent goes back to the default too.
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && document.RootElement.TryGetProperty("config_version", out _))
            {
                var g3m = new Dictionary<string, string>();
                if (document.RootElement.TryGetProperty("custom_border_color", out var border) && border.ValueKind == System.Text.Json.JsonValueKind.String && border.GetString() is { } hex && ThemeColors.IsValid(hex)) g3m[ThemeColors.Accent] = hex.ToUpperInvariant();
                return g3m;
            }

            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !document.RootElement.TryGetProperty("colors", out var colors) || colors.ValueKind != System.Text.Json.JsonValueKind.Object) throw new InvalidDataException("The file has no \"colors\" object.");
            var result = new Dictionary<string, string>();
            foreach (var property in colors.EnumerateObject())
                if (ThemeColors.Editable.Contains(property.Name) && property.Value.ValueKind == System.Text.Json.JsonValueKind.String && property.Value.GetString() is { } hex && ThemeColors.IsValid(hex)) result[property.Name] = hex.ToUpperInvariant();
            return result;
        }
        catch (System.Text.Json.JsonException e)
        {
            throw new InvalidDataException("The file is not a theme: " + e.Message, e);
        }
    }
}
