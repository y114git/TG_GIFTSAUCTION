using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Moves the origin of a sprite (both coordinates in one undo step), keeping the origin of a sequence-backed sprite in line.</summary>
public sealed class SpriteOriginCommand(string sprite, int x, int y) : EditCommand
{
    public const string Name = "spriteOrigin";

    private (int X, int Y)? _old;

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["x"] = x.ToString(CultureInfo.InvariantCulture),
        ["y"] = y.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sprites", sprite)];

    public override bool ChangesNames => false;

    public static SpriteOriginCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["sprite"], int.Parse(a["x"], CultureInfo.InvariantCulture), int.Parse(a["y"], CultureInfo.InvariantCulture));

    private static GameMakerSprite Find(GameMakerData data, string name) => data.Sprites.FirstOrDefault(s => s.Name.Content == name) ?? throw new InvalidOperationException($"Sprite '{name}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data, sprite);
        _old = (target.OriginX, target.OriginY);
        (target.OriginXWrapper, target.OriginYWrapper) = (x, y);
    }

    public override void Revert(GameMakerData data)
    {
        var target = Find(data, sprite);
        if (_old is not { } old) throw new InvalidOperationException("The edit was never applied.");
        (target.OriginXWrapper, target.OriginYWrapper) = old;
    }
}
