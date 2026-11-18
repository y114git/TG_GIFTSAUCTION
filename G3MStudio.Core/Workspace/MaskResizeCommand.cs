using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Follows a resize of the sprite's frames with a manual collision box: the margins move with the pixels (stretched, or shifted by the anchor and kept inside the picture)
/// and each mask is resampled the same way, so a hand-made mask keeps its shape. Runs after the sprite's Width and Height are set; the old box and masks come back on undo.
/// </summary>
public sealed class MaskResizeCommand(string sprite, int oldWidth, int oldHeight, bool scale, int anchorX, int anchorY) : EditCommand
{
    public const string Name = "maskResize";

    private (MaskBox Box, (byte[] Data, int Width, int Height)[] Masks)? _old;

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["oldWidth"] = oldWidth.ToString(CultureInfo.InvariantCulture),
        ["oldHeight"] = oldHeight.ToString(CultureInfo.InvariantCulture),
        ["scale"] = scale ? "1" : "0",
        ["anchorX"] = anchorX.ToString(CultureInfo.InvariantCulture),
        ["anchorY"] = anchorY.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches { get; } = [new("Sprites", sprite)];

    public static MaskResizeCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["sprite"], int.Parse(a["oldWidth"], CultureInfo.InvariantCulture), int.Parse(a["oldHeight"], CultureInfo.InvariantCulture),
        a["scale"] == "1", int.Parse(a["anchorX"], CultureInfo.InvariantCulture), int.Parse(a["anchorY"], CultureInfo.InvariantCulture));

    private static GameMakerSprite Find(GameMakerData data, string name) => data.Sprites.FirstOrDefault(s => s.Name.Content == name) ?? throw new InvalidOperationException($"Sprite '{name}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data, sprite);
        int width = (int)target.Width, height = (int)target.Height;
        var oldBox = SpriteMaskCommand.BoxOf(target);
        _old = (oldBox, [.. target.CollisionMasks.Select(m => (m.Data, m.Width, m.Height))]);
        // The first pixel column that lands in the old box and the last: with a stretch, the new pixels whose source lies in it.
        int Low(int v, int from, int to, int anchor) => scale ? (int)(((long)v * to + from - 1) / from) : v + (to - from) * anchor / 2;
        int High(int v, int from, int to, int anchor) => scale ? (int)(((long)(v + 1) * to + from - 1) / from) - 1 : v + (to - from) * anchor / 2;
        var box = SpriteMask.Clamp(new MaskBox(Low(oldBox.Left, oldWidth, width, anchorX), Low(oldBox.Top, oldHeight, height, anchorY),
            High(oldBox.Right, oldWidth, width, anchorX), High(oldBox.Bottom, oldHeight, height, anchorY)), width, height);
        (target.MarginLeft, target.MarginTop, target.MarginRight, target.MarginBottom) = (box.Left, box.Top, box.Right, box.Bottom);
        // From 2024.6 a mask covers only the box; before that the whole picture.
        var modern = data.IsVersionAtLeast(2024, 6);
        var (oldArea, area) = (modern ? oldBox : SpriteMask.Full(oldWidth, oldHeight), modern ? box : SpriteMask.Full(width, height));
        foreach (var mask in target.CollisionMasks)
        {
            var (stride, bits) = ((area.Width + 7) / 8, new byte[((area.Width + 7) / 8) * area.Height]);
            var fromStride = (mask.Width + 7) / 8;
            for (var y = 0; y < area.Height; y++)
            {
                var sy = Source(area.Top + y, oldHeight, height, scale, anchorY) - oldArea.Top;
                if ((uint)sy >= (uint)mask.Height) continue;
                for (var x = 0; x < area.Width; x++)
                {
                    var sx = Source(area.Left + x, oldWidth, width, scale, anchorX) - oldArea.Left;
                    if ((uint)sx >= (uint)mask.Width || (mask.Data[sy * fromStride + sx / 8] & (0x80 >> (sx % 8))) == 0) continue;
                    bits[y * stride + x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            (mask.Width, mask.Height, mask.Data) = (area.Width, area.Height, bits);
        }
    }

    // The same pixel the picture's own resize takes: floor(x * old / new) for a stretch, shifted back by the anchor otherwise.
    private static int Source(int v, int from, int to, bool scale, int anchor) => scale ? (int)((long)v * from / to) : v - (to - from) * anchor / 2;

    public override void Revert(GameMakerData data)
    {
        var target = Find(data, sprite);
        if (_old is not { } old) throw new InvalidOperationException("The edit was never applied.");
        (target.MarginLeft, target.MarginTop, target.MarginRight, target.MarginBottom) = (old.Box.Left, old.Box.Top, old.Box.Right, old.Box.Bottom);
        foreach (var (mask, saved) in target.CollisionMasks.Zip(old.Masks)) (mask.Data, mask.Width, mask.Height) = saved;
    }
}
