using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Sets how a sprite collides: the box mode (automatic, full image or manual), the box itself, the shape and the mask bits.
/// An automatic box is found from the pixels above the tolerance. The file stores the mode, the four margins, the separation type and the mask bytes.
/// One undo step restores all of them.
/// </summary>
public sealed class SpriteMaskCommand(string sprite, MaskMode mode, MaskShape shape, MaskBox box, int tolerance = 0) : EditCommand
{
    public const string Name = "spriteMask";

    private (uint Mode, GameMakerSprite.SepMaskType Sep, MaskBox Box, GameMakerSprite.MaskEntry[] Masks)? _old;

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["mode"] = ((int)mode).ToString(CultureInfo.InvariantCulture),
        ["shape"] = shape.ToString(),
        ["box"] = string.Join(',', box.Left, box.Top, box.Right, box.Bottom),
        ["tolerance"] = tolerance.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sprites", sprite)];

    public static SpriteMaskCommand FromArgs(IReadOnlyDictionary<string, string> a)
    {
        var n = a["box"].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        return new(a["sprite"], (MaskMode)int.Parse(a["mode"], CultureInfo.InvariantCulture), Enum.Parse<MaskShape>(a["shape"]), new MaskBox(n[0], n[1], n[2], n[3]), int.Parse(a["tolerance"], CultureInfo.InvariantCulture));
    }

    /// <summary>The box a sprite has now, as the margins in its file.</summary>
    public static MaskBox BoxOf(GameMakerSprite s) => new(s.MarginLeft, s.MarginTop, s.MarginRight, s.MarginBottom);

    public static MaskShape ShapeOf(GameMakerSprite s) => s.SepMasks switch
    {
        GameMakerSprite.SepMaskType.RotatedRect => MaskShape.Rotated,
        GameMakerSprite.SepMaskType.Precise => s.CollisionMasks.Count > 1 ? MaskShape.PrecisePerFrame : MaskShape.Precise,
        _ => MaskShape.Rectangle,
    };

    public override void Apply(GameMakerData data)
    {
        var target = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        _old = (target.BBoxMode, target.SepMasks, BoxOf(target), [.. target.CollisionMasks]);
        int width = (int)target.Width, height = (int)target.Height;
        var solids = SpriteSolids.Of(target, tolerance);
        var chosen = mode switch
        {
            MaskMode.Automatic => SpriteMask.Auto(solids, width, height),
            MaskMode.Full => SpriteMask.Full(width, height),
            _ => SpriteMask.Clamp(box, width, height),
        };
        target.BBoxMode = (uint)mode;
        (target.MarginLeft, target.MarginTop, target.MarginRight, target.MarginBottom) = (chosen.Left, chosen.Top, chosen.Right, chosen.Bottom);
        target.SepMasks = shape switch
        {
            MaskShape.Rectangle => GameMakerSprite.SepMaskType.AxisAlignedRect,
            MaskShape.Rotated => GameMakerSprite.SepMaskType.RotatedRect,
            _ => GameMakerSprite.SepMaskType.Precise,
        };
        // From 2024.6 the mask covers only the box; before that it covers the whole picture.
        var area = data.IsVersionAtLeast(2024, 6) ? chosen : SpriteMask.Full(width, height);
        var count = shape == MaskShape.PrecisePerFrame ? Math.Max(1, solids.Count) : 1;
        target.CollisionMasks.Clear();
        for (var i = 0; i < count; i++)
        {
            var solid = solids.Count == 0 ? (_, _) => false : solids[Math.Min(i, solids.Count - 1)];
            target.CollisionMasks.Add(new GameMakerSprite.MaskEntry(SpriteMask.Bits(shape, solid, chosen, area), area.Width, area.Height));
        }
    }

    public override void Revert(GameMakerData data)
    {
        var target = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        if (_old is not { } old) throw new InvalidOperationException("The edit was never applied.");
        target.BBoxMode = old.Mode;
        target.SepMasks = old.Sep;
        (target.MarginLeft, target.MarginTop, target.MarginRight, target.MarginBottom) = (old.Box.Left, old.Box.Top, old.Box.Right, old.Box.Bottom);
        target.CollisionMasks.Clear();
        foreach (var mask in old.Masks) target.CollisionMasks.Add(mask);
    }
}

/// <summary>Which pixels of a sprite count as solid.</summary>
public static class SpriteSolids
{
    /// <summary>A function per frame that tells whether a pixel of the sprite is solid (alpha above the tolerance).</summary>
    public static List<Func<int, int, bool>> Of(GameMakerSprite target, int tolerance)
    {
        var raw = new Dictionary<GameMakerEmbeddedTexture, GMImage?>();
        var result = new List<Func<int, int, bool>>();
        foreach (var entry in target.Textures)
        {
            var frame = entry.Texture;
            GMImage? page = null;
            if (frame?.TexturePage is { } texture && !raw.TryGetValue(texture, out page))
                raw[texture] = page = texture.TextureData?.Image is { Width: > 1 } image ? image.ConvertToRawBgra() : null;
            var pixels = page?.GetData();
            result.Add((x, y) =>
            {
                if (frame is null || page is null || pixels is null) return false;
                int fx = x - frame.TargetX, fy = y - frame.TargetY;
                if (fx < 0 || fy < 0 || fx >= frame.SourceWidth || fy >= frame.SourceHeight) return false;
                return pixels[((((fy + frame.SourceY) * page.Width) + fx + frame.SourceX) * 4) + 3] > tolerance;
            });
        }

        return result;
    }
}
