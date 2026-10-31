using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds a glyph for a character to a font, or removes one. The game looks glyphs up by character in a sorted list, so an added glyph is inserted
/// at its sorted place; it starts empty (size 0), and its position on the sheet is set afterwards in the glyph's property list.
/// </summary>
#pragma warning disable CA1001 // the glyph belongs to the font or is put back into it; the model disposes it
public sealed class FontGlyphCommand : EditCommand
{
    public const string Name = "fontGlyph";
    public const string Add = "add";
    public const string Remove = "remove";

    private readonly string _font;
    private readonly string _operation;
    private readonly int _index;
    private readonly ushort _character;
    private GameMakerFont.Glyph? _glyph;
    private int _at;

    // index: the glyph to remove. character: the character of a new glyph.
    public FontGlyphCommand(string font, string operation, int index, ushort character = 0) => (_font, _operation, _index, _character) = (font, operation, index, character);

    public override string Kind => Name;

    public override string Editor => "fonts";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["font"] = _font,
        ["operation"] = _operation,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["character"] = _character.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Fonts", _font)];

    public static FontGlyphCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["font"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture), ushort.Parse(a["character"], CultureInfo.InvariantCulture));

    private GameMakerFont Find(GameMakerData data) => data.Fonts.FirstOrDefault(f => f.Name.Content == _font) ?? throw new InvalidOperationException($"Font '{_font}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var glyphs = Find(data).Glyphs;
        if (_operation == Add)
        {
            if (glyphs.Any(g => g.Character == _character)) throw new InvalidOperationException($"The font already has a glyph for U+{_character:X4}.");
            _glyph = new GameMakerFont.Glyph { Character = _character };
            _at = glyphs.TakeWhile(g => g.Character < _character).Count();
            glyphs.Insert(_at, _glyph);
            return;
        }

        if (_operation != Remove) throw new InvalidDataException($"Unknown font operation '{_operation}'.");
        if (_index < 0 || _index >= glyphs.Count) throw new InvalidOperationException($"Font '{_font}' has no glyph {_index}.");
        (_glyph, _at) = (glyphs[_index], _index);
        glyphs.RemoveAt(_index);
    }

    public override void Revert(GameMakerData data)
    {
        var glyphs = Find(data).Glyphs;
        if (_glyph is null) throw new InvalidOperationException("The edit was never applied.");
        if (_operation == Add) glyphs.Remove(_glyph);
        else glyphs.Insert(_at, _glyph);
    }
}
#pragma warning restore CA1001
