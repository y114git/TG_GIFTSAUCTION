using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Settings;

namespace G3MStudio.Theme;

/// <summary>
/// Sizes every dialog the same way (design/ux-standards.md, Sizing): it follows the interface scale, never grows past 90% of the screen's work area,
/// and opens at the size the user gave it last time. The main window sizes itself.
/// </summary>
public static class DialogSizing
{
    // Weak: the store belongs to a window, and a static strong reference would keep a closed window alive through its subscriptions.
    private static WeakReference<SettingsStore>? _store;
    private static bool _hooked;

    /// <summary>Tests set a fixed screen, so the automatic scale does not depend on the machine.</summary>
    public static ScreenInfo? ScreenOverride { get; set; }

    /// <summary>Tests watch which windows the scale computation reads a screen for.</summary>
    public static Action<WindowBase>? ScreenProbe { get; set; }

    /// <summary>Tests drop the store of an earlier run, which a leaked window may keep alive, so a new dialog is not scaled by someone else's settings.</summary>
    public static void Unhook() => _store = null;

    public static void Hook(SettingsStore store)
    {
        _store = new WeakReference<SettingsStore>(store);
        if (_hooked) return;
        _hooked = true;
        Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => Attach(w));
    }

    /// <summary>The primary screen of a window as the scale computation reads it; unknown when the window has no screen (headless).</summary>
    public static ScreenInfo ScreenOf(WindowBase window)
    {
        ScreenProbe?.Invoke(window);
        return ScreenOverride ?? ((window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary) is { } screen ? new ScreenInfo(screen.Bounds.Width, screen.Bounds.Height, screen.Scaling) : default);
    }

    private static double Scale(Window w, SettingsStore store) => UiSizing.Effective(store.Current.UiScale, ScreenOf(w)) / 100.0;

    private static string Key(Window w) => w.GetType() == typeof(Window) ? "Window:" + w.Title : w.GetType().Name;

    private static void Attach(Window w)
    {
        if (_store is null || !_store.TryGetTarget(out var store) || w is MainWindow || w.Content is LayoutTransformControl) return;
        var screen = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
        var scale = Scale(w, store);
        if (w.Content is Control child)
        {
            w.Content = null; // a control has one parent: it leaves the window before it joins the scaling host
            var host = new LayoutTransformControl { Child = child };
            w.Content = host;
            // A dialog's own Closed handler may save settings before this window's subscription is removed; its screen is gone by then.
            void Rescale() => host.LayoutTransform = new ScaleTransform(Scale(w, store), Scale(w, store));
            void Apply()
            {
                if (w.IsVisible) Rescale();
            }

            Rescale();
            store.Changed += Apply;
            w.Closed += (_, _) => store.Changed -= Apply;
        }

        if (store.Current.DialogSizes.TryGetValue(Key(w), out var saved) && w.CanResize && w.SizeToContent != SizeToContent.WidthAndHeight)
        {
            w.Width = saved.Width;
            if (w.SizeToContent == SizeToContent.Manual) w.Height = saved.Height;
        }
        else
        {
            if (!double.IsNaN(w.Width)) w.Width *= scale;
            if (!double.IsNaN(w.Height)) w.Height *= scale;
        }

        w.MinWidth *= scale;
        w.MinHeight *= scale;
        if (screen is not null)
        {
            w.MaxWidth = Math.Min(w.MaxWidth, screen.WorkingArea.Width * 0.9 / screen.Scaling);
            w.MaxHeight = Math.Min(w.MaxHeight, screen.WorkingArea.Height * 0.9 / screen.Scaling);
        }

        // Escape closes the dialog with its default (cancel) result; a handler that already used the key keeps it.
        w.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Escape || e.Handled || !w.IsVisible) return;
            e.Handled = true;
            DialogFrame.RequestClose(w);
        };

        // Only a size the user gave is kept: changes in the first moments are the dialog sizing itself.
        var settled = false;
        var resized = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => settled = true, Avalonia.Threading.DispatcherPriority.Background);
        w.SizeChanged += (_, _) =>
        {
            resized |= settled;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => KeepInWorkArea(w), Avalonia.Threading.DispatcherPriority.Loaded);
        };
        w.Closing += (_, _) => { if (resized) Remember(w, store); };
    }

    /// <summary>The top of a window of <paramref name="height"/> pixels that starts at <paramref name="top"/>, moved up so its bottom is not under the taskbar; never above the area.</summary>
    internal static int FitTop(int top, int height, int areaTop, int areaBottom) => top + height > areaBottom ? Math.Max(areaTop, areaBottom - height) : top;

    // A dialog that grows after it opened (the recorder shows its take) keeps its top, so its buttons could end up off the screen.
    private static void KeepInWorkArea(Window w)
    {
        if (!w.IsVisible || w.WindowState != WindowState.Normal || (w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary) is not { } screen) return;
        var height = (int)Math.Ceiling((w.FrameSize?.Height ?? w.Bounds.Height) * w.RenderScaling);
        var top = FitTop(w.Position.Y, height, screen.WorkingArea.Y, screen.WorkingArea.Bottom);
        if (top != w.Position.Y) w.Position = new PixelPoint(w.Position.X, top);
    }

    private static void Remember(Window w, SettingsStore store)
    {
        if (!w.CanResize || w.WindowState != WindowState.Normal || w.Bounds.Width < 1) return;
        var size = new DialogSize((int)Math.Round(w.Bounds.Width), (int)Math.Round(w.Bounds.Height));
        var key = Key(w);
        if (store.Current.DialogSizes.TryGetValue(key, out var old) && old == size) return;
        store.Update(s => s with { DialogSizes = new Dictionary<string, DialogSize>(s.DialogSizes) { [key] = size } });
    }
}
