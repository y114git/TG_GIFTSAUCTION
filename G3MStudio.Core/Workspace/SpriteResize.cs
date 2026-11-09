using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>How a picture changes size: <see cref="Scale"/> stretches the pixels, otherwise the canvas changes and the picture sits at the anchor (0, 1 or 2 across and down).</summary>
public sealed record ResizeSpec(bool Scale, int Width, int Height, int AnchorX = 0, int AnchorY = 0)
{
    public const int MaxSide = 4096;

    public ImageOp Op => Scale
        ? new ImageOp(ImageOps.Scale, ImageOp.Format(Width), ImageOp.Format(Height))
        : new ImageOp(ImageOps.Resize, ImageOp.Format(Width), ImageOp.Format(Height), ImageOp.Format(AnchorX), ImageOp.Format(AnchorY));

    /// <summary>The pixel size for a percentage of the current size (at least 1 pixel).</summary>
    public static int Percent(int size, double percent) => Math.Clamp((int)Math.Round(size * percent / 100), 1, MaxSide);
}

/// <summary>Builds the one undoable edit of the Resize dialog of the image editor: the frames change size, a whole-sprite resize also moves the sprite's box, margins, origin and masks, and the sprite may change texture group.</summary>
public static class SpriteResize
{
    /// <summary>The name of the texture group that lists the sprite, or null (older games have no groups).</summary>
    public static string? GroupOf(GameMakerData data, string sprite) =>
        data.TextureGroupInfo?.FirstOrDefault(g => g.Sprites.Any(s => TextureGroupMoveCommand.NameOf(s) == sprite))?.Name.Content;

    /// <summary>The edit for a resize: frame <paramref name="frame"/> or every frame, and a move to texture group <paramref name="group"/> (null or the current group: no move).</summary>
    public static EditCommand Build(GameMakerData data, string sprite, int frame, ResizeSpec spec, bool allFrames, string? group)
    {
        if (spec.Width is < 1 or > ResizeSpec.MaxSide || spec.Height is < 1 or > ResizeSpec.MaxSide) throw new InvalidDataException($"A picture side is 1 to {ResizeSpec.MaxSide} pixels.");
        var target = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        if (frame < 0 || frame >= target.Textures.Count) throw new InvalidOperationException($"Sprite '{sprite}' has no frame {frame}.");
        var items = new List<EditCommand>();
        foreach (var i in allFrames ? Enumerable.Range(0, target.Textures.Count) : [frame]) items.Add(new ImageEditCommand(sprite, i, null, [spec.Op]));
        if (allFrames) items.AddRange(Box(target, spec));
        if (group is not null && group != GroupOf(data, sprite)) items.Add(new TextureGroupMoveCommand("sprites", [sprite], group));
        return items.Count == 1 ? items[0] : new GroupCommand(items);
    }

    // The box, margins and origin follow the pixels: scaled with a stretch, shifted by the anchor with a canvas change.
    private static IEnumerable<EditCommand> Box(GameMakerSprite s, ResizeSpec spec)
    {
        int w = Math.Max(1, (int)s.Width), h = Math.Max(1, (int)s.Height); // a sprite with no size would divide by zero in the scale
        Func<int, int> x = spec.Scale ? v => (int)Math.Round((double)v * spec.Width / w) : v => v + (spec.Width - w) * spec.AnchorX / 2;
        Func<int, int> y = spec.Scale ? v => (int)Math.Round((double)v * spec.Height / h) : v => v + (spec.Height - h) * spec.AnchorY / 2;
        SetPropertyCommand Set(string path, int value) => new("sprites", s.Name.Content ?? "", path, Math.Max(0, value).ToString(CultureInfo.InvariantCulture));
        yield return Set("Width", spec.Width);
        yield return Set("Height", spec.Height);
        yield return Set("MarginLeft", Math.Min(x(s.MarginLeft), spec.Width - 1));
        yield return Set("MarginRight", Math.Min(x(s.MarginRight), spec.Width - 1));
        yield return Set("MarginTop", Math.Min(y(s.MarginTop), spec.Height - 1));
        yield return Set("MarginBottom", Math.Min(y(s.MarginBottom), spec.Height - 1));
        yield return new SpriteOriginCommand(s.Name.Content ?? "", x(s.OriginX), y(s.OriginY));
        yield return new RecalcMaskCommand(s.Name.Content ?? "");
    }
}
