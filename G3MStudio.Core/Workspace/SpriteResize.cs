using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>How a picture changes size: <see cref="Scale"/> stretches the pixels (<see cref="Smooth"/> blends them, otherwise hard edges), otherwise only the canvas changes: the pixels stay,
/// sit at the anchor (0, 1 or 2 across and down) and new area gets <see cref="Fill"/> (clear when null). <see cref="Fit"/> holds the advanced choices of where the picture goes on the texture pages.</summary>
public sealed record ResizeSpec(bool Scale, int Width, int Height, int AnchorX = 0, int AnchorY = 0, FitOptions? Fit = null)
{
    public const int MaxSide = 4096;

    public bool Smooth { get; init; }

    /// <summary>For a handle drag: the new size applies to every frame of the sprite, not only the selected one. The Resize dialog carries this choice itself.</summary>
    public bool AllFrames { get; init; }

    public Rgba? Fill { get; init; }

    /// <summary>With a stretch of a whole sprite: scale the collision box and the origin with the pixels (false: they stay as they are). A canvas change always moves them with the pixels.</summary>
    public bool ScaleBox { get; init; } = true;

    public ImageOp Op => Scale
        ? Smooth ? new ImageOp(ImageOps.Scale, ImageOp.Format(Width), ImageOp.Format(Height), "smooth") : new ImageOp(ImageOps.Scale, ImageOp.Format(Width), ImageOp.Format(Height))
        : new ImageOp(ImageOps.Resize, ImageOp.Format(Width), ImageOp.Format(Height), ImageOp.Format(AnchorX), ImageOp.Format(AnchorY), ImageOp.Format(Fill));

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
        List<int> frames = allFrames ? [.. Enumerable.Range(0, target.Textures.Count)] : [frame];
        // frames without paint layers are placed together, each page written once; a frame with layers flattens them in its own edit
        var store = LayerStore.Of(data);
        if (frames.Count > 1 && !frames.Any(f => store.Has(sprite, f))) items.Add(new FramesFitCommand(sprite, frames, spec.Op, spec.Fit));
        else foreach (var i in frames) items.Add(new ImageEditCommand(sprite, i, null, [spec.Op]) { Fit = spec.Fit });
        if (allFrames) items.AddRange(Box(data, target, spec));
        if (group is not null && group != GroupOf(data, sprite)) items.Add(new TextureGroupMoveCommand("sprites", [sprite], group));
        return items.Count == 1 ? items[0] : new GroupCommand(items);
    }

    // The box, margins and origin follow the pixels: scaled with a stretch, shifted by the anchor with a canvas change. An automatic or full box is found again from
    // the new pixels; a manual box moves with them and its mask bits are resampled the same way, so a hand-made mask keeps its shape.
    private static IEnumerable<EditCommand> Box(GameMakerData data, GameMakerSprite s, ResizeSpec spec)
    {
        int w = Math.Max(1, (int)s.Width), h = Math.Max(1, (int)s.Height); // a sprite with no size would divide by zero in the scale
        var name = s.Name.Content ?? "";
        var keep = spec.Scale && !spec.ScaleBox; // a stretch that leaves the origin and the box alone
        var (originX, originY) = keep ? (s.OriginX, s.OriginY) : (MapPoint(s.OriginX, w, spec.Width, spec.Scale, spec.AnchorX), MapPoint(s.OriginY, h, spec.Height, spec.Scale, spec.AnchorY));
        SetPropertyCommand Set(string path, int value) => new("sprites", name, path, Math.Max(0, value).ToString(CultureInfo.InvariantCulture));
        yield return Set("Width", spec.Width);
        yield return Set("Height", spec.Height);
        yield return new SpriteOriginCommand(name, originX, originY);
        if (keep)
        {
            // The margins stay. A hand-made mask of a box that still fits stays too (from 2024.6 it covers only the box); otherwise the mask is read again from the new pixels inside the same box.
            // An automatic or full sprite keeps its mode: the box is written as a manual one, then the mode is put back.
            var box = SpriteMaskCommand.BoxOf(s);
            var mode = (MaskMode)s.BBoxMode;
            if (!(mode == MaskMode.Manual && data.IsVersionAtLeast(2024, 6) && Math.Max(box.Left, box.Right) < spec.Width && Math.Max(box.Top, box.Bottom) < spec.Height))
                yield return new SpriteMaskCommand(name, MaskMode.Manual, SpriteMaskCommand.ShapeOf(s), box);
            if (mode != MaskMode.Manual) yield return Set("BBoxMode", (int)mode);
            yield break;
        }

        if ((MaskMode)s.BBoxMode == MaskMode.Manual) yield return new MaskResizeCommand(name, w, h, spec.Scale, spec.AnchorX, spec.AnchorY);
        else yield return new SpriteMaskCommand(name, (MaskMode)s.BBoxMode, SpriteMaskCommand.ShapeOf(s), SpriteMaskCommand.BoxOf(s));
    }

    /// <summary>Where a coordinate of the old picture lands in the new one: stretched, or shifted by the anchor.</summary>
    internal static int MapPoint(int v, int oldSize, int newSize, bool scale, int anchor) =>
        scale ? (int)Math.Round((double)v * newSize / oldSize) : v + (newSize - oldSize) * anchor / 2;
}
