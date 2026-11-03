using System.Globalization;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>Replaces one collision mask of a sprite with the bytes made from a picture; the old bytes come back on undo.</summary>
public sealed class ImportMaskCommand : EditCommand
{
    public const string Name = "importMask";

    private readonly string _sprite;
    private readonly int _index;
    private readonly byte[] _mask;
    private byte[] _old = [];

    public ImportMaskCommand(string sprite, int index, byte[] mask) => (_sprite, _index, _mask) = (sprite, index, mask);

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["sprite"] = _sprite,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["mask"] = Convert.ToBase64String(_mask),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sprites", _sprite)];

    public static ImportMaskCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sprite"], int.Parse(a["index"], CultureInfo.InvariantCulture), Convert.FromBase64String(a["mask"]));

    private G3MLib.DataFile.Models.GameMakerSprite.MaskEntry Entry(GameMakerData data)
    {
        var sprite = data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite) ?? throw new InvalidOperationException($"Sprite '{_sprite}' does not exist.");
        return _index >= 0 && _index < sprite.CollisionMasks.Count ? sprite.CollisionMasks[_index] : throw new InvalidOperationException($"Sprite '{_sprite}' has no mask {_index}.");
    }

    public override void Apply(GameMakerData data)
    {
        var entry = Entry(data);
        if (_mask.Length != MaskPicture.Stride(entry.Width) * entry.Height) throw new InvalidDataException("The mask does not fit the mask size of the sprite.");
        _old = entry.Data;
        entry.Data = _mask;
    }

    public override void Revert(GameMakerData data) => Entry(data).Data = _old;
}
