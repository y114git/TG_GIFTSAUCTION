using System.Globalization;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Reads a character or a range of characters typed by a person: a literal (any script, emoji included), U+0041, 0x41 or a decimal number,
/// and ranges such as A-Z, a..z or U+0410-U+044F. Results are Unicode code points; a font glyph holds only 0 to 0xFFFF.
/// </summary>
public static class CharacterInput
{
    public const int MaxCode = 0x10FFFF;
    public const int FontMax = 0xFFFF;
    public const int DefaultRangeLimit = 2048;

    private static bool Valid(int code) => code is >= 0 and <= MaxCode and not (>= 0xD800 and <= 0xDFFF);

    /// <summary>The code point a single character stands for, or null. One character is taken literally ("9" is the digit); longer text must be a code or a number.</summary>
    public static int? ParseCharacter(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        if (text.StartsWith("U+", StringComparison.OrdinalIgnoreCase) || text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) && Valid(hex) ? hex : null;
        if (Rune.TryGetRuneAt(text, 0, out var rune) && rune.Utf16SequenceLength == text.Length) return rune.Value;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && Valid(number) ? number : null;
    }

    /// <summary>The code points of a range (<c>A-Z</c>, <c>a..z</c>, <c>U+0410-U+044F</c>) or the one a single character stands for; null when the text is neither or the range is longer than <paramref name="limit"/>.</summary>
    public static IReadOnlyList<int>? ParseRange(string text, int limit = DefaultRangeLimit)
    {
        text = text.Trim();
        if (ParseCharacter(text) is { } one) return [one];
        if (Ends(text) is not var (from, to) || to - from >= limit) return null;
        return [.. Enumerable.Range(from, to - from + 1).Where(Valid)];
    }

    /// <summary>True when the text is a well-formed range that is only refused for being longer than <paramref name="limit"/>.</summary>
    public static bool IsTooLong(string text, int limit = DefaultRangeLimit) => Ends(text.Trim()) is var (from, to) && to - from >= limit;

    private static (int From, int To)? Ends(string text)
    {
        if (text.Length < 3) return null;
        var dots = text.IndexOf("..", StringComparison.Ordinal);
        var (cut, width) = dots >= 0 ? (dots, 2) : (text.IndexOfAny(['-', '–'], 1), 1);
        if (cut < 0 || ParseCharacter(text[..cut]) is not { } from || ParseCharacter(text[(cut + width)..]) is not { } to || from > to) return null;
        return (from, to);
    }

    /// <summary>The character as text for a label: empty for a control character.</summary>
    public static string Display(int code) => Valid(code) && !(code <= FontMax && char.IsControl((char)code)) ? char.ConvertFromUtf32(code) : "";

    /// <summary>The character with its code and decimal number, such as <c>Z  U+005A  90</c>.</summary>
    public static string Describe(int code) => string.Create(CultureInfo.InvariantCulture, $"{Display(code)}  U+{code:X4}  {code}");

    /// <summary>The distinct non-control characters of a text, in order of first use, as code points (a surrogate pair counts once).</summary>
    public static IReadOnlyList<int> DistinctCharacters(string text) => [.. text.EnumerateRunes().Where(r => !Rune.IsControl(r)).Select(r => r.Value).Distinct()];

    private static IEnumerable<int> Span(int from, int to) => Enumerable.Range(from, to - from + 1);

    /// <summary>The characters of a preset button; unknown names give the printable ASCII set. The big scripts (CJK, Hangul) are a starting set, "Add from text" covers the rest.</summary>
    public static IReadOnlyList<int> Preset(string name) => name switch
    {
        "Digits" => [.. Span('0', '9')],
        "Letters" => [.. Span('A', 'Z'), .. Span('a', 'z')],
        "Cyrillic" => [.. Span(0x400, 0x4FF)],
        "Greek" => [.. Span(0x370, 0x3FF)],
        "LatinExtended" => [.. Span(0xA0, 0x24F)],
        "Hiragana" => [.. Span(0x3040, 0x309F)],
        "Katakana" => [.. Span(0x30A0, 0x30FF)],
        "Cjk" => [.. Span(0x3000, 0x303F), .. Span(0xFF00, 0xFFEF), .. Span(0x4E00, 0x51FF)],
        "Hangul" => [.. Span(0x3131, 0x3163), .. Span(0xAC00, 0xB1FF)],
        "Symbols" => [.. Span(0x2000, 0x206F), .. Span(0x20A0, 0x20CF), .. Span(0x2190, 0x21FF), .. Span(0x2500, 0x25FF)],
        _ => [.. Span(32, 126)],
    };

    // Button order.
    public static readonly string[] PresetNames = ["Normal", "Digits", "Letters", "Cyrillic", "Greek", "LatinExtended", "Hiragana", "Katakana", "Cjk", "Hangul", "Symbols"];
}
