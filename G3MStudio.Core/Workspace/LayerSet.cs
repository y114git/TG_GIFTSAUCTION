using System.Globalization;

namespace G3MStudio.Core.Workspace;

public enum LayerBlend { Normal, Multiply, Screen, Overlay, Add }

/// <summary>One layer of a frame. Opacity is a percent. The pixels of a layer are never changed after it is in a stack; an edit makes a new layer.</summary>
public sealed record PaintLayer(string Name, PixelBuffer Pixels, bool Visible = true, bool Locked = false, int Opacity = 100, LayerBlend Blend = LayerBlend.Normal)
{
    public bool IsPlain => Visible && Opacity == 100 && Blend == LayerBlend.Normal;
}

/// <summary>
/// The layers of one sprite frame, bottom first. The data file stores only the flattened result (<see cref="Flat"/>); the stack lives beside it in <see cref="LayerStore"/>.
/// A stack never changes: every layer operation returns a new one, which makes undo a matter of keeping the old one.
/// </summary>
public sealed class LayerSet
{
    public LayerSet(IReadOnlyList<PaintLayer> layers)
    {
        if (layers.Count == 0 || layers.Any(l => l.Pixels.Width != layers[0].Pixels.Width || l.Pixels.Height != layers[0].Pixels.Height)) throw new ArgumentException("A stack needs layers of one size.", nameof(layers));
        Layers = layers;
    }

    public IReadOnlyList<PaintLayer> Layers { get; }

    public int Count => Layers.Count;

    public int Width => Layers[0].Pixels.Width;

    public int Height => Layers[0].Pixels.Height;

    /// <summary>One plain layer holding the picture: what a frame is when nothing else is known.</summary>
    public static LayerSet OneLayer(PixelBuffer flat) => new([new PaintLayer("Layer 1", flat)]) { Flat = flat };

    /// <summary>True for a single plain layer, which the store need not keep.</summary>
    public bool IsPlain => Layers is [{ IsPlain: true }];

    /// <summary>All visible layers composited; a single plain layer is its own picture.</summary>
    public PixelBuffer Flat
    {
        get => field ??= Compose();
        private init;
    }

    private PixelBuffer Compose()
    {
        if (IsPlain) return Layers[0].Pixels;
        var flat = new PixelBuffer(Width, Height);
        Composite(flat, new Box(0, 0, Width, Height));
        return flat;
    }

    public LayerSet With(int index, PaintLayer layer) => new([.. Layers.Select((l, i) => i == index ? layer : l)]);

    /// <summary>Composites the layers into the area of <paramref name="target"/>. The layer at <paramref name="live"/> can be replaced by the picture being drawn on.</summary>
    public void Composite(PixelBuffer target, Box area, int live = -1, PixelBuffer? liveMap = null)
    {
        var (x0, y0, x1, y1) = (Math.Max(0, area.X), Math.Max(0, area.Y), Math.Min(Width, area.X + area.Width), Math.Min(Height, area.Y + area.Height));
        var shown = Layers.Select((l, i) => (Layer: l, Data: i == live && liveMap is not null ? liveMap.Data : l.Pixels.Data)).Where(s => s.Layer.Visible && s.Layer.Opacity > 0).ToArray();
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var i = (y * Width + x) * 4;
                var pixel = default(Rgba);
                foreach (var (layer, data) in shown) pixel = Over(pixel, new Rgba(data[i], data[i + 1], data[i + 2], data[i + 3]), layer.Opacity, layer.Blend);
                (target.Data[i], target.Data[i + 1], target.Data[i + 2], target.Data[i + 3]) = (pixel.R, pixel.G, pixel.B, pixel.A);
            }

