using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Sets the shift of a kerning pair of one glyph (the glyph's character and the character after it), adds the pair when it is new, or removes it (UTMT-FNT-04).
/// Pairs are found by the next character, not by position, so the command stays right when other edits move the list.
/// </summary>
public sealed class FontKerningCommand(string font, int glyph, string operation, short character, short shift = 0) : EditCommand
{
    public const string Name = "fontKerning";
    public const string Set = "set";
    public const string Remove = "remove";

    private short? _before;
    private int _at = -1;

    public override string Kind => Name;

    public override string Editor => "fonts";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["font"] = font,
        ["glyph"] = glyph.ToString(CultureInfo.InvariantCulture),
        ["operation"] = operation,
        ["character"] = character.ToString(CultureInfo.InvariantCulture),
        ["shift"] = shift.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Fonts", font)];

    public static FontKerningCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["font"], int.Parse(a["glyph"], CultureInfo.InvariantCulture), a["operation"], short.Parse(a["character"], CultureInfo.InvariantCulture), short.Parse(a["shift"], CultureInfo.InvariantCulture));

    private GameMakerFont.Glyph.GlyphKerning? Pair(GameMakerData data, out IList<GameMakerFont.Glyph.GlyphKerning> list)
    {
        var found = data.Fonts.FirstOrDefault(f => f.Name.Content == font) ?? throw new InvalidOperationException($"Font '{font}' does not exist.");
        if (glyph < 0 || glyph >= found.Glyphs.Count) throw new InvalidOperationException($"Font '{font}' has no glyph {glyph}.");
        list = found.Glyphs[glyph].Kerning;
        return list.FirstOrDefault(k => k.Character == character);
    }

    public override void Apply(GameMakerData data)
    {
        var pair = Pair(data, out var list);
        (_before, _at) = (pair?.ShiftModifier, pair is null ? -1 : list.IndexOf(pair));
        if (operation == Remove)
        {
            if (pair is null) throw new InvalidOperationException($"The glyph has no pair with character {character}.");
            list.Remove(pair);
        }
        else if (operation == Set)
        {
            if (pair is null) list.Add(new GameMakerFont.Glyph.GlyphKerning { Character = character, ShiftModifier = shift });
            else pair.ShiftModifier = shift;
        }
        else throw new InvalidDataException($"Unknown kerning operation '{operation}'.");
    }

    public override void Revert(GameMakerData data)
    {
        var pair = Pair(data, out var list);
        if (operation == Remove)
        {
            list.Insert(Math.Min(_at, list.Count), new GameMakerFont.Glyph.GlyphKerning { Character = character, ShiftModifier = _before ?? 0 });
        }
        else if (_before is { } old && pair is not null) pair.ShiftModifier = old;
        else if (pair is not null) list.Remove(pair);
    }
}
