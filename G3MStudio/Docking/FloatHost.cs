using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;

namespace G3MStudio.Docking;

/// <summary>The window of a floated panel; it opens with the size and place of the dock it left, so it is pulled back inside the screen.</summary>
internal sealed class FloatHost : HostWindow
{
    public FloatHost()
    {
        MinWidth = StudioDock.MinFloatWidth;
        MinHeight = StudioDock.MinFloatHeight;
    }

    // The main window closing asks its floating windows first; the library then closes the panels inside them, and the layout saved a moment later would have none.
    // Only the user closing the floating window itself empties it; the window then goes with the rest of the app and is saved as it was.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (e.CloseReason is WindowCloseReason.OwnerWindowClosing or WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            Window?.Save();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(KeepOnScreen, DispatcherPriority.Loaded);
    }

    private void KeepOnScreen()
    {
        if (!IsVisible || TryGetPlatformHandle() is null) return; // closed again before the posted call ran
        if ((Screens.ScreenFromWindow(this) ?? Screens.Primary)?.WorkingArea is not { } area) return;
        var scale = DesktopScaling;
        var size = PixelSize.FromSize(ClientSize, scale);
        if (size.Width <= 0 || size.Height <= 0) return;
        ClientSize = new Size(Math.Min(size.Width, area.Width) / scale, Math.Min(size.Height, area.Height) / scale);
        var fitted = PixelSize.FromSize(ClientSize, scale);
        Position = new PixelPoint(Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - fitted.Width)), Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - fitted.Height)));
    }
}
