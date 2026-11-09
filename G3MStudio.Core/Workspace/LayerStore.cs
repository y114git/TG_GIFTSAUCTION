using System.Runtime.CompilerServices;
using System.Text.Json;
using G3MLib.DataFile;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The layer stacks of the sprite frames of one open data file, keyed by sprite name and frame. The data file holds only the flat picture of a frame, so a stack
/// is trusted only while its flattened result still looks like the frame; when the frame was changed by anything else (replace frame, an undo of another edit,
/// a frame list change) the stack is dropped and the frame becomes one layer with the flat picture, which loses nothing the data file had.
/// <see cref="Export"/> and <see cref="Import"/> move the stacks through the view state file, so they survive closing the file.
/// </summary>
public sealed class LayerStore
{
    private static readonly ConditionalWeakTable<GameMakerData, LayerStore> Stores = [];
    private readonly Dictionary<string, LayerSet> _stacks = [];

    private const int MaxLayers = 64, MaxSide = 16384, MaxStackChars = 256 * 1024 * 1024;

    private sealed record Stored(string Name, bool Visible, bool Locked, int Opacity, LayerBlend Blend, string Png);

    public static LayerStore Of(GameMakerData data) => Stores.GetValue(data, _ => new LayerStore());

    private static string Key(string sprite, int frame) => $"{sprite}#{frame}";

    public bool Has(string sprite, int frame)
    {
        lock (_stacks) return _stacks.ContainsKey(Key(sprite, frame));
    }

    /// <summary>The layers of a frame: the stored stack while the frame still looks like it, else one layer with the frame's picture.</summary>
    public LayerSet Read(GameMakerData data, string sprite, int frame)
    {
        var flat = FramePicture.Read(data, sprite, frame);
        lock (_stacks)
        {
            // A stack that no longer matches stays: an undo of the other edit makes it valid again.
            if (_stacks.TryGetValue(Key(sprite, frame), out var stack) && LayerSet.SameLook(stack.Flat, flat)) return stack;
        }

        return LayerSet.OneLayer(flat);
    }

    /// <summary>Keeps the stack of a frame; null, or one plain layer, forgets it.</summary>
    public void Set(string sprite, int frame, LayerSet? stack)
    {
        lock (_stacks)
        {
            if (stack is null || stack.IsPlain) _stacks.Remove(Key(sprite, frame));
            else _stacks[Key(sprite, frame)] = stack;
        }

        Changed?.Invoke();
    }

    /// <summary>Raised after a stack was kept or forgotten; may run on the thread of the edit, with the workspace locked.</summary>
    public event Action? Changed;

    public Dictionary<string, string> Export()
    {
        // Encoding the pictures takes long; only the lookup is under the lock, so an edit never waits for a write.
        KeyValuePair<string, LayerSet>[] snapshot;
        lock (_stacks) snapshot = [.. _stacks];
        return snapshot.ToDictionary(p => p.Key, p => JsonSerializer.Serialize(p.Value.Layers.Select(l => new Stored(l.Name, l.Visible, l.Locked, l.Opacity, l.Blend, Convert.ToBase64String(FramePicture.ToPng(l.Pixels)))).ToList()));
    }

    /// <summary>Takes stacks saved by <see cref="Export"/>; a damaged entry is skipped (the frame then shows as one layer).</summary>
    public void Import(IReadOnlyDictionary<string, string>? saved)
    {
        foreach (var (key, text) in saved ?? new Dictionary<string, string>())
        {
            try
            {
                if (text.Length > MaxStackChars) throw new ArgumentException("The layer stack is too large.");
                var layers = JsonSerializer.Deserialize<List<Stored>>(text) ?? [];
                if (layers.Count > MaxLayers) throw new ArgumentException("The layer stack has too many layers.");
                var stack = new LayerSet([.. layers.Select(l => new PaintLayer(l.Name ?? "", Decode(l), l.Visible, l.Locked, Math.Clamp(l.Opacity, 0, 100), Enum.IsDefined(l.Blend) ? l.Blend : LayerBlend.Normal))]);
                lock (_stacks) _stacks[key] = stack;
            }
            catch (Exception e) when (e is JsonException or ArgumentException or FormatException or MagickException)
            {
                // a lost convenience: the frame is read as one layer
            }
        }
    }

    /// <summary>Takes the stacks of the view state, then the newer ones of the autosave sidecar; a frame in both ends up with the sidecar's stack.</summary>
    public void ImportSaved(IReadOnlyDictionary<string, string>? viewState, IReadOnlyDictionary<string, string>? sidecar)
    {
        Import(viewState);
        Import(sidecar);
    }

    private static PixelBuffer Decode(Stored layer)
    {
        var bytes = Convert.FromBase64String(layer.Png ?? "");
        var info = new MagickImageInfo(bytes); // reads the header only: a small file can claim a huge picture
        if (info.Width > MaxSide || info.Height > MaxSide) throw new ArgumentException("The layer picture is too large.");
        using var image = new MagickImage(bytes);
        image.Alpha(AlphaOption.Set);
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new ArgumentException("The layer has no pixels."));
    }
}
