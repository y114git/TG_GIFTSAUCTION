using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Rebuilds the collision masks of a sprite from the pictures of its frames: a pixel is solid when its alpha is above the tolerance.
/// A sprite keeps as many masks as it had (one per frame, or one shared mask built from the first frame); the old bytes come back on undo.
/// </summary>
public sealed class RecalcMaskCommand(string sprite, int tolerance = 0) : EditCommand
{
    public const string Name = "recalcMask";

    private byte[]?[] _old = [];

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["tolerance"] = tolerance.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sprites", sprite)];

    public static RecalcMaskCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["sprite"], int.Parse(a["tolerance"], CultureInfo.InvariantCulture));

    /// <summary>The mask bytes for one frame: rows of (width + 7) / 8 bytes, the leftmost pixel in the top bit.</summary>
    public static byte[] BuildMask(GameMakerTexturePageItem? frame, GMImage? page, int width, int height, int tolerance)
    {
        var stride = (width + 7) / 8;
        var mask = new byte[stride * height];
        if (frame is null || page is null) return mask;
        var pixels = page.GetData();
        for (var y = 0; y < height; y++)
        {
            var py = y - frame.TargetY + frame.SourceY;
            if (y < frame.TargetY || y >= frame.TargetY + frame.SourceHeight) continue;
            for (var x = 0; x < width; x++)
            {
                if (x < frame.TargetX || x >= frame.TargetX + frame.SourceWidth) continue;
                var px = x - frame.TargetX + frame.SourceX;
                if (pixels[((py * page.Width) + px) * 4 + 3] > tolerance) mask[(y * stride) + (x / 8)] |= (byte)(0x80 >> (x % 8));
            }
        }

        return mask;
    }

    public override void Apply(GameMakerData data)
    {
        var target = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        _old = [.. target.CollisionMasks.Select(m => m?.Data)];
        var raw = new Dictionary<GameMakerEmbeddedTexture, GMImage?>();
        for (var i = 0; i < target.CollisionMasks.Count; i++)
        {
            var entry = target.CollisionMasks[i];
            var frame = target.Textures.ElementAtOrDefault(Math.Min(i, Math.Max(0, target.Textures.Count - 1)))?.Texture;
            GMImage? page = null;
            if (frame?.TexturePage is { } texture && !raw.TryGetValue(texture, out page))
                raw[texture] = page = texture.TextureData?.Image is { Width: > 1 } image ? image.ConvertToRawBgra() : null;
            using var shape = target.NewMaskEntry(data);
            (entry.Width, entry.Height) = (shape.Width, shape.Height);
            entry.Data = BuildMask(frame, page, shape.Width, shape.Height, tolerance);
        }
    }

    public override void Revert(GameMakerData data)
    {
        var target = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        for (var i = 0; i < _old.Length && i < target.CollisionMasks.Count; i++)
            if (_old[i] is { } bytes) target.CollisionMasks[i].Data = bytes;
    }
}
