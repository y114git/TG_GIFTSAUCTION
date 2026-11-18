namespace G3MStudio.Core.Workspace;

/// <summary>
/// Named dock layouts: the built-in presets and the ones the user saved (name to layout JSON, kept in the settings).
/// Every operation returns a new dictionary, so the settings store can swap it in one update.
/// </summary>
public static class LayoutBook
{
    /// <summary>The built-in presets in menu order. The id is also the language key suffix (<c>layout.preset.&lt;id&gt;</c>).</summary>
    public static readonly string[] BuiltIn = ["studio", "wide", "code", "canvas", "minimal"];

    public static bool IsBuiltIn(string name) => BuiltIn.Contains(name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Names(IReadOnlyDictionary<string, string> saved) => [.. saved.Keys.Order(StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>A name the user may give: not blank, not a built-in id, at most 60 characters.</summary>
    public static string? Clean(string? name)
    {
        var clean = name?.Trim() ?? "";
        return clean.Length is > 0 and <= 60 && !IsBuiltIn(clean) ? clean : null;
    }

    public static IReadOnlyDictionary<string, string> Save(IReadOnlyDictionary<string, string> saved, string name, string json) =>
        Clean(name) is { } clean ? new Dictionary<string, string>(saved) { [clean] = json } : saved;

    public static IReadOnlyDictionary<string, string> Delete(IReadOnlyDictionary<string, string> saved, string name) =>
        saved.Where(p => p.Key != name).ToDictionary();

    /// <summary>Gives a layout another name; unchanged when the old one is missing, the new one is not allowed or already taken.</summary>
    public static IReadOnlyDictionary<string, string> Rename(IReadOnlyDictionary<string, string> saved, string from, string to)
    {
        if (!saved.TryGetValue(from, out var json) || Clean(to) is not { } clean || clean != from && saved.ContainsKey(clean)) return saved;
        var copy = saved.Where(p => p.Key != from).ToDictionary();
        copy[clean] = json;
        return copy;
    }
}
