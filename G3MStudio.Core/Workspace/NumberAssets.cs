using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>A number literal of GML: its value, whether it was written in hex ($BBGGRR or 0xBBGGRR), and how many hex digits it has.</summary>
public readonly record struct NumberLiteral(long Value, bool Hex, int Digits);

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

    /// <summary>The whole number literal under an offset of the text, or null when the offset is not on one.</summary>
    public static NumberLiteral? LiteralAt(string text, int offset)
    {
        if (offset < 0 || offset > text.Length) return null;
        static bool Word(char c) => char.IsAsciiLetterOrDigit(c) || c == '$';
        int start = offset, end = offset;
        while (start > 0 && Word(text[start - 1])) start--;
        while (end < text.Length && Word(text[end])) end++;
        // A name (sprite2), a member (a_1) and the parts of a real (1.5) are not asset numbers.
        if (end == start || start > 0 && text[start - 1] is '_' or '.' || end < text.Length && text[end] is '_' or '.' || char.IsLetter(text[start]) && text[start] != '$') return null;
        var word = text.AsSpan(start, end - start);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var isHex = word[0] == '$' || word.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (isHex)
        {
            var digits = word[0] == '$' ? word[1..] : word[2..];
            // ulong, so a 16-digit literal above long.MaxValue is refused instead of wrapping to a negative number.
            return !digits.IsEmpty && ulong.TryParse(digits, System.Globalization.NumberStyles.AllowHexSpecifier, culture, out var h) && h <= long.MaxValue ? new NumberLiteral((long)h, true, digits.Length) : null;
        }

        return long.TryParse(word, System.Globalization.NumberStyles.None, culture, out var value) ? new NumberLiteral(value, false, 0) : null;
    }

    /// <summary>The whole number under an offset of the text, or null when the offset is not on one that fits an index.</summary>
    public static int? NumberAt(string text, int offset) => LiteralAt(text, offset) is { Value: <= int.MaxValue } literal ? (int)literal.Value : null;
}
