namespace G3MStudio.Core.Settings;

/// <summary>The sizes of the theme the user can change (Settings > Appearance > Shape and size): key, range and the default, in the order the page lists them.</summary>
public static class ThemeSizes
{
    public static readonly (string Key, int Min, int Max, int Default)[] All =
    [
        ("Radius.Control", 0, 16, 4),
        ("Radius.Panel", 0, 20, 6),
        ("Border.Width", 1, 4, 2),
        ("FontSize.Body", 10, 20, 13),
        ("Row.Height", 18, 40, 24),
        ("Space.Scale", 50, 200, 100), // percent of the padding of buttons, boxes, lists and menus
    ];

    public static int Value(IReadOnlyDictionary<string, int> sizes, string key) => sizes.TryGetValue(key, out var v) ? v : All.First(a => a.Key == key).Default;

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
