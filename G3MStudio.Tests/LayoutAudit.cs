using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>Finds controls that leave their window or scroll area and controls that touch a neighbour with no gap, in any hosted view.</summary>
internal static class LayoutAudit
{
    private static readonly Type[] Targets = [typeof(Button), typeof(TextBox), typeof(ComboBox), typeof(NumericUpDown), typeof(CheckBox), typeof(RadioButton), typeof(ToggleButton), typeof(Slider), typeof(ListBox), typeof(AutoCompleteBox)];

    // A control inside one of these is a part of its template (a spin button, a drop arrow), not a neighbour.
    private static readonly Type[] Composite = [typeof(ComboBox), typeof(NumericUpDown), typeof(TextBox), typeof(ListBox), typeof(Slider), typeof(AutoCompleteBox), typeof(Expander), typeof(Avalonia.Controls.Primitives.ScrollBar), typeof(ToggleButton), typeof(Button), typeof(CheckBox), typeof(RadioButton), typeof(TabControl), typeof(TreeView)];

    public static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Hosts a view in a window of the given size and lets it lay out.</summary>
    public static async Task<Window> Host(Control view, double width, double height = 700)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        await Settle();
        return window;
    }

    private static Rect Where(Control control, Visual root) => control.TransformToVisual(root) is { } matrix ? new Rect(default, control.Bounds.Size).TransformToAABB(matrix) : default;

    private static IEnumerable<Control> Visible(Visual root) => root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width >= 1 && c.Bounds.Height >= 1);

    /// <summary>Shown controls the layout squeezed to nothing: no pixel of them can be reached.</summary>
    private static IEnumerable<Control> Squeezed(Visual root) => root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && (c.Bounds.Width < 1 || c.Bounds.Height < 1) && IsTarget(c) && c is not (ListBox or Slider));

    private static bool Part(Control control) => control.GetVisualAncestors().Any(a => Composite.Any(t => t.IsInstanceOfType(a)));

    private static bool IsTarget(Control control) => Targets.Any(t => t.IsInstanceOfType(control)) && !Part(control);

    /// <summary>A control is cut off when part of it lies outside the window or a scroll area that cannot bring it into view.</summary>
    public static IEnumerable<string> Overflows(Window window)
    {
        var client = new Rect(default, window.Bounds.Size).Inflate(1);
        foreach (var visual in Squeezed(window)) yield return $"{Label(visual)} is squeezed to {Round(Where(visual, window))} in {window.Bounds.Width:0}x{window.Bounds.Height:0}";
        foreach (var visual in Visible(window).Where(IsTarget))
        {
            var rect = Where(visual, window);
            var ancestors = visual.GetVisualAncestors().OfType<Control>().ToList();
            // A scroll viewer or any control that clips to its bounds hides what lies outside it, each one judged on its own. The window is one of them.
            var clippers = ancestors.Where(a => a is ScrollViewer || a.ClipToBounds && a is not Window).Select(a => (Name: $"{a.GetType().Name} '{a.Name}' at {Round(Where(a, window))}", Area: Where(a, window).Inflate(1))).Prepend((Name: "the window", Area: client));
            // A scroll viewer only waives a clip that its own view is part of: it clips the rectangle, can scroll it in, and sits inside the clipping area.
            var scrollers = ancestors.OfType<ScrollViewer>().Select(s => ViewOf(s, window)).Where(v => !v.Area.Inflate(1).Contains(rect) && CanScroll(v.Scroller, rect, v.Area)).Select(v => v.Area).ToList();
            if (clippers.FirstOrDefault(c => !c.Area.Contains(rect) && !scrollers.Any(s => c.Area.Contains(s))) is { Name: { } by })
                yield return $"{Label(visual)} is cut off at {Round(rect)} by {by} in {window.Bounds.Width:0}x{window.Bounds.Height:0}";
        }
    }

    private static readonly Type[] Captioned = [typeof(Button), typeof(ToggleButton), typeof(CheckBox), typeof(RadioButton)];

    /// <summary>A button, check box or radio button whose caption needs more room than it was given: the text is clipped, trimmed or pushed out of the control.</summary>
    public static IEnumerable<string> CutText(Window window)
    {
        foreach (var visual in Visible(window).Where(c => Captioned.Any(t => t.IsInstanceOfType(c)) && !Part(c)))
        {
            var rect = Where(visual, window).Inflate(1);
            foreach (var block in visual.GetVisualDescendants().OfType<TextBlock>().Where(b => b.IsEffectivelyVisible && b.Text is { Length: > 0 }))
            {
                // A wrapping caption is judged by its own measure (what the layout asked for in its wrapping mode, against what it got); a copy of it can differ in font metrics.
                // A single line is measured on a copy at any width.
                var probe = new TextBlock { Text = block.Text, FontSize = block.FontSize, FontFamily = block.FontFamily, FontWeight = block.FontWeight, FontStyle = block.FontStyle, TextWrapping = block.TextWrapping };
                probe.Measure(Size.Infinity);
                var needs = block.TextWrapping == Avalonia.Media.TextWrapping.NoWrap ? probe.DesiredSize : block.DesiredSize;
                var cut = needs.Height > block.Bounds.Height + 1 || needs.Width > block.Bounds.Width + 1 || !rect.Contains(Where(block, window));
                if (cut) yield return $"{Label(visual)} cuts its text \"{block.Text}\": needs {needs.Width:0}x{needs.Height:0} px, has {block.Bounds.Width:0}x{block.Bounds.Height:0} in {window.Bounds.Width:0}x{window.Bounds.Height:0}";
            }
        }
    }

    // What a scroll viewer shows of its content: the area of its content presenter, without the scroll bars.
    private static (ScrollViewer Scroller, Rect Area) ViewOf(ScrollViewer scroller, Visual root) =>
        (scroller, Where(scroller.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ScrollContentPresenter>().FirstOrDefault() ?? (Control)scroller, root));

    // The scroll viewer can bring the rectangle into view when it has more content than room on the axis where the rectangle is cut and does not forbid scrolling there.
    private static bool CanScroll(ScrollViewer scroller, Rect rect, Rect viewport)
    {
        var verticalCut = rect.Top < viewport.Top - 1 || rect.Bottom > viewport.Bottom + 1;
        var horizontalCut = rect.Left < viewport.Left - 1 || rect.Right > viewport.Right + 1;
        return (!verticalCut || scroller.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.Extent.Height > scroller.Viewport.Height + 1)
            && (!horizontalCut || scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.Extent.Width > scroller.Viewport.Width + 1);
    }

    /// <summary>Pairs of controls whose edges meet with no gap: the same line within a pixel and overlapping on the other axis.</summary>
    public static IEnumerable<string> Touching(Window window)
    {
        var items = Visible(window).Where(IsTarget).Where(c => c is not Slider).Select(c => (Control: c, Rect: Where(c, window))).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var (a, b) = (items[i].Rect, items[j].Rect);
                var gapX = Math.Max(a.Left, b.Left) - Math.Min(a.Right, b.Right);
                var gapY = Math.Max(a.Top, b.Top) - Math.Min(a.Bottom, b.Bottom);
                var overlapY = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
                var overlapX = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
                var side = gapX is > -0.5 and < 0.5 && overlapY > 2 || gapY is > -0.5 and < 0.5 && overlapX > 2;
                if (side) yield return $"{Label(items[i].Control)} touches {Label(items[j].Control)} at {Round(a)} and {Round(b)}";
            }
        }
    }

    private static readonly Type[] Boxed = [typeof(Button), typeof(ToggleButton), typeof(ComboBox), typeof(NumericUpDown), typeof(TextBox), typeof(DropDownButton)];

    private static bool Horizontal(Visual? panel) => panel is StackPanel { Orientation: Avalonia.Layout.Orientation.Horizontal } or WrapPanel { Orientation: Avalonia.Layout.Orientation.Horizontal };

    /// <summary>
    /// Controls side by side in a toolbar row (any nesting of horizontal stack and wrap panels) that do not sit on one level: their middles differ by more than a pixel,
    /// or boxed controls (buttons, toggles, combo boxes, spinners, text boxes) of no fixed height differ in height.
    /// </summary>
    public static IEnumerable<string> Misaligned(Window window)
    {
        var rows = new Dictionary<Visual, List<(Control Control, Rect Rect)>>();
        foreach (var control in Visible(window).Where(c => (IsTarget(c) || c is DropDownButton) && c is not (ListBox or CheckBox or RadioButton) && Horizontal(c.GetVisualParent())))
        {
            Visual row = control.GetVisualParent()!;
            while (Horizontal(row.GetVisualParent())) row = row.GetVisualParent()!;
            if (!rows.TryGetValue(row, out var list)) rows[row] = list = [];
            list.Add((control, Where(control, window)));
        }

        foreach (var (_, list) in rows)
        {
            // one line of the row: controls whose vertical extents overlap the first one left
            foreach (var line in list.GroupBy(c => list.First(o => o.Rect.Top < c.Rect.Bottom && c.Rect.Top < o.Rect.Bottom).Control))
            {
                var items = line.ToList();
                var middle = items.Select(i => i.Rect.Center.Y).ToList();
                if (middle.Max() - middle.Min() > 1.5) yield return $"not on one level: {string.Join(", ", items.Select(i => $"{Label(i.Control)} middle {i.Rect.Center.Y:0.#}"))}";
                var boxed = items.Where(i => Boxed.Any(t => t.IsInstanceOfType(i.Control)) && double.IsNaN(i.Control.Height)).ToList();
                if (boxed.Count > 1 && boxed.Max(i => i.Rect.Height) - boxed.Min(i => i.Rect.Height) > 1.5) yield return $"mixed heights: {string.Join(", ", boxed.Select(i => $"{Label(i.Control)} {i.Rect.Height:0.#}"))}";
            }
        }
    }

    /// <summary>Controls pressed against the top, left or right edge of the hosted view (closer than 2 pixels): a toolbar or a field needs an inset from the tab border.</summary>
    public static IEnumerable<string> TooCloseToEdge(Window window)
    {
        var client = new Rect(default, window.Bounds.Size);
        foreach (var visual in Visible(window).Where(c => IsTarget(c) && c is not (ListBox or Slider)))
        {
            var rect = Where(visual, window);
            if (rect.Top < 2 || rect.Left < 2 || client.Right - rect.Right < 2 && rect.Width < client.Width - 4) yield return $"{Label(visual)} is {Math.Min(rect.Top, rect.Left):0} px from the edge at {Round(rect)}";
        }
    }

    /// <summary>What the "..." drop-down of each overflow bar holds is reachable too: hosted at the width of its popup, none of it is cut off or squeezed. A drop-down that holds nothing must not show its button.</summary>
    public static async Task<List<string>> DropDowns(Window window)
    {
        var found = new List<string>();
        foreach (var bar in window.GetVisualDescendants().OfType<G3MStudio.Theme.OverflowBar>())
        {
            var holds = bar.Spill is ScrollViewer { Content: Panel { Children.Count: > 0 } };
            var onBar = bar.More.GetVisualParent() is not null; // a detached control counts as visible
            if (holds != onBar) found.Add($"the drop-down button of {bar.Name} is {(onBar ? "shown" : "missing")} with {(holds ? "" : "no ")}content");
            if (!holds) continue;
            var flyout = (Flyout)bar.More.Flyout!;
            var content = flyout.Content!;
            flyout.Content = null;
            var host = new Window { Content = content, Width = 440, Height = 520 };
            host.Show();
            await Settle();
            found.AddRange(Overflows(host).Concat(CutText(host)).Select(p => $"drop-down of {bar.Name}: {p}"));
            host.Content = null;
            host.Close();
            flyout.Content = content;
        }

        return found;
    }

    private static string Label(Control control) => $"{control.GetType().Name} '{control.Name}'{(control is ContentControl { Content: string text } ? " \"" + text + "\"" : "")}";

    private static string Round(Rect r) => $"({r.X:0},{r.Y:0},{r.Width:0},{r.Height:0})";
}
