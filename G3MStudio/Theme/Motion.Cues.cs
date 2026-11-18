using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;

namespace G3MStudio.Theme;

/// <summary>Short cues that carry information: where a new tab landed, which item was just created, that an input was refused.</summary>
public static partial class Motion
{
    /// <summary>
    /// A tab that opens: it fades in and grows sideways from its left edge, so the eye sees where in the strip it landed.
    /// Minimal keeps the fade only. One run on one element.
    /// </summary>
    public static void Open(Control tab)
    {
        if (Instant) return;
        var from = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } };
        var to = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0) } };
        if (MotionPolicy.AllowsMovement(Level))
        {
            tab.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
            from.Setters.Add(new Setter(GrowXProperty, 0.6));
            to.Setters.Add(new Setter(GrowXProperty, 1.0));
        }

        Play(tab, new Animation { Duration = Ms(150), Easing = new CubicEaseOut(), Children = { from, to } });
    }

    /// <summary>
    /// A refused input: the control shakes sideways a few pixels. Minimal (no movement) dips its opacity instead.
    /// </summary>
    public static void Shake(Control control)
    {
        if (Instant) return;
        var offsets = MotionPolicy.ShakeOffsets;
        var shake = new Animation { Duration = Ms(200), Easing = new LinearEasing() };
        for (var i = 0; i < offsets.Length; i++)
        {
            var frame = new KeyFrame { Cue = new Cue((double)i / (offsets.Length - 1)) };
            if (MotionPolicy.AllowsMovement(Level)) frame.Setters.Add(new Setter(ShiftXProperty, offsets[i]));
            else frame.Setters.Add(new Setter(Visual.OpacityProperty, offsets[i] == 0 ? 1.0 : 0.6));
            shake.Children.Add(frame);
        }

        Play(control, shake);
    }

    /// <summary>
    /// Lights one list row (or tree row) in the accent colour and lets it fade, so the eye finds the item that was just created, duplicated or renamed.
    /// It animates the row's own fill only (the selection fill is a style, an animation outranks it) and touches no other row.
    /// </summary>
    public static void Flash(Control row)
    {
        if (Instant || Application.Current?.FindResource("Text.Accent") is not ISolidColorBrush accent) return;
        var fill = row.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(p => p.Name is "PART_ContentPresenter" or "PART_HeaderPresenter");
        if (fill is null) return;
        static KeyFrame Frame(double cue, Color colour, byte alpha) => new() { Cue = new Cue(cue), Setters = { new Setter(ContentPresenter.BackgroundProperty, new SolidColorBrush(Color.FromArgb(alpha, colour.R, colour.G, colour.B))) } };
        Play(fill, new Animation { Duration = Ms(200), Easing = new CubicEaseOut(), Children = { Frame(0, accent.Color, 0x70), Frame(0.4, accent.Color, 0x50), Frame(1, accent.Color, 0) } });
    }
}
