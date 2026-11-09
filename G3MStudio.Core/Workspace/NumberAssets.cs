using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>The assets a number in GML can stand for (UTMT-CODE-09): code refers to an asset by its index, so every asset type that has an entry at that index is a candidate.</summary>
public static class NumberAssets
{
    /// <summary>Type key and name of each asset whose index in its list is <paramref name="index"/>.</summary>
    public static IReadOnlyList<(string Type, string Name)> Find(GameMakerData data, int index)
    {
        var found = new List<(string, string)>();
        if (index < 0) return found;
        foreach (var type in ResourceTypes.All.Where(t => UsageSearch.AssetTypes.Contains(t.Key)))
            if ((type.Raw ?? type.Items)(data)?.ElementAtOrDefault(index) is { } item) found.Add((type.Key, ResourceTypes.NameOf(item)));
        return found;
    }

    /// <summary>The whole number under an offset of the text, or null when the offset is not on digits.</summary>
    public static int? NumberAt(string text, int offset)
    {
        if (offset < 0 || offset > text.Length) return null;
        int start = offset, end = offset;
        while (start > 0 && char.IsAsciiDigit(text[start - 1])) start--;
        while (end < text.Length && char.IsAsciiDigit(text[end])) end++;
        // A digit run inside a name (sprite2) or after a dot (1.5) is not an asset number.
        if (end == start || start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] is '_' or '.') || end < text.Length && (char.IsLetter(text[end]) || text[end] is '_' or '.')) return null;
        return int.TryParse(text.AsSpan(start, end - start), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
