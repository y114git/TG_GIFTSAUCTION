using Avalonia;
using Avalonia.Controls;

namespace G3MStudio.Docking;

/// <summary>
/// Hosts a panel at least <c>minWidth</c> by <c>minHeight</c> in size. In a smaller space (a small floating window, a low dock) the panel keeps that size and
/// scrolls, so its list keeps more than one row and its buttons are not cut off. In a larger space it fills it, as if it were not wrapped.
/// </summary>
internal sealed class MinSizeScroller : Decorator
{
    private readonly Filler _inner;

    public MinSizeScroller(Control content, double minWidth, double minHeight)
    {
        _inner = new Filler(minWidth, minHeight);
        // A new layout is filled before the old one is released: the panel leaves the old wrapper here.
        if (content.Parent is Filler { Parent: ScrollViewer { Parent: MinSizeScroller old } }) old.Release();
        _inner.Child = content;
        Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = _inner };
    }

    /// <summary>Lets go of the panel so another host can take it; a panel is one control for the whole session.</summary>
    public void Release() => _inner.Child = null;

    protected override Size MeasureOverride(Size availableSize)
    {
        _inner.Fit(availableSize);
        return base.MeasureOverride(availableSize);
    }

    // The panel gets an explicit size, never an infinite one, so it keeps filling a space it is given (a list scrolls itself) instead of growing to its content.
    // The scroll viewer measures its content without a limit and then arranges it at the space it has, so the panel is measured at the space of the host (Fit, at
    // least the minimum) and arranged at that same size; measuring it once at the minimum and again at the arranged size made every layout pass measure a tab's
    // whole panel twice. No property is set on the way, so an arrange that differs from the measure cannot invalidate the measure again.
    private sealed class Filler(double minWidth, double minHeight) : Decorator
    {
        private Size _space = new(minWidth, minHeight);

        public void Fit(Size available)
        {
            var space = new Size(double.IsInfinity(available.Width) ? minWidth : Math.Max(available.Width, minWidth), double.IsInfinity(available.Height) ? minHeight : Math.Max(available.Height, minHeight));
            if (space == _space) return;
            _space = space;
            InvalidateMeasure(); // the scroll viewer asks with the same unlimited size as before, so the panel would keep its old measure
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Child?.Measure(_space);
            return _space;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = new Size(Math.Max(finalSize.Width, minWidth), Math.Max(finalSize.Height, minHeight));
            // A host that arranges at another size than it measured (an unbounded parent) measures the panel again. Layout rounding alone never does: at a scaled UI
            // the measured 721.4 is arranged as 722, and measuring the panel again at 722 invalidates this control, whose measure puts it back at 721.4, so the layout
            // loops until the pass gives up and leaves the whole panel stale (a fold or a longer text that shows only after the window is resized).
            // shortcut: rounding differences are taken as anything under 4 px, which holds above a 50 percent UI scale; upgrade if a real host arranges within 4 px of its measure.
            if (Math.Abs(size.Width - _space.Width) >= 4 || Math.Abs(size.Height - _space.Height) >= 4) Child?.Measure(size);
            Child?.Arrange(new Rect(size));
            return size;
        }
    }
}
