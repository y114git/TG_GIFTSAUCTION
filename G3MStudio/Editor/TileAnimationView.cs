using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>Draws the index of every tile over the tile set picture, in picture pixels times the zoom of the view it sits on.</summary>
public sealed class TileIndexOverlay(GameMakerBackground set, Func<double> zoom) : Control
{
    public override void Render(DrawingContext context)
    {
        var scale = zoom();
        var total = (int)Math.Min(set.GMS2TileCount, 16384);
        for (var i = 0; i < total; i++)
        {
            var (x, y, _, h) = TileGeometry.RectOf(set, i);
            if (x * scale > Bounds.Width || y * scale > Bounds.Height) continue;
            var text = new FormattedText(i.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, Math.Clamp(h * scale / 3, 8, 14), Brushes.White);
            var at = new Point(x * scale + 1, y * scale + 1);
            context.DrawRectangle(new SolidColorBrush(Colors.Black, 0.55), null, new Rect(at, new Size(text.Width + 2, text.Height)));
            context.DrawText(text, at + new Point(1, 0));
        }
    }
}

/// <summary>
/// The animated tiles of a tile set: a row per tile with its frame ids, a box to retype the frames of the picked tile as a comma list, and the
/// frame length as frames per second. Each change is one undoable edit.
/// </summary>
public sealed class TileAnimationView : StackPanel
{
    private readonly GameMakerBackground _set;
    private readonly Workspace _workspace;
    private readonly ListBox _rows = new() { Name = "TileAnimationRows", MaxHeight = 160 };
    private readonly TextBox _frames = new() { Name = "TileAnimationFrames", MinWidth = 200 };
    private readonly NumericUpDown _fps = new() { Name = "TileAnimationFps", Minimum = 1, Maximum = 1000, FormatString = "0.##", Width = 110 };
    private readonly TextBlock _message = new() { Name = "TileAnimationMessage", Classes = { "muted" } };
    private bool _building;

    public TileAnimationView(GameMakerBackground set, Workspace workspace)
    {
        (_set, _workspace) = (set, workspace);
        Name = "TileAnimationView";
        Spacing = 4;
        var apply = new Button { Name = "TileAnimationApply", Content = Loc.Current["editors.tiles.apply"] };
        apply.Click += (_, _) => ApplyFrames();
        Children.Add(new TextBlock { Text = Loc.Current["editors.tiles.animated"], FontWeight = FontWeight.SemiBold });
        Children.Add(_rows);
        Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["editors.tiles.frames"], VerticalAlignment = VerticalAlignment.Center }, _frames, apply } });
        Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["editors.tiles.speed"], VerticalAlignment = VerticalAlignment.Center }, _fps } });
        Children.Add(_message);
        _rows.SelectionChanged += (_, _) => ShowSelected();
        _fps.ValueChanged += (_, _) => ApplySpeed();
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    /// <summary>Frames per second of a frame length in microseconds.</summary>
    public static double FramesPerSecond(long microseconds) => microseconds <= 0 ? 0 : 1_000_000.0 / microseconds;

    private int Items => (int)Math.Max(1, _set.GMS2ItemsPerTileCount);

    private List<uint[]> Table => TileAnimationCommand.Rows([.. _set.GMS2TileIds.Select(t => t.ID)], Items) ?? [];

    private void Reload()
    {
        _building = true;
        var table = Table;
        IsVisible = table.Count > 0;
        var picked = _rows.SelectedIndex;
        _rows.ItemsSource = table.Select((r, i) => string.Create(CultureInfo.InvariantCulture, $"{i}: {string.Join(", ", r)}")).ToList();
        _rows.SelectedIndex = picked < table.Count ? picked : -1;
        _fps.Value = (decimal)Math.Round(FramesPerSecond(_set.GMS2FrameLength), 2);
        _building = false;
        ShowSelected();
    }

    private void ShowSelected()
    {
        if (_building) return;
        var table = Table;
        _frames.Text = _rows.SelectedIndex is >= 0 and var i && i < table.Count ? string.Join(", ", table[i].Distinct()) : "";
    }

    private void ApplyFrames()
    {
        if (_rows.SelectedIndex < 0) return;
        try
        {
            var frames = (_frames.Text ?? "").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList();
            _workspace.Edit(new TileAnimationCommand(_set.Name.Content ?? "", TileAnimationCommand.Frames, _rows.SelectedIndex, frames));
            _message.Text = "";
        }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidOperationException)
        {
            _message.Text = e.Message;
        }
    }

    private void ApplySpeed()
    {
        if (_building || _fps.Value is not { } fps || fps <= 0) return;
        var length = (long)Math.Round(1_000_000.0 / (double)fps);
        if (length != _set.GMS2FrameLength) _workspace.Edit(new TileAnimationCommand(_set.Name.Content ?? "", TileAnimationCommand.Speed, length: length));
    }
}
