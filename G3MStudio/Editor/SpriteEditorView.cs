using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The whole sprite tab header: playback and frame strip on top, then one zoomable canvas with the tools panel beside it when the tab is wide
/// and under it when narrow. The canvas never gets less than <see cref="MinCanvasHeight"/>; what does not fit scrolls.
/// </summary>
public sealed class SpriteEditorView : ScrollViewer
{
    public const double MinCanvasHeight = 260;
    public const double WideWidth = 900;
    private const double PanelWidth = 420;
    private double ZoomBarHeight => this.Size("Control.Height", 30) + 6; // the canvas below it keeps the full minimum

    private readonly Grid _body = new();
    private readonly Grid _canvasColumn = new() { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };

    public SpriteEditorView(GameMakerSprite sprite, TextureBitmaps textures, Workspace workspace)
    {
        Name = "SpriteEditor";
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Frames = new SpriteFramesView(sprite, textures, workspace.History);
        Tools = new SpriteToolsView(sprite, textures, workspace);
        var zoomOut = new Button { Name = "SpriteZoomOut", Content = "-", MinWidth = 32 }.Labeled("rooms.zoomOut");
        var zoomIn = new Button { Name = "SpriteZoomIn", Content = "+", MinWidth = 32 }.Labeled("rooms.zoomIn");
        var zoomText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Classes = { "muted" } };
        void Zoom(int by)
        {
            Tools.Canvas.Zoom += by;
            zoomText.Text = $"{Tools.Canvas.Zoom}x";
        }

        zoomOut.Click += (_, _) => Zoom(-1);
        zoomIn.Click += (_, _) => Zoom(1);
        zoomText.Text = $"{Tools.Canvas.Zoom}x";
        var viewer = new ScrollViewer { Name = "SpriteCanvasHost", HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = Tools.Canvas };
        viewer.PointerWheelChanged += (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            Zoom(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        };
        var zoomBar = new StackPanel { Height = ZoomBarHeight, Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 0), Children = { zoomOut, zoomText, zoomIn } };
        Grid.SetRow(viewer, 1);
        _canvasColumn.Children.Add(zoomBar);
        _canvasColumn.Children.Add(viewer);
        _body.Children.Add(_canvasColumn);
        _body.Children.Add(Tools);
        Content = new StackPanel { Children = { Frames, _body } };
        Frames.FrameChosen += Tools.ShowFrame;
        ArrangeParts(default);
        SizeChanged += (_, e) => ArrangeParts(e.NewSize);
        Frames.SizeChanged += (_, _) => ArrangeParts(Bounds.Size);
    }

    public SpriteFramesView Frames { get; }

    public SpriteToolsView Tools { get; }

    /// <summary>True when the tools panel sits right of the canvas.</summary>
    public bool IsWide { get; private set; }

    private void ArrangeParts(Size size)
    {
        IsWide = size.Width > WideWidth;
        _body.ColumnDefinitions.Clear();
        _body.RowDefinitions.Clear();
        var room = size.Height - Frames.Bounds.Height;
        if (IsWide)
        {
            _body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _body.ColumnDefinitions.Add(new ColumnDefinition(PanelWidth, GridUnitType.Pixel));
            Grid.SetColumn(Tools, 1);
            Grid.SetRow(Tools, 0);
            _canvasColumn.Height = Math.Max(MinCanvasHeight + ZoomBarHeight, room);
        }
        else
        {
            _body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(Tools, 0);
            Grid.SetRow(Tools, 1);
            _canvasColumn.Height = Math.Max(MinCanvasHeight + ZoomBarHeight, room * 0.6);
        }
    }
}
