using G3MStudio.Core.Settings;

namespace G3MStudio.Editor;

/// <summary>What the Resize dialog offers: the frames, the texture groups and the sprite's group, and for the Advanced choices the pages and the largest page side without a choice.</summary>
public sealed record ResizeContext(int Frames, IReadOnlyList<string> Groups, string? Group, IReadOnlyList<string>? Pages = null, int AutomaticMax = 0, bool Box = false, IReadOnlyList<(int Width, int Height)>? FrameSizes = null);

/// <summary>The options of the Resize and Import Sheet dialogs that are kept between dialogs and runs (pixel sizes are not: the current size is the default; the percents are).</summary>
public sealed record ResizeChoices
{
    private static WeakReference<SettingsStore>? _store;

    public bool Scale { get; init; } = true;

    public bool Smooth { get; init; }

    public bool Keep { get; init; } = true;

    public bool Percent { get; init; }

    public int Anchor { get; init; }

    /// <summary>0 clear, 1 black, 2 white.</summary>
    public int Fill { get; init; }

    /// <summary>The modifier key (Alt, Control, Shift or Meta) that makes a handle drag apply to every frame of a sprite; the one place to change it.</summary>
    public string AllKey { get; init; } = "Alt";

    /// <summary>Resize image also scales the collision box and the origin of a sprite.</summary>
    public bool Box { get; init; } = true;

    public int PercentW { get; init; } = 100;

    public int PercentH { get; init; } = 100;

    public bool SheetBySize { get; init; }

    /// <summary>The window's settings, where the choices are kept between runs; without it (tests) they last for the run.</summary>
    public static void Hook(SettingsStore store) => _store = new WeakReference<SettingsStore>(store);

    /// <summary>Applying a dialog sets it; the next dialog reads it.</summary>
    public static ResizeChoices Current
    {
        get => _store?.TryGetTarget(out var store) == true ? From(store.Current.Image) : field;
        set
        {
            field = value;
            if (_store is null || !_store.TryGetTarget(out var store)) return;
            store.Update(s => s with
            {
                Image = s.Image with
                {
                    ResizeScale = value.Scale,
                    ResizeSmooth = value.Smooth,
                    ResizeKeep = value.Keep,
                    ResizePercent = value.Percent,
                    ResizeAnchor = value.Anchor,
                    ResizeFill = value.Fill,
                    ResizeAllKey = value.AllKey,
                    ResizeBox = value.Box,
                    ResizePercentW = value.PercentW,
                    ResizePercentH = value.PercentH,
                    SheetBySize = value.SheetBySize,
                },
            }, false);
        }
    } = new();

    private static ResizeChoices From(ImageOptions o) => new()
    {
        Scale = o.ResizeScale,
        Smooth = o.ResizeSmooth,
        Keep = o.ResizeKeep,
        Percent = o.ResizePercent,
        Anchor = o.ResizeAnchor,
        Fill = o.ResizeFill,
        AllKey = o.ResizeAllKey,
        Box = o.ResizeBox,
        PercentW = o.ResizePercentW,
        PercentH = o.ResizePercentH,
        SheetBySize = o.SheetBySize,
    };

    /// <summary>The colour of new canvas area for a fill index: clear, black or white.</summary>
    public static Core.Workspace.Rgba? FillColour(int fill) => fill switch { 1 => new(0, 0, 0, 255), 2 => new(255, 255, 255, 255), _ => null };
}
