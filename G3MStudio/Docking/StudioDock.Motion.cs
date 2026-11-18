using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Dock.Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Theme;

namespace G3MStudio.Docking;

/// <summary>Animation of the docks: folding and opening a dock, tabs sliding into place, and the content of a switched tab or panel fading in.</summary>
public sealed partial class StudioDock
{
    private static (double Across, double Down) Edge(IToolDock dock) => (dock.Id switch { "LeftDock" => -24.0, "RightDock" => 24.0, _ => 0.0 }, dock.Id == "BottomDock" ? 24.0 : 0.0);

    private Control? ViewOf(IToolDock dock) => _control?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => ReferenceEquals(c.DataContext, dock) && c.GetType().Name == "ToolDockControl");

    /// <summary>A dock that folds fades out and slides to its own edge, then gives up its share; false when it folds at once.</summary>
    private bool FoldAway(IToolDock dock, Action fold)
    {
        if (Motion.Instant || ViewOf(dock) is not { } view) return false;
        var (across, down) = Edge(dock);
        Motion.Leave(view, across, down, fold);
        return true;
    }

    /// <summary>Opening a dock: it fades in and slides from its own edge.</summary>
    private void FoldMotion(IToolDock dock)
    {
        if (Motion.Instant || dock.Proportion <= FoldedShare || _control is null) return;
        var (across, down) = Edge(dock);
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewOf(dock) is not { } view) return;
            Motion.Fold(view);
            Motion.Slide(view, across, down);
        }, DispatcherPriority.Loaded);
        AnimateTabs();
    }

    /// <summary>Closes a dockable without the fade (bulk closes and replacements); a tab that is still fading out is closed at once.</summary>
    private void CloseNow(IDockable dockable)
    {
        var was = _factory.Immediate;
        _factory.Immediate = true;
        try { Factory.CloseDockable(dockable); }
        finally { _factory.Immediate = was; }
    }

    private Control? TabItemOf(IDockable dockable) =>
        _control?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => ReferenceEquals(c.DataContext, dockable) && c is Dock.Avalonia.Controls.DocumentTabStripItem or Dock.Avalonia.Controls.ToolTabStripItem);

    /// <summary>A closing tab fades out on its own, then closes; the tabs after it slide into the gap (see <see cref="AnimateTabs"/>).</summary>
    private bool Leave(IDockable dockable, Action close)
    {
        if (Motion.Instant || TabItemOf(dockable) is not { } item) return false;
        Motion.Exit(item, close);
        // The close must run even when the fade never finishes (the item left the tree mid-fade): close runs once whichever comes first.
        DispatcherTimer.RunOnce(close, TimeSpan.FromMilliseconds(600));
        return true;
    }

    /// <summary>Lets the tab strips slide their tabs into place when one closes or moves. Safe to call often; a strip is followed once.</summary>
    private void AnimateTabs()
    {
        if (_control is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var strip in _control.GetVisualDescendants().OfType<TabStrip>())
                if (strip.ItemsPanelRoot is Panel panel) Motion.Follow(panel);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// A move made the library take a tab out of its strip and put it in again: the strip makes a new tab control for it, which must show at once what the old one showed.
    /// Tab fills fade only while the dock does not carry the class this sets; it is taken off after the strip has laid the new control out.
    /// </summary>
    private void Moved()
    {
        if (_control is not { } control) return;
        control.Classes.Add(MovingClass);
        Dispatcher.UIThread.Post(() => control.Classes.Remove(MovingClass), DispatcherPriority.Background);
    }

    /// <summary>A tab that was added grows and fades in at its place in the strip, once its item exists.</summary>
    private void OpenMotion(IDockable? dockable)
    {
        if (Motion.Instant || dockable is null || _factory.Moving) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (TabItemOf(dockable) is { } item) Motion.Open(item);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>The content of the tab or panel that became active fades in. The host control of a tab lives as long as the tab, so the dock shows no change by itself.</summary>
    private void SwitchFade(IDockable? dockable)
    {
        if (Motion.Instant || _factory.Moving) return;
        var content = dockable switch { Dock.Model.Avalonia.Controls.Document d => d.Content, Dock.Model.Avalonia.Controls.Tool t => t.Content, _ => null };
        if (content is Control view) Motion.Enter(view);
    }

    /// <summary>Puts the panel titles in the current language again (after a language change).</summary>
    public void RelabelPanels()
    {
        // Tools parked as hidden or pinned (auto-hide) are not in the visible tree but show their title once they come back.
        var parked = All(Layout).OfType<IRootDock>().SelectMany(r => (r.HiddenDockables ?? []).Concat(r.LeftPinnedDockables ?? []).Concat(r.RightPinnedDockables ?? []).Concat(r.TopPinnedDockables ?? []).Concat(r.BottomPinnedDockables ?? []));
        foreach (var tool in All(Layout).Concat(parked).OfType<Dock.Model.Avalonia.Controls.Tool>()) tool.Title = PanelTitle(tool.Id);
    }

    /// <summary>Docks a floated panel back at its old position in its old group; a group that is gone or a panel never docked takes it at the end of its default side.</summary>
    public void DockHome(IDockable panel)
    {
        if (panel.Owner is not IDock from) return;
        var home = ((StudioFactory)Factory).HomeOf(panel);
        // The standard layout puts Explorer and Search left, Inspector right, the rest below.
        var to = (home is { Owner.Id: { } owner } ? Docked<IToolDock>(owner) : null) ?? SideDock(panel.Id is "Explorer" or "Search" ? "LeftDock" : panel.Id == "Inspector" ? "RightDock" : "BottomDock");
        if (to is null) return;
        Factory.MoveDockable(from, to, panel, null);
        if (to.VisibleDockables is not { } list) return;
        var at = list.IndexOf(panel);
        if (at < 0) return;
        var wanted = home is { Index: >= 0 } known && known.Owner.Id == to.Id ? Math.Min(known.Index, list.Count - 1) : list.Count - 1;
        if (wanted != at)
        {
            list.RemoveAt(at);
            list.Insert(wanted, panel);
        }

        Factory.SetActiveDockable(panel);
    }

    /// <summary>A panel closed while it floats goes back to its old place instead of vanishing; false for a panel that does not float.</summary>
    private bool ReturnFloating(IDockable dockable)
    {
        if (dockable is not Dock.Model.Avalonia.Controls.Tool || !(Layout.Windows ?? []).Any(w => w.Layout is IDockable root && Docked(root).Contains(dockable))) return false;
        DockHome(dockable);
        return true;
    }

    /// <summary>
    /// A tab let go on the empty part of a tab strip joins that group at its end. The library floats a dragged tab unless it is dropped on a dock marker, and the empty strip
    /// beyond the last tab has none; this is asked by the factory before it floats a tab. False when the tab was not let go over a strip, and the library floats it.
    /// </summary>
    internal bool KeepInStrip(IDockable dockable)
    {
        if (dockable is not Dock.Model.Avalonia.Controls.Document || dockable.Owner is not IDock from || _control?.InputHitTest(_pointer) is not Visual hit) return false;
        if (hit.FindAncestorOfType<DocumentTabStripItem>(true) is not null || hit.FindAncestorOfType<DocumentTabStrip>(true) is not { DataContext: IDock target }) return false;
        if (from != target) Factory.MoveDockable(from, target, dockable, null);
        else if (from.VisibleDockables?.LastOrDefault() is { } last && last != dockable) Factory.MoveDockable(from, dockable, last);
        return true;
    }

    /// <summary>The window of a floating dock is closed: every panel in it goes back to its old place.</summary>
    internal void ReturnPanels(IDockWindow? window)
    {
        if (window?.Layout is not IDockable root) return;
        foreach (var tool in Docked(root).OfType<Dock.Model.Avalonia.Controls.Tool>().ToList()) DockHome(tool);
    }

    /// <summary>Closes the floating windows that no longer hold a panel or tab; runs after the library finished its own bookkeeping.</summary>
    private void CloseEmptyFloats() => Dispatcher.UIThread.Post(() =>
    {
        foreach (var floating in (Layout.Windows ?? []).ToList())
            if (floating.Layout is { } root && !All(root).Any(d => d is Dock.Model.Avalonia.Controls.Tool or Dock.Model.Avalonia.Controls.Document)) floating.Host?.Exit();
    }, DispatcherPriority.Background);

    // A floating window holds a copy of the dock its panel left, with the same id: only the docked tree counts as the home.
    private T? Docked<T>(string id) where T : class, IDockable => Docked(Layout).OfType<T>().FirstOrDefault(d => d.Id == id);

    // The side dock with the id; a layout restored without it (the user closed its last panel) gets it again at its usual place.
    private IToolDock? SideDock(string id)
    {
        if (Docked<IToolDock>(id) is { } have) return have;
        if ((id == "BottomDock" ? Docked<IDock>("Centre") : Docked<IDock>("Main")) is not { } parent) return null;
        var alignment = id switch { "LeftDock" => Alignment.Left, "RightDock" => Alignment.Right, _ => Alignment.Bottom };
        var created = new Dock.Model.Avalonia.Controls.ToolDock { Id = id, Alignment = alignment, Proportion = DefaultShare(id), VisibleDockables = Factory.CreateList<IDockable>() };
        var at = id == "LeftDock" ? 0 : parent.VisibleDockables?.Count ?? 0;
        var splitter = new Dock.Model.Avalonia.Controls.ProportionalDockSplitter();
        Factory.InsertDockable(parent, id == "LeftDock" ? created : splitter, at);
        Factory.InsertDockable(parent, id == "LeftDock" ? splitter : created, at + 1);
        LimitWidth(created);
        Balance();
        return created;
    }

    private sealed partial class StudioFactory
    {
        /// <summary>Plays the closing animation and calls the action when it ends; false when there is none and the close is done at once.</summary>
        public Func<IDockable, Action, bool>? Leave { get; init; }

        /// <summary>True while a bulk close runs: those close at once, only a single tab fades out.</summary>
        public bool Immediate { get; set; }

        /// <summary>Stops the fade of a closing dockable; its close then runs.</summary>
        public Action<IDockable>? Abort { get; init; }

        /// <summary>True while the library moves a dockable: it takes it out and puts it in again, which is no open and no switch, so no entrance or fade plays for it.</summary>
        public bool Moving { get; private set; }

        // A tab dragged to another place in its strip is moved in the list, not taken out and put in again: the strip keeps the tab controls, and nothing is made anew.
        public override void MoveDockable(IDock dock, IDockable sourceDockable, IDockable targetDockable)
        {
            if (dock.VisibleDockables is not Avalonia.Collections.AvaloniaList<IDockable> list || list.IndexOf(sourceDockable) is not (>= 0 and var from) || list.IndexOf(targetDockable) is not (>= 0 and var to) || from == to)
            {
                Move(() => base.MoveDockable(dock, sourceDockable, targetDockable));
                return;
            }

            Move(() =>
            {
                list.Move(from, to);
                OnDockableMoved(sourceDockable);
                dock.ActiveDockable = sourceDockable;
            });
        }

        public override void MoveDockable(IDock sourceDock, IDock targetDock, IDockable sourceDockable, IDockable? targetDockable) => Move(() => base.MoveDockable(sourceDock, targetDock, sourceDockable, targetDockable));

        private void Move(Action move)
        {
            var was = Moving;
            Moving = true;
            Home?.Moved();
            try { move(); }
            finally { Moving = was; }
        }

        // A dockable that is fading out, with the one-shot close that ends the fade (runs once, from the fade, the fallback timer or a bulk close).
        private readonly Dictionary<IDockable, Action> _closing = [];

        // Where a floated panel came from, by panel id: its group and its position there.
        private readonly Dictionary<string, (IDock Owner, int Index)> _homes = [];

        public (IDock Owner, int Index)? HomeOf(IDockable dockable) => dockable.Id is { } id && _homes.TryGetValue(id, out var home) ? home : null;

        public override void FloatDockable(IDockable dockable)
        {
            if (Home?.KeepInStrip(dockable) == true) return;
            if (dockable.Id is { } id && dockable.Owner is IDock owner && owner.VisibleDockables is { } list) _homes[id] = (owner, list.IndexOf(dockable));
            base.FloatDockable(dockable);
        }

        public override void CloseDockable(IDockable? dockable)
        {
            if (dockable is null)
            {
                base.CloseDockable(dockable);
                return;
            }

            if (Home?.ReturnFloating(dockable) == true) return;
            // A close the user asked for may be held back to ask about unapplied edits; the guard closes the tab again once it is answered.
            if (!Immediate && dockable.Id is { } guarded && Home?.CloseGuard?.Invoke(guarded) == false) return;

            // A second close of a tab that is still fading out (a double click on its x) must not remove it twice; a bulk close takes it out at once.
            if (_closing.TryGetValue(dockable, out var pending))
            {
                if (!Immediate) return;
                Abort?.Invoke(dockable);
                pending();
                return;
            }

            var finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                _closing.Remove(dockable);
                base.CloseDockable(dockable);
            }

            _closing[dockable] = Finish;
            if (!Immediate && Leave is { } leave && leave(dockable, Finish)) return;
            Finish();
        }
    }
}
