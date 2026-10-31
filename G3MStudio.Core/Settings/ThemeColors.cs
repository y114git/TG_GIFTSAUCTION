using System.Text.RegularExpressions;

namespace G3MStudio.Core.Settings;

/// <summary>
/// The colors a user can change. Each has one hex value (#RRGGBB) that stands for a few theme tokens: the accent also sets the border and a lighter
/// accent text, the keyword color also colors true and false.
/// </summary>
public static partial class ThemeColors
{
    public const string Accent = "Accent";

    /// <summary>The settings keys, in the order the Settings page lists them.</summary>
    public static readonly string[] Editable =
    [
        Accent, "Syntax.Keyword", "Syntax.Function", "Syntax.Variable", "Syntax.Constant", "Syntax.Asset", "Syntax.String", "Syntax.Number", "Syntax.Comment", "Syntax.Operator", "Syntax.Preprocessor",
    ];

    public static bool IsValid(string? hex) => hex is not null && HexColor().IsMatch(hex);

    /// <summary>The theme tokens that take this color, with the value each gets.</summary>
    public static IEnumerable<(string Token, string Hex)> Expand(string key, string hex) => key switch
    {
        Accent => [("Accent.Fill", hex), ("Border", hex), ("Text.Accent", Lighten(hex))],
        "Syntax.Keyword" => [("Syntax.Keyword", hex), ("Syntax.Boolean", hex)],
        _ => [(key, hex)],
    };

    /// <summary>Every token a key can set, so an override can be taken away again.</summary>
    public static IEnumerable<string> Tokens(string key) => Expand(key, "#000000").Select(t => t.Token);

    /// <summary>The color mixed 45% towards white, for text that sits on the dark background.</summary>
    public static string Lighten(string hex)
    {
        static int Mix(string hex, int at) => (int)Math.Round(Convert.ToInt32(hex.Substring(at, 2), 16) is var c ? c + (255 - c) * 0.45 : 0);
        return $"#{Mix(hex, 1):X2}{Mix(hex, 3):X2}{Mix(hex, 5):X2}";
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}

/// <summary>A theme as a file: the colors of <see cref="ThemeColors.Editable"/> as JSON (<c>{"colors": {"Accent": "#039D5B"}}</c>).</summary>
public static class ThemeFile
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Write(IReadOnlyDictionary<string, string> colors) => System.Text.Json.JsonSerializer.Serialize(new { colors }, Options);

    /// <summary>The valid colors of a theme file. Unknown keys and values that are not #RRGGBB are left out; a file that is not a theme throws <see cref="InvalidDataException"/>.</summary>
    public static Dictionary<string, string> Read(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !document.RootElement.TryGetProperty("colors", out var colors) || colors.ValueKind != System.Text.Json.JsonValueKind.Object) throw new InvalidDataException("The file has no \"colors\" object.");
            var result = new Dictionary<string, string>();
            foreach (var property in colors.EnumerateObject())
                if (ThemeColors.Editable.Contains(property.Name) && property.Value.ValueKind == System.Text.Json.JsonValueKind.String && ThemeColors.IsValid(property.Value.GetString())) result[property.Name] = property.Value.GetString()!.ToUpperInvariant();
            return result;
        }
        catch (System.Text.Json.JsonException e)
        {
            throw new InvalidDataException("The file is not a theme: " + e.Message, e);
        }
    }
}
