using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Localization;

namespace G3MStudio.Theme;

/// <summary>
/// A toolbar of control groups that never grows past <see cref="MaxRows"/> rows. The groups that do not fit, in order from the end, move into a "..." drop-down
/// at the end of the last row, so a narrow bar or a long language never eats the room of the view under it. Extra controls (a side column that gave way) join the drop-down too.
/// </summary>
public sealed class OverflowBar : StackPanel
{
    private readonly List<Control> _all = [];
    private readonly List<Control> _extras = [];
    private readonly StackPanel _spill = new() { Spacing = 6, Margin = new Thickness(8) };
    private readonly Flyout _flyout = new() { Placement = PlacementMode.BottomEdgeAlignedRight };
    private readonly Dictionary<Control, double> _natural = [];
    private bool _queued;

    public OverflowBar()
    {
        Spacing = 4;
        More.Bind(AutomationProperties.NameProperty, LocBinding.To("menu.more"));
        More.Bind(ToolTip.TipProperty, LocBinding.To("menu.more"));
        Spill = new ScrollViewer { MaxHeight = 480, MaxWidth = 440, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _spill };
        _flyout.Content = Spill;
        More.Flyout = _flyout;
        SizeChanged += (_, e) =>
        {
            if (e.WidthChanged) Queue();
        };
    }

    /// <summary>The most rows the bar uses; the rest goes to the drop-down.</summary>
    public int MaxRows
    {
        get;
        set
        {
            if (value == field) return;
            field = Math.Max(1, value);
            Queue();
        }
    } = 2;

    /// <summary>Every group, whether it is in a row or in the drop-down.</summary>
    public IReadOnlyList<Control> Groups => _all;

    /// <summary>The "..." button; it exists in the bar only while something is in the drop-down.</summary>
    public Button More { get; } = new() { Name = "ToolbarOverflow", Content = "…", VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The content of the drop-down: the groups that did not fit, then the extras.</summary>
    public Control Spill { get; }

    public bool IsSpilled(Control group) => group.Parent == _spill;

    public void Add(Control group)
    {
        _all.Add(group);
        // A row is as tall as its tallest group; the others sit in its middle, so every control of the row is on one level (a group stretched to the row put its controls at the top).
        group.VerticalAlignment = VerticalAlignment.Center;
        // Until the first layout reads the width, every group sits in one row, so the controls exist in the tree from the start.
        if (Children.Count == 0) Children.Add(NewRow());
        ((StackPanel)Children[0]).Children.Add(group);
        group.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty) Queue();
        };
        Queue();
    }

    /// <summary>Controls shown at the end of the drop-down only, such as a side column too narrow for the view. They leave their old parent first.</summary>
    public void SetExtras(IEnumerable<Control> extras)
    {
        var next = extras.ToList();
        if (next.SequenceEqual(_extras)) return;
        foreach (var old in _extras.Except(next)) _spill.Children.Remove(old); // the caller may put it back at once
        _extras.Clear();
        _extras.AddRange(next);
        Queue();
    }

    private void Queue()
    {
        if (_queued) return;
        _queued = true;
        Dispatcher.UIThread.Post(Arrange, DispatcherPriority.Loaded);
    }

    private void Release()
    {
        foreach (var row in Children.OfType<Panel>().ToList()) row.Children.Clear();
        Children.Clear();
        _spill.Children.Clear();
    }

    private static StackPanel NewRow() => new() { Orientation = Orientation.Horizontal, Spacing = 0 };

    private void Arrange()
    {
        _queued = false;
        var room = Bounds.Width;
        if (room <= 0) return;
        var shown = _all.Where(g => g.IsVisible).ToList();
        // A group in a row (not in the drop-down, which wraps it) has its natural width from the last layout; one that was never seen on a row (or was hidden) is measured in a row of its own.
        foreach (var group in shown.Where(g => g.GetVisualParent() is StackPanel row && row != _spill && g.DesiredSize.Width > 0)) _natural[group] = group.DesiredSize.Width;
        if (More.GetVisualParent() is not null && More.DesiredSize.Width > 0) _natural[More] = More.DesiredSize.Width;
        if (!_natural.ContainsKey(More) || shown.Any(g => !_natural.ContainsKey(g)))
        {
            Release();
            var probe = NewRow();
            foreach (var group in shown) probe.Children.Add(group);
            probe.Children.Add(More);
            Children.Add(probe);
            probe.Measure(Size.Infinity);
            foreach (var group in shown) _natural[group] = group.DesiredSize.Width;
            _natural[More] = More.DesiredSize.Width;
            Release();
        }

        var widths = shown.Select(g => _natural[g]).ToList();
        var (rows, spill) = Pack(shown, widths, room, 0);
        var needMore = spill.Count > 0 || _extras.Count > 0;
        if (needMore) (rows, spill) = Pack(shown, widths, room, _natural[More]);
        if (needMore && rows.Count == 0) rows.Add([]);
        // The same plan as now leaves the controls where they are, so a focused one keeps its focus while the window is dragged.
        var spilled = spill.Concat(_extras).ToList();
        var same = Children.Count == rows.Count && spilled.SequenceEqual(_spill.Children)
            && Children.OfType<Panel>().Select(r => r.Children.Where(c => c != More)).Zip(rows).All(z => z.First.SequenceEqual(z.Second))
            && (More.GetVisualParent() is not null) == needMore && (!needMore || More.GetVisualParent() == Children[^1]);
        if (same) return;
        Release();
        foreach (var line in rows)
        {
            var row = NewRow();
            foreach (var group in line) row.Children.Add(group);
            Children.Add(row);
        }

        if (needMore) ((StackPanel)Children[^1]).Children.Add(More);
        foreach (var group in spill) _spill.Children.Add(group);
        foreach (var extra in _extras)
        {
            (extra.Parent as Panel)?.Children.Remove(extra);
            (extra.Parent as Decorator)?.Child = null;
            _spill.Children.Add(extra);
        }
    }

    // Fills the rows in order; the first group that fits no row ends the fill, and it and every later group spill. The last row keeps `reserve` for the "..." button.
    private (List<List<Control>> Rows, List<Control> Spill) Pack(List<Control> groups, List<double> widths, double room, double reserve)
    {
        var rows = new List<List<Control>> { new() };
        var spill = new List<Control>();
        var used = 0.0;
        for (var i = 0; i < groups.Count; i++)
        {
            if (spill.Count > 0)
            {
                spill.Add(groups[i]);
                continue;
            }

            var limit = room - (rows.Count == MaxRows ? reserve : 0);
            if (used + widths[i] <= limit)
            {
                rows[^1].Add(groups[i]);
                used += widths[i];
            }
            else if (rows.Count < MaxRows && rows[^1].Count > 0)
            {
                rows.Add([]);
                used = 0;
                i--; // the same group again, now on the next row
            }
            else spill.Add(groups[i]);
        }

        if (rows[^1].Count == 0 && rows.Count > 1) rows.RemoveAt(rows.Count - 1);
        if (rows.Count == 1 && rows[0].Count == 0) rows.Clear();
        return (rows, spill);
    }
}
