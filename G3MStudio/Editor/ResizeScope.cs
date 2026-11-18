using Avalonia.Input;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>Which frames a resize touches, and the words that tell the user: the selected frame is the default everywhere, every frame is always an explicit choice with the real numbers shown.</summary>
internal static class ResizeScope
{
    private const int SizeCap = 3;

    /// <summary>The key that makes a handle drag apply to every frame; set in <see cref="ResizeChoices.AllKey"/> (Alt, Control, Shift or Meta).</summary>
    public static KeyModifiers Modifier =>
        Enum.TryParse<KeyModifiers>(ResizeChoices.Current.AllKey, true, out var key) && key is KeyModifiers.Alt or KeyModifiers.Control or KeyModifiers.Shift or KeyModifiers.Meta ? key : KeyModifiers.Alt;

    /// <summary>The distinct sizes in order of first use, at most three, then "...".</summary>
    public static string Sizes(IReadOnlyList<(int Width, int Height)> sizes)
    {
        var distinct = sizes.Distinct().ToList();
        var text = string.Join(", ", distinct.Take(SizeCap).Select(s => $"{s.Width} x {s.Height}"));
        return distinct.Count > SizeCap ? text + " ..." : text;
    }

    /// <summary>The line that says what an all-frames resize does: how many frames, the new size and the sizes they have now.</summary>
    public static string Warning(IReadOnlyList<(int Width, int Height)> sizes, int width, int height) =>
        Loc.Current.Format("image.scopeWarn", ("n", sizes.Count), ("w", width), ("h", height), ("sizes", Sizes(sizes)));

    /// <summary>What the live label of a handle drag adds: the frame, or every frame with the sizes they have now. Nothing for a sprite of one frame.</summary>
    public static string DragSuffix(ResizeSpec spec, int frame, IReadOnlyList<(int Width, int Height)> sizes) =>
        (sizes.Count < 2 ? "" : spec.AllFrames ? Loc.Current.Format("image.dragScopeAll", ("n", sizes.Count), ("sizes", Sizes(sizes))) : Loc.Current.Format("image.dragScopeFrame", ("n", frame + 1)))
        + (Math.Max(spec.Width, spec.Height) >= ResizeSpec.MaxSide ? Loc.Current.Format("image.dragMax", ("max", ResizeSpec.MaxSide)) : "");

    /// <summary>The hint under the canvas: the handle keys, with the all-frames key by name when the sprite has more than one frame.</summary>
    public static string DragHint(bool frames) => frames ? Loc.Current.Format("image.dragHintAll", ("key", Modifier)) : Loc.Current["image.dragHint"];
}
