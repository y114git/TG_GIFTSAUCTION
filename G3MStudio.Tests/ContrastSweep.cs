using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>
/// Measures the text and icons of a visual tree against what is drawn behind them: the first opaque fill up the tree (translucent fills stacked over it, opacity applied),
/// in the state the tree is in and with every kind of row, button and tab forced into each pointer state.
/// </summary>
internal static class ContrastSweep
{
    public const double Text = 4.5, Large = 3.0, Icon = 3.0, Disabled = 3.0;

    // States forced one kind of control at a time; a press on a button inside a row does not press the row.
    private static readonly (Type Kind, string[] States)[] Kinds =
    [
        (typeof(Button), ["", ":pointerover", ":pressed", ":focus", ":disabled"]),
        (typeof(ToggleButton), ["", ":pointerover", ":pressed", ":checked", ":checked:pointerover", ":disabled"]),
        (typeof(ListBoxItem), ["", ":pointerover", ":selected", ":selected:pointerover", ":disabled"]),
        (typeof(ComboBoxItem), ["", ":pointerover", ":selected", ":selected:pointerover", ":disabled"]),
        (typeof(TreeViewItem), ["", ":pointerover", ":selected", ":selected:pointerover", ":disabled"]),
        (typeof(TabItem), ["", ":pointerover", ":selected", ":selected:pointerover", ":disabled"]),
        (typeof(MenuItem), ["", ":pointerover", ":selected", ":disabled"]),
        (typeof(Dock.Avalonia.Controls.DocumentTabStripItem), ["", ":pointerover", ":selected", ":selected:pointerover"]),
        (typeof(Dock.Avalonia.Controls.ToolTabStripItem), ["", ":pointerover", ":selected", ":selected:pointerover"]),
    ];

    public static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    public static double Ratio(Color a, Color b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    private static Color Over(Color top, Color back, double extra = 1)
    {
        var a = top.A / 255.0 * extra;
        static byte Mix(byte t, byte b, double a) => (byte)Math.Round((t * a) + (b * (1 - a)));
        return Color.FromRgb(Mix(top.R, back.R, a), Mix(top.G, back.G, a), Mix(top.B, back.B, a));
    }

    private static IBrush? FillOf(Visual v) => v switch
    {
        Border b => b.Background,
        Panel p => p.Background,
        ContentPresenter c => c.Background,
        TemplatedControl t => t.Background,
        _ => null,
    };

    /// <summary>What shows behind a visual: its ancestors' fills, translucent ones stacked, down to the first opaque one (the window colour when none is).</summary>
    public static Color Behind(Visual visual)
    {
        var layers = new List<Color>();
        var opaque = Color.Parse("#282828");
        for (var v = visual.GetVisualParent(); v is not null; v = v.GetVisualParent())
        {
            if (FillOf(v) is not ISolidColorBrush { Color.A: > 0 } brush) continue;
            if (brush.Color.A * brush.Opacity >= 254)
            {
                opaque = brush.Color;
                break;
            }

            layers.Add(Color.FromArgb((byte)(brush.Color.A * brush.Opacity), brush.Color.R, brush.Color.G, brush.Color.B));
        }

        for (var i = layers.Count - 1; i >= 0; i--) opaque = Over(layers[i], opaque);
        return opaque;
    }

    private static double OpacityAbove(Visual visual)
    {
        var o = 1.0;
        for (Visual? v = visual; v is not null; v = v.GetVisualParent()) o *= double.IsNaN(v.Opacity) ? 1 : v.Opacity;
        return o;
    }

    // A control a state change replaced (the flag rows rebuild when one is ticked) is no longer in a window.
    private static bool Shown(Control c) => c.GetVisualAncestors().Any(a => a is Window) && c.IsEffectivelyVisible && c.Bounds.Width >= 1 && c.Bounds.Height >= 1;

    private static bool Large18(TextBlock t) => t.FontSize >= 24 || (t.FontSize >= 18.66 && t.FontWeight >= FontWeight.Bold);

    // Custom drawn surfaces and pictures carry colours of the user's own art, not of the theme.
    private static readonly string[] Skip = ["RowThumbnail", "HoverIcon"];

    // The nearest control with a name or a caption, so a finding says which button or tab it sits in.
    private static string Describe(Visual v, Visual root)
    {
        var chain = new List<string>();
        for (Visual? c = v; c is not null && chain.Count < 3; c = c.GetVisualParent())
        {
            if (c is ContentPresenter or Border or Panel or ScrollContentPresenter && c != v) continue;
            if (c is Control { Name: { Length: > 0 } n } && c != v) chain.Add(c.GetType().Name + "#" + n);
            else if (c is ContentControl { Content: string text }) chain.Add(c.GetType().Name + "\"" + Trim(text) + "\"");
            else if (c is TemplatedControl && c != v) chain.Add(c.GetType().Name);
            if (c == root) break;
        }

        return string.Join(" < ", chain);
    }

    /// <summary>The measurements of the tree in its current state: a line per text or icon under its threshold.</summary>
    public static IEnumerable<string> Measure(Visual root, string state = "")
    {
        foreach (var visual in root.GetVisualDescendants().Prepend(root))
        {
            if (visual is not Control control || !Shown(control)) continue;
            if (visual.GetVisualAncestors().OfType<Control>().Any(a => Skip.Contains(a.Name ?? ""))) continue;
            (IBrush? brush, string kind, double need, string what) = visual switch
            {
                TextBlock { Text.Length: > 0 } t => (t.Foreground, "text", Large18(t) ? Large : Text, $"\"{Trim(t.Text)}\""),
                TextPresenter { Text.Length: > 0 } p => (p.Foreground, "input", Text, $"\"{Trim(p.Text)}\""),
                Avalonia.Controls.Shapes.Path { Fill: { } fill, Width: <= 40 } => (fill, "icon", Icon, ""),
                PathIcon { Foreground: { } fg } => (fg, "icon", Icon, ""),
                _ => (null, "", 0, ""),
            };
            if (brush is not ISolidColorBrush solid) continue;
            // The tick of a check box is drawn over the box's own fill, which is not an ancestor of it; an unchecked box shows no tick.
            if (control.Name == "CheckGlyph" && control.GetVisualAncestors().OfType<CheckBox>().FirstOrDefault() is { } box)
            {
                if (box.IsChecked != true) continue;
                if (box.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "NormalRectangle") is not { Background: ISolidColorBrush { Color: var paint } } rectangle) continue;
                var boxFill = Over(paint, Behind(rectangle));
                var tick = Ratio(solid.Color, boxFill);
                if (tick < Icon) yield return $"icon check tick {box.Name}{(state.Length > 0 ? " [" + state + "]" : "")}: {solid.Color} on {boxFill} is {tick:0.0}:1, needs {Icon:0.0}";
                continue;
            }

            var opacity = OpacityAbove(visual) * solid.Opacity;
            if (opacity < 0.05) continue;
            var disabled = !control.IsEffectivelyEnabled;
            var back = Behind(visual);
            var front = Over(solid.Color, back, opacity);
            var ratio = Ratio(front, back);
            var limit = disabled ? Math.Min(need, Disabled) : need;
            if (ratio < limit) yield return $"{kind} {what} in {Describe(visual, root)}{(state.Length > 0 ? " [" + state + "]" : "")}{(disabled ? " [disabled]" : "")}: {front} on {back} is {ratio:0.0}:1, needs {limit:0.0}";
        }
    }

