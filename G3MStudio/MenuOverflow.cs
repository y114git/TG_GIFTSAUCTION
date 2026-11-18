using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// Owns the top-level items of a menu bar. The items that do not fit the width go, in order from the end, into a "..." menu at the right
/// end, so no menu is cut off at a narrow window (the Avalonia menu bar clips instead).
/// </summary>
internal sealed class MenuOverflow
{
    private readonly Menu _menu;
    private readonly List<MenuItem> _all = [];
    private readonly MenuItem _more = new() { Name = "MenuOverflow", Header = "…" };
    private bool _queued;

    public MenuOverflow(Menu menu)
    {
        _menu = menu;
        _more.Bind(AutomationProperties.NameProperty, LocBinding.To("menu.more"));
        _more.Bind(ToolTip.TipProperty, LocBinding.To("menu.more"));
        _menu.SizeChanged += (_, e) =>
        {
            if (e.WidthChanged) Queue();
        };
    }

    public int Count => _all.Count;

    /// <summary>Every top-level item, whether it is in the bar or in the overflow menu.</summary>
    public IReadOnlyList<MenuItem> Items => _all;

    public void Insert(int index, MenuItem item)
    {
        _all.Insert(index, item);
        item.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty || e.Property == MenuItem.HeaderProperty) Queue();
        };
        _menu.Items.Insert(Math.Min(index, _menu.Items.Count), item);
        Queue();
    }

    public void Add(MenuItem item) => Insert(_all.Count, item);

    private void Queue()
    {
        if (_queued) return;
        _queued = true;
        Dispatcher.UIThread.Post(Arrange, DispatcherPriority.Loaded);
    }

    private void Arrange()
    {
        _queued = false;
        if (_menu.Bounds.Width <= 0) return;
        // All items back in the bar, to read their natural widths.
        _more.Items.Clear();
        _menu.Items.Clear();
        foreach (var item in _all) _menu.Items.Add(item);
        _more.IsVisible = true;
        _menu.Items.Add(_more);
        _menu.Measure(Size.Infinity);
        var total = _all.Where(i => i.IsVisible).Sum(i => i.DesiredSize.Width);
        var room = _menu.Bounds.Width - (_menu.DesiredSize.Width - total - _more.DesiredSize.Width); // the bar has padding of its own
        if (total <= room)
        {
            _menu.Items.Remove(_more);
            return;
        }

        var budget = room - _more.DesiredSize.Width;
        var used = 0.0;
        var split = _all.Count;
        for (var at = 0; at < _all.Count; at++)
        {
            used += _all[at].IsVisible ? _all[at].DesiredSize.Width : 0;
            if (used <= budget) continue;
            split = at;
            break;
        }

        // A menu that never shows (a context menu out of context) stays where it is; the cut starts at the first visible item that does not fit.
        foreach (var item in _all.Skip(split).ToList())
        {
            _menu.Items.Remove(item);
            _more.Items.Add(item);
        }
    }
}
