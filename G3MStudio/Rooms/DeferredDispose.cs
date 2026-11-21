using Avalonia.Threading;

namespace G3MStudio.Rooms;

/// <summary>
/// Frees bitmaps a control may still draw. An Image that is closing or is about to be given another source is rendered once more by the compositor pass already queued,
/// and reading a disposed bitmap there throws on the UI thread. The release therefore waits until the pending render passes are done (Background runs after Render),
/// and by then the control has been detached or given its new source. Every place that disposes a bitmap an Image could show goes through here.
/// </summary>
public static class DeferredDispose
{
    public static void Release(params IDisposable?[] items) => Release((IEnumerable<IDisposable?>)items);

    public static void Release(IEnumerable<IDisposable?> items)
    {
        var list = items.OfType<IDisposable>().ToList();
        if (list.Count == 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var item in list) item.Dispose();
        }, DispatcherPriority.Background);
    }
}
