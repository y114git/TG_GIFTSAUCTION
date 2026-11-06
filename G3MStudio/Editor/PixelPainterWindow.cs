using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The image editor in a window of its own: the same <see cref="ImageEditorView"/> as the document tab, on one picture. Apply hands the picture back as a PNG;
/// the caller makes the one undoable edit of the sprite.
/// </summary>
public sealed class PixelPainterWindow : Window
{
    public PixelPainterWindow(string title, PixelBuffer picture, Action<byte[], int, int> apply)
    {
        Title = title;
        Width = 1100;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        View = new ImageEditorView(new LocalImageSession(picture));
        Closed += (_, _) => View.Dispose();

        var ok = new Button { Name = "PaintApply", Content = Loc.Current["painter.apply"], Classes = { "primary" }, Margin = new Avalonia.Thickness(8, 0, 0, 0) };
        var cancel = new Button { Name = "PaintCancel", Content = Loc.Current["explorer.cancel"] };
        ok.Click += (_, _) =>
        {
            apply(FramePicture.ToPng(Canvas.Picture), Canvas.Picture.Width, Canvas.Picture.Height);
            Close(true);
        };
        cancel.Click += (_, _) => Close(false);
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(8), Children = { cancel, ok } };
        var layout = new DockPanel();
        DockPanel.SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        layout.Children.Add(bottom);
        layout.Children.Add(View);
        Content = layout;
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || e.Handled || e.Source is TextBox) return;
            Close(false);
            e.Handled = true;
        };
    }

    public ImageEditorView View { get; }

    public ImageCanvas Canvas => View.Canvas;
}