        target.MarkDirty(x0, y0, x1 - 1, y1 - 1);
    }

    /// <summary>
    /// One source pixel over a backdrop, straight alpha, whole numbers (the W3C compositing formula): the colours are mixed by the blend function weighted by
    /// the backdrop alpha, then composited with the source alpha (its own alpha times the layer opacity).
    /// </summary>
    internal static Rgba Over(Rgba back, Rgba source, int opacity, LayerBlend blend)
    {
        int sa = source.A * opacity / 100, ba = back.A;
        if (sa == 0) return back;
        var oa = sa + ba * (255 - sa) / 255;
        int Channel(int cb, int cs)
        {
            var mixed = ((255 - ba) * cs + ba * BlendChannel(blend, cb, cs)) / 255;
            return Math.Min(255, (sa * mixed * 255 + (255 - sa) * ba * cb) / (oa * 255));
        }

        return new Rgba((byte)Channel(back.R, source.R), (byte)Channel(back.G, source.G), (byte)Channel(back.B, source.B), (byte)oa);
    }

    private static int BlendChannel(LayerBlend blend, int cb, int cs) => blend switch
    {
        LayerBlend.Multiply => cb * cs / 255,
        LayerBlend.Screen => cb + cs - cb * cs / 255,
        LayerBlend.Overlay => cb < 128 ? 2 * cb * cs / 255 : 255 - 2 * (255 - cb) * (255 - cs) / 255,
        LayerBlend.Add => Math.Min(255, cb + cs),
        _ => cs,
    };

    /// <summary>
    /// Runs a layer operation (an <see cref="ImageOp"/> of kind "layer"): add, delete, dup, move, merge, visible, lock, opacity, blend, rename or flatten.
    /// The first part is the verb; the second is the layer number, bottom is 0. Out of range numbers are clamped, so a replay on a stack that fell back to one layer still runs.
    /// </summary>
    public LayerSet Apply(ImageOp op)
    {
        var verb = op.Parts[0];
        if (verb == "flatten") return OneLayer(Flat);
        var at = Math.Clamp(op.Int(1), 0, Count - 1);
        var list = Layers.ToList();
        switch (verb)
        {
            case "add": list.Insert(at + 1, new PaintLayer(op.Parts[2], new PixelBuffer(Width, Height))); break;
            case "delete" when Count > 1: list.RemoveAt(at); break;
            case "dup": list.Insert(at + 1, list[at] with { Name = op.Parts[2], Pixels = list[at].Pixels.Copy() }); break;
            case "move": Move(list, at, op.Int(2)); break;
            case "merge" when at > 0: list[at - 1] = list[at - 1] with { Pixels = Merge(list[at - 1], list[at]) }; list.RemoveAt(at); break;
            case "visible": list[at] = list[at] with { Visible = op.Parts[2] == "1" }; break;
            case "lock": list[at] = list[at] with { Locked = op.Parts[2] == "1" }; break;
            case "opacity": list[at] = list[at] with { Opacity = Math.Clamp(op.Int(2), 0, 100) }; break;
            case "blend": list[at] = list[at] with { Blend = Enum.Parse<LayerBlend>(op.Parts[2], true) }; break;
            case "rename": list[at] = list[at] with { Name = op.Parts[2] }; break;
        }

        return new LayerSet(list);
    }

    private static void Move(List<PaintLayer> list, int at, int delta)
    {
        var to = Math.Clamp(at + delta, 0, list.Count - 1);
        var layer = list[at];
        list.RemoveAt(at);
        list.Insert(to, layer);
    }

    // The upper layer drawn onto the lower one with its own blend and opacity; the result keeps the settings of the lower layer.
    private static PixelBuffer Merge(PaintLayer lower, PaintLayer upper)
    {
        var merged = new PixelBuffer(lower.Pixels.Width, lower.Pixels.Height);
        new LayerSet([lower with { Visible = true, Opacity = 100, Blend = LayerBlend.Normal }, upper]).Composite(merged, new Box(0, 0, merged.Width, merged.Height));
        return merged;
    }

    /// <summary>True when both pictures look the same: equal alpha, and equal colour where something is visible.</summary>
    public static bool SameLook(PixelBuffer a, PixelBuffer b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;
        for (var i = 0; i < a.Data.Length; i += 4)
        {
            if (a.Data[i + 3] != b.Data[i + 3]) return false;
            if (a.Data[i + 3] != 0 && (a.Data[i] != b.Data[i] || a.Data[i + 1] != b.Data[i + 1] || a.Data[i + 2] != b.Data[i + 2])) return false;
        }

        return true;
    }

    public static ImageOp Op(string verb, int layer, string? extra = null) => new(ImageOps.Layer, extra is null ? [verb, layer.ToString(CultureInfo.InvariantCulture)] : [verb, layer.ToString(CultureInfo.InvariantCulture), extra]);
}
