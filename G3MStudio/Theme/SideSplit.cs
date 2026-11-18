using Avalonia.Controls;
using Avalonia.Layout;

namespace G3MStudio.Theme;

/// <summary>A side column the user can drag wider or narrower: its width is remembered by key until the app closes, and a hidden side takes no room.</summary>
public sealed record SideColumn(Control Content, string Key, double Width, double Min, bool OnLeft = false);

public static class SideSplit
{
    private static readonly Dictionary<string, double> Widths = [];

    /// <summary>The widths dragged so far, by key.</summary>
    public static IReadOnlyDictionary<string, double> Remembered => Widths;

    // The state of one side: its column, and the width it wants (dragged to, or the default) whatever the window leaves it right now.
    private sealed class State(SideColumn side)
    {
        public SideColumn Side { get; } = side;
        public double Wanted { get; set; } = Widths.GetValueOrDefault(side.Key, side.Width);
        public ColumnDefinition Column { get; } = new(Widths.GetValueOrDefault(side.Key, side.Width), GridUnitType.Pixel) { MinWidth = side.Min };
    }

    public static Grid Around(Control centre, params SideColumn[] sides) => Around(centre, 0, sides);

    /// <summary>The centre in the star column with each side beside it behind a splitter; a side shown or hidden through IsVisible gives its room back. The centre keeps at least <paramref name="centreShare"/> of the width (0 to 1) while the sides' minimums allow it.</summary>
    public static Grid Around(Control centre, double centreShare, params SideColumn[] sides)
    {
        var grid = new Grid();
        var states = sides.ToDictionary(s => s, s => new State(s));
        var left = sides.Where(s => s.OnLeft).ToList();
        var right = sides.Where(s => !s.OnLeft).ToList();
        var order = new List<(SideColumn? Side, bool Splitter)>();
        foreach (var side in left)
        {
            order.Add((side, false));
            order.Add((side, true));
        }

        order.Add((null, false));
        foreach (var side in right)
        {
            order.Add((side, true));
            order.Add((side, false));
        }

        // A side column is a fixed pixel width: in a narrow window it gives way so the centre keeps its minimum, and it grows back toward its wanted width when room returns.
        void Fit()
        {
            var visible = states.Values.Where(st => st.Side.Content.IsVisible).ToList();
            var room = grid.Bounds.Width - Math.Max(120, centreShare * grid.Bounds.Width) - 5 * visible.Count;
            if (visible.Count == 0 || room <= 0) return;
            // Every side keeps its minimum first; what is left over is shared in proportion to how much more each side wants.
            var totalMin = visible.Sum(st => st.Side.Min);
            var wantExtra = visible.Sum(st => Math.Max(0, st.Wanted - st.Side.Min));
            var share = wantExtra <= 0 ? 0 : Math.Min(1, Math.Max(0, room - totalMin) / wantExtra);
            foreach (var st in visible)
            {
                var target = st.Side.Min + Math.Max(0, st.Wanted - st.Side.Min) * share;
                if (Math.Abs(st.Column.Width.Value - target) > 0.5 || st.Column.Width.GridUnitType != GridUnitType.Pixel) st.Column.Width = new GridLength(target);
            }
        }

        for (var i = 0; i < order.Count; i++)
        {
            var (side, splitter) = order[i];
            if (side is null)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 120 });
                Grid.SetColumn(centre, i);
                grid.Children.Add(centre);
            }
            else if (splitter) AddSplitter(grid, states[side], i, Fit);
            else AddSide(grid, states[side], i, Fit);
        }

        grid.SizeChanged += (_, _) => Fit();
        return grid;
    }

    private static void AddSide(Grid grid, State state, int index, Action fit)
    {
        var side = state.Side;
        var column = state.Column;
        grid.ColumnDefinitions.Add(column);
        Grid.SetColumn(side.Content, index);
        grid.Children.Add(side.Content);
        side.Content.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Visual.IsVisibleProperty) return;
            if (side.Content.IsVisible)
            {
                (column.MinWidth, column.Width) = (side.Min, new GridLength(state.Wanted));
                fit();
            }
            else (column.MinWidth, column.Width) = (0, new GridLength(0));
        };
        if (!side.Content.IsVisible) (column.MinWidth, column.Width) = (0, new GridLength(0));
    }

    private static void AddSplitter(Grid grid, State state, int index, Action fit)
    {
        var side = state.Side;
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var splitter = new GridSplitter { Width = 5, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns, IsVisible = side.Content.IsVisible };
        Grid.SetColumn(splitter, index);
        grid.Children.Add(splitter);
        side.Content.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Visual.IsVisibleProperty) splitter.IsVisible = side.Content.IsVisible;
        };
        // The width the user dragged to is the one remembered and the one the side grows back to.
        splitter.DragCompleted += (_, _) =>
        {
            var column = grid.ColumnDefinitions[side.OnLeft ? index - 1 : index + 1];
            if (column.ActualWidth <= 0) return;
            Widths[side.Key] = state.Wanted = column.ActualWidth;
            fit();
        };
    }
}