    private static string Trim(string? s) => s is { Length: > 24 } ? s[..24] + "..." : s ?? "";

    private static async Task Settle()
    {
        // Brush transitions of the theme (80 ms) must be over, or a half-faded colour is measured.
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(130);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Measures the tree as it stands, then once for each state of each kind of control, with the state forced on every control of that kind at once.</summary>
    public static async Task<List<string>> Sweep(Visual root, string where)
    {
        var found = new List<string>();
        found.AddRange(Measure(root).Select(p => where + ": " + p));
        foreach (var (kind, states) in Kinds)
        {
            var controls = root.GetVisualDescendants().Prepend(root).OfType<Control>().Where(kind.IsInstanceOfType).Where(Shown).Take(40).ToList();
            if (controls.Count == 0) continue;
            foreach (var state in states.Where(s => s.Length > 0))
            {
                var undo = new List<Action>();
                foreach (var control in controls) undo.Add(Force(control, state));
                await Settle();
                foreach (var control in controls)
                    found.AddRange(Measure(control, kind.Name + state).Select(p => where + ": " + p));
                foreach (var action in undo) action();
            }

            await Settle();
        }

        return [.. found.Distinct()];
    }

    internal static Action Force(Control control, string state)
    {
        if (state == ":disabled")
        {
            var before = control.IsEnabled;
            control.IsEnabled = false;
            return () => control.IsEnabled = before;
        }

        var flags = state.Split(':', StringSplitOptions.RemoveEmptyEntries);
        // A toggle's own code decides what an on toggle looks like (icon opacity), so it is really switched on.
        if (control is ToggleButton toggle && flags.Contains("checked"))
        {
            var before = toggle.IsChecked;
            toggle.IsChecked = true;
            var undo = Force(control, state.Replace(":checked", "", StringComparison.Ordinal));
            return () =>
            {
                undo();
                toggle.IsChecked = before;
            };
        }

        var classes = (IPseudoClasses)control.Classes;
        var was = flags.Select(f => control.Classes.Contains(":" + f)).ToArray();
        foreach (var f in flags) classes.Add(":" + f);
        return () =>
        {
            for (var i = 0; i < flags.Length; i++) if (!was[i]) classes.Remove(":" + flags[i]);
        };
    }
}
