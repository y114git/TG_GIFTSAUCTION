using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Explorer;

public partial class ExplorerView
{
    // Row positions of the type headings, in list order; a binary search finds the heading above the first visible row.
    private int[] _headings = [];

    /// <summary>The type heading pinned at the top of the list, or null when none is shown.</summary>
    public ExplorerRow? StickyRow { get; private set; }

    private void WireSticky()
    {
        List.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => RefreshSticky(), Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        List.SizeChanged += (_, _) => RefreshSticky();
        // A scroll by the user ends the checks of the last reveal, so they cannot move the list back.
        List.AddHandler(Avalonia.Input.InputElement.PointerWheelChangedEvent, (_, _) => _reveal++, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        List.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => _reveal++, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        // A right click on the pinned heading opens the menu of that heading, as on the heading in the list.
        Sticky.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(Sticky).Properties.IsRightButtonPressed || StickyRow is not { } pinned) return;
            // The heading is selected for the menu's commands, but the list stays where it is: the heading in it is far above. Turning the follow
            // back on scrolls to the selection at once, so it waits until the selection moves on to another row.
            if (List.AutoScrollToSelectedItem)
            {
                List.AutoScrollToSelectedItem = false;
                void Resume(object? sender, SelectionChangedEventArgs args)
                {
                    List.SelectionChanged -= Resume;
                    List.AutoScrollToSelectedItem = true;
                }

                List.SelectedItem = _rows.FirstOrDefault(r => r.Equals(pinned)) ?? List.SelectedItem;
                List.SelectionChanged += Resume;
            }
            else List.SelectedItem = _rows.FirstOrDefault(r => r.Equals(pinned)) ?? List.SelectedItem;

            _reveal++; // the selection made here queued a check that would scroll the heading out from under its pinned copy
            MenuRequested?.Invoke(pinned, Sticky);
            e.Handled = true;
        };
        Sticky.Tapped += (_, _) =>
        {
            if (StickyRow is not { } row) return;
            Model.Toggle(row.Key);
            G3MStudio.Theme.Motion.Fold(List);
            // Folding from deep inside a long category would leave the list somewhere below; bring the heading back to the top.
            Dispatcher.UIThread.Post(() =>
            {
                var at = Array.FindIndex(_headings, i => _rows[i].Key == row.Key);
                if (at < 0) return;
                List.UpdateLayout(); // the shrunken extent must be measured first, or the offset is clamped over the scroll
                G3MStudio.Theme.Motion.ScrollTo(List, _headings[at]);
            }, DispatcherPriority.Loaded);
        };
    }

    private bool _stickyQueued;

    // Counts the reveals and the scrolls by the user; a check made for an older count is dropped.
    private int _reveal;

    // Containers are not arranged yet when the scroll event fires: update now for the common case and once more after layout.
    private void RefreshSticky()
    {
        UpdateSticky();
        if (_stickyQueued) return;
        _stickyQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _stickyQueued = false;
            UpdateSticky();
        }, DispatcherPriority.Loaded);
    }

    // Group rows (Quick Access, the data group) are headings too: their own rows (the data items) would otherwise sit under the copy of the last type above them.
    private void IndexHeadings() => _headings = [.. _rows.Select((r, i) => (r, i)).Where(t => t.r.IsHeader).Select(t => t.i)];

    private void ShowSelected()
    {
        var count = List.SelectedItems?.OfType<ExplorerRow>().Count(r => r.IsItem) ?? 0; // a heading picked by a range is no item
        SelectedCount.IsVisible = count > 0;
        if (count > 0) SelectedCount.Text = Loc.Current.Format("explorer.selected", ("count", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("n", count));
    }

    // The pinned heading overlays the top row; a selection moved there by the keyboard is scrolled down below it.
    private void KeepSelectedClearOfSticky()
    {
        if (List.SelectedIndex < 0) return;
        var reveal = ++_reveal;
        Dispatcher.UIThread.Post(() => { if (reveal == _reveal) ClearSelectedOfSticky(); }, DispatcherPriority.Loaded);
        // A reveal from far away scrolls in several layout passes (the row is not realized at first), so the check is made again at the layout that realizes the row.
        void Realized(object? sender, EventArgs e)
        {
            if (reveal == _reveal && List.SelectedIndex >= 0 && List.ContainerFromIndex(List.SelectedIndex) is null) return;
            List.LayoutUpdated -= Realized;
            if (reveal == _reveal) ClearSelectedOfSticky();
        }

        List.LayoutUpdated += Realized;
    }

    private void ClearSelectedOfSticky()
    {
        UpdateSticky();
        if (List.SelectedIndex < 0 || !Sticky.IsVisible || List.ContainerFromIndex(List.SelectedIndex) is not { } row || row.TranslatePoint(default, List) is not { } top) return;
        var hidden = Sticky.Bounds.Height + _push - top.Y;
        if (hidden > 0 && List.Scroll is ScrollViewer scroll) scroll.Offset = scroll.Offset.WithY(Math.Max(0, scroll.Offset.Y - hidden));
    }

    /// <summary>Index of the row at the top edge of the list (the first one any part of which shows), or -1.</summary>
    private int FirstVisible()
    {
        var first = -1;
        foreach (var container in List.GetRealizedContainers())
        {
            var index = List.IndexFromContainer(container);
            if (index < 0 || first >= 0 && index > first) continue;
            if (TopOf(container) + container.Bounds.Height > 0) first = index;
        }

        return first;
    }

    // How far the pinned heading is pushed up by the next heading coming in under it (0 or less).
    private double _push;

    // Layout position relative to the viewport; unlike TranslatePoint it ignores the slide animation of a fold, which would leave the pinned heading stale.
    private double TopOf(Control container) => InList(container) - (List.Scroll is Visual scroll ? InList(scroll) : 0);

    // Layout position relative to the list (and the pinned heading, which starts at the same place). It sums the arranged positions, so it includes the
    // scroll offset as layout rounding placed it on screen (a whole pixel), not the fractional offset the scroll viewer reports.
    private double InList(Visual visual)
    {
        var y = 0.0;
        for (var at = visual; at is not null && at != List; at = at.GetVisualParent()) y += at.Bounds.Y;
        return y;
    }

    // The height the pinned heading gets after layout rounding.
    private double StickyHeight => Avalonia.Layout.LayoutHelper.RoundLayoutValue(RowHeight, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);

    // The interface scale makes row edges fall inside device pixels. The copy ends at the next whole pixel row, so the pixel row it shares with
    // the next heading shows no anti-aliased edge of the row under it.
    private double CeilToDevicePixel(double y)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || List.TransformToVisual(top) is not { } m || m.M22 <= 0) return y;
        var scale = m.M22 * top.RenderScaling;
        var origin = m.M32 * top.RenderScaling;
        return (Math.Ceiling(origin + y * scale - 0.01) - origin) / scale;
    }

    private void UpdateSticky()
    {
        ExplorerRow? row = null;
        var push = 0.0;
        var first = FirstVisible();
        if (first >= 0 && _headings.Length > 0)
        {
            var at = Array.BinarySearch(_headings, first);
            // The heading is the row itself when it is one, else the nearest one above it that holds it (a folder, subfolder, collection, group or type).
            var heading = at;
            if (heading < 0 && first < _rows.Count)
                for (var i = ~at - 1; i >= 0; i--)
                    if (_rows[_headings[i]].Depth < _rows[first].Depth) { heading = i; break; }
            // A heading flush at the top needs no copy; any other row at the top is covered by the copy of its heading.
            var flush = at >= 0 && List.ContainerFromIndex(first) is { } c && TopOf(c) >= -0.01;
            if (heading >= 0 && !flush && _headings[heading] < _rows.Count)
            {
                row = _rows[_headings[heading]];
                // The next heading pushes the copy up instead of sliding under it, so no strip of the last rows of this section shows above it.
                if (heading + 1 < _headings.Length && List.ContainerFromIndex(_headings[heading + 1]) is { } next) push = Math.Min(0, CeilToDevicePixel(InList(next)) - StickyHeight);
            }
        }

        if (push != _push || Sticky.RenderTransform is null)
        {
            _push = push;
            Sticky.RenderTransform = new Avalonia.Media.TranslateTransform(0, push);
        }

        var count = row?.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        // The same heading object can show new values after a rebuild (fold state, count, thumbnails on or off).
        if (ReferenceEquals(row, StickyRow) && (row is null || Sticky.IsVisible && StickyText.Text == row.Text && StickyChevron.Text == row.Chevron && StickyCount.Text == count && StickyCount.IsVisible == row.HasCount && StickyThumb.IsVisible == Thumbnails)) return;
        StickyRow = row;
        Sticky.IsVisible = row is not null;
        if (row is null) return;
        Sticky.Height = RowHeight;
        Sticky.Padding = new Thickness(8 + row.IndentDepth * 14, 0, 8, 0);
        StickyChevron.Text = row.Chevron;
        StickyText.Text = row.Text;
        StickyCount.Text = count;
        StickyCount.IsVisible = row.HasCount;
        StickyThumb.IsVisible = Thumbnails;
        StickyThumb.Width = StickyThumb.Height = ThumbSide;
        StickyThumb.Source = Thumbnails ? ResourceIcons.Of(row.Key, IconColour) : null;
    }
}
