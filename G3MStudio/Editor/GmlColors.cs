using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>Colour literals of GML for the swatch drawn before them: $BBGGRR and 0xBBGGRR (GameMaker stores blue first), #RRGGBB (the CSS form of the decompiler setting) and the c_ constants.</summary>
public static class GmlColors
{
    private static readonly Dictionary<string, int> Named = new(StringComparer.Ordinal)
    {
        ["c_aqua"] = 16776960,
        ["c_black"] = 0,
        ["c_blue"] = 16711680,
        ["c_dkgray"] = 4210752,
        ["c_fuchsia"] = 16711935,
        ["c_gray"] = 8421504,
        ["c_green"] = 32768,
        ["c_lime"] = 65280,
        ["c_ltgray"] = 12632256,
        ["c_maroon"] = 128,
        ["c_navy"] = 8388608,
        ["c_olive"] = 32896,
        ["c_orange"] = 4235519,
        ["c_purple"] = 8388736,
        ["c_red"] = 255,
        ["c_silver"] = 12632256,
        ["c_teal"] = 8421376,
        ["c_white"] = 16777215,
        ["c_yellow"] = 65535,
    };

    /// <summary>The colour a token stands for as red, green and blue, or null when it is not a colour literal.</summary>
    public static (byte R, byte G, byte B)? Parse(string token, GmlTokenKind kind)
    {
        if (kind == GmlTokenKind.Identifier) return Named.TryGetValue(token, out var value) ? FromBgr(value) : null;
        if (kind == GmlTokenKind.Preprocessor && token.Length == 7 && token[0] == '#' && int.TryParse(token.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var css))
            return ((byte)(css >> 16), (byte)(css >> 8), (byte)css);
        if (kind != GmlTokenKind.Number) return null;
        var digits = token.StartsWith('$') ? token[1..] : token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : null;
        // Six digits is a colour; shorter hex numbers are ordinary values (flags, masks).
        return digits is { Length: 6 } && int.TryParse(digits, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var bgr) ? FromBgr(bgr) : null;
    }

    private static (byte, byte, byte) FromBgr(int bgr) => ((byte)bgr, (byte)(bgr >> 8), (byte)(bgr >> 16));

    /// <summary>The GML number of a colour, as it shows in a hover.</summary>
    public static string Describe((byte R, byte G, byte B) color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}  ${color.B:X2}{color.G:X2}{color.R:X2}";
}
