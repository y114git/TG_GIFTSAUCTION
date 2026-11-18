namespace G3MStudio.Core.Settings;

/// <summary>The sizes of the theme the user can change (Settings > Appearance > Sizes): key, range and the default (the Comfortable density), in the order the page lists them.</summary>
public static class ThemeSizes
{
    /// <summary>Control.Height starts at 28: the smallest hit target at 100%.</summary>
    public static readonly (string Key, int Min, int Max, int Default)[] All =
    [
        ("Radius.Control", 0, 16, 4),
        ("Radius.Panel", 0, 20, 6),
        ("Border.Width", 1, 4, 2),
        ("FontSize.Body", 10, 20, 13),
        ("FontSize.Editor", 8, 32, 13),
        ("Tooltip.Size", 10, 20, 12),
        ("Control.Height", 28, 56, 30),
        ("Row.Height", 18, 40, 26),
        ("Tab.Height", 24, 48, 32),
        ("Panel.Padding", 4, 24, 12),
        ("Icon.Size", 12, 32, 16),
        ("Scrollbar.Width", 6, 24, 12),
        ("Space.Scale", 50, 200, 100), // percent of the padding of buttons, boxes, lists and menus
    ];

    /// <summary>What each density sets; a key missing here keeps its default. The values of Comfortable are the defaults above.</summary>
    private static readonly Dictionary<string, Dictionary<string, int>> Presets = new()
    {
        [UiSizing.Compact] = new() { ["FontSize.Body"] = 12, ["FontSize.Editor"] = 12, ["Tooltip.Size"] = 11, ["Control.Height"] = 28, ["Row.Height"] = 22, ["Tab.Height"] = 28, ["Panel.Padding"] = 8, ["Icon.Size"] = 14, ["Scrollbar.Width"] = 10, ["Space.Scale"] = 75 },
        [UiSizing.Comfortable] = [],
        [UiSizing.Spacious] = new() { ["FontSize.Body"] = 14, ["FontSize.Editor"] = 14, ["Tooltip.Size"] = 13, ["Control.Height"] = 36, ["Row.Height"] = 32, ["Tab.Height"] = 38, ["Panel.Padding"] = 16, ["Icon.Size"] = 20, ["Scrollbar.Width"] = 16, ["Space.Scale"] = 125 },
    };


    public static int Value(IReadOnlyDictionary<string, int> sizes, string key) => sizes.TryGetValue(key, out var v) ? v : All.First(a => a.Key == key).Default;

    /// <summary>The size in force: the user's own, else the density's, else the default.</summary>
    public static int Value(IReadOnlyDictionary<string, int> sizes, string density, string key) =>
        sizes.TryGetValue(key, out var v) ? v : Presets[UiSizing.NormalDensity(density)].TryGetValue(key, out var p) ? p : All.First(a => a.Key == key).Default;

    /// <summary>True when the size is the theme's own (no override and the density does not move it), so the token can fall back to Tokens.axaml.</summary>
    public static bool IsDefault(IReadOnlyDictionary<string, int> sizes, string density, string key) => Value(sizes, density, key) == All.First(a => a.Key == key).Default;

    /// <summary>The theme tokens a size sets. The body font size moves the other text sizes with it, so the type scale keeps its steps.</summary>
    public static IEnumerable<(string Token, double Value)> Expand(string key, double value) => key == "FontSize.Body"
        ? [("FontSize.Caption", value - 2), ("FontSize.Small", value - 1), ("FontSize.Body", value), ("FontSize.Title", value + 3), ("FontSize.Display", value + 7)]
        : [(key, value)];

    public static Dictionary<string, int> Clamp(IReadOnlyDictionary<string, int>? sizes) =>
        (sizes ?? new Dictionary<string, int>()).Where(p => All.Any(a => a.Key == p.Key)).ToDictionary(p => p.Key, p =>
        {
            var (_, min, max, _) = All.First(x => x.Key == p.Key);
            return Math.Clamp(p.Value, min, max);
        });
}
