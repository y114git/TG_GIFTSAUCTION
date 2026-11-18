using Avalonia.Controls;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// Keeps a view in step with the data: after the history changed (an edit anywhere, an undo, a redo) it calls <c>refresh</c> once, on the dispatcher, however many changes came in the
/// same pass. A view that is hidden or detached does nothing until it is shown again, then refreshes if the data moved meanwhile. It listens only while it is on screen, so a closed
/// tab holds nothing alive. There is no timer: the history events are the only trigger.
/// </summary>
public sealed class HistoryFollow
{
    private readonly Control _view;
    private readonly EditHistory _history;
    private readonly Action _refresh;
    private int _seen;
    private bool _scheduled;
    private bool _listening;

    private HistoryFollow(Control view, EditHistory history, Action refresh)
    {
        (_view, _history, _refresh, _seen) = (view, history, refresh, history.Version);
        view.AttachedToVisualTree += (_, _) => Listen();
        view.DetachedFromVisualTree += (_, _) => Stop();
        if (view.IsAttachedToVisualTree()) Listen();
    }

    // The view and every ancestor: hiding or showing any of them changes whether the view is on screen. Flush decides once it runs.
    private readonly List<Visual> _watched = [];

    private void OnVisibility(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty) Schedule();
    }

    /// <summary>How many refreshes ran, so a test can tell that a change was coalesced or skipped.</summary>
    public int Runs { get; private set; }

    /// <summary>Starts following. The view refreshes by itself from now on; nothing needs to be kept.</summary>
    public static HistoryFollow Of(Control view, EditHistory history, Action refresh) => new(view, history, refresh);

    private void Listen()
    {
        if (_listening) return;
        _listening = true;
        foreach (var visual in _view.GetVisualAncestors().Prepend(_view))
        {
            visual.PropertyChanged += OnVisibility;
            _watched.Add(visual);
        }

        _history.Changed += OnChanged;
        if (_seen != _history.Version) Schedule(); // an edit came while the view was away
    }

    private void Stop()
    {
        if (!_listening) return;
        _listening = false;
        foreach (var visual in _watched) visual.PropertyChanged -= OnVisibility;
        _watched.Clear();
        _history.Changed -= OnChanged;
    }

    // The history raises Changed while the workspace is locked for writing; the refresh waits for the dispatcher, where reading is safe again.
    private void OnChanged() => Schedule();

    private void Schedule()
    {
        if (_scheduled || !_listening) return;
        _scheduled = true;
        Dispatcher.UIThread.Post(Flush, DispatcherPriority.Normal);
    }

    private void Flush()
    {
        _scheduled = false;
        // Hidden: the visibility change schedules this again.
        if (!_listening || !_view.IsEffectivelyVisible || _seen == _history.Version) return;
        var version = _history.Version;
        Runs++;
        try
        {
            _refresh();
            _seen = version; // a refresh that threw leaves the view stale, so the next change or show tries again
        }
        catch (Exception e)
        {
            FailureReport.Report(e, "history-follow");
        }
    }
}
