using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
using G3MStudio.Core.Settings;

namespace G3MStudio.Theme;

/// <summary>
/// Single-element transitions on the render clock (opacity and render transforms only, no layout). One run per control: a new run cancels the old one,
/// and a cancelled run hands back the plain values, so an interrupted control is never left half transparent or shifted.
/// </summary>
public static partial class Motion
{
    private static readonly ConditionalWeakTable<Control, CancellationTokenSource> Runs = [];
    private static readonly HashSet<CancellationTokenSource> Live = [];
    private static readonly ConditionalWeakTable<Panel, Action> Followed = [];

    // The render transform is driven by three plain numbers, because Avalonia has no public animator for the transform itself.
    private static readonly AttachedProperty<double> ShiftXProperty = AvaloniaProperty.RegisterAttached<Control, Control, double>("ShiftX");
    private static readonly AttachedProperty<double> ShiftYProperty = AvaloniaProperty.RegisterAttached<Control, Control, double>("ShiftY");
    private static readonly AttachedProperty<double> GrowProperty = AvaloniaProperty.RegisterAttached<Control, Control, double>("Grow", 1.0);
    private static readonly AttachedProperty<double> GrowXProperty = AvaloniaProperty.RegisterAttached<Control, Control, double>("GrowX", 1.0);

    static Motion()
    {
        static void Apply(Control c)
        {
            double x = c.GetValue(ShiftXProperty), y = c.GetValue(ShiftYProperty), g = c.GetValue(GrowProperty), gx = c.GetValue(GrowXProperty);
            if (x == 0 && y == 0 && g == 1 && gx == 1)
            {
                c.ClearValue(Visual.RenderTransformProperty);
                return;
            }

            var b = TransformOperations.CreateBuilder(2);
            if (g != 1 || gx != 1) b.AppendScale(g * gx, g);
            b.AppendTranslate(x, y);
            c.RenderTransform = b.Build();
        }

        ShiftXProperty.Changed.AddClassHandler<Control>((c, _) => Apply(c));
        ShiftYProperty.Changed.AddClassHandler<Control>((c, _) => Apply(c));
        GrowProperty.Changed.AddClassHandler<Control>((c, _) => Apply(c));
        GrowXProperty.Changed.AddClassHandler<Control>((c, _) => Apply(c));
    }

    // shortcut: a panel with more children than this is not followed, so a big list never pays for it; raise it if tab strips grow past it.
    private const int FollowLimit = 40;

    /// <summary>Animations running right now; zero when Off and after every run has finished or was cancelled.</summary>
    public static int Active => Live.Count;

    /// <summary>The last animation that failed with an error (for example a property without an animator), or null.</summary>
    public static Exception? LastFailure { get; private set; }

    /// <summary>Runs that were cancelled before they finished.</summary>
    public static int Cancelled { get; private set; }

    /// <summary>Ends every running animation at once (their values are handed back and their exits complete).</summary>
    public static void CancelAll()
    {
        foreach (var cts in Live.ToList()) cts.Cancel();
        // A run whose control is gone (its window closed, its clock stopped) never sees the cancel: it is not active any more either way.
        Live.Clear();
    }

    /// <summary>Stops the running animation of one control.</summary>
    public static void Cancel(Control control)
    {
        if (Runs.TryGetValue(control, out var cts)) cts.Cancel();
    }

    /// <summary>Fades a control out, then calls <paramref name="done"/> (always once, at once when animations are instant).</summary>
    public static void Exit(Control control, Action done)
    {
        if (Instant)
        {
            done();
            return;
        }

        control.IsHitTestVisible = false;
        Play(control, Fade(1, 0, 110), () =>
        {
            done(); // closes first: the control may be reused by its strip, so it is made clickable only after the close ran
            control.IsHitTestVisible = true;
        });
    }

    /// <summary>A panel leaving: fades out and, when movement is allowed, slides by the offset; then calls <paramref name="done"/> (at once when instant).</summary>
    public static void Leave(Control control, double toX, double toY, Action done)
    {
        if (Instant)
        {
            done();
            return;
        }

        var from = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 1.0) } };
        var to = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0.0) } };
        if (MotionPolicy.AllowsMovement(Level))
        {
            from.Setters.Add(new Setter(ShiftXProperty, 0.0));
            from.Setters.Add(new Setter(ShiftYProperty, 0.0));
            to.Setters.Add(new Setter(ShiftXProperty, toX));
            to.Setters.Add(new Setter(ShiftYProperty, toY));
        }

        control.IsHitTestVisible = false;
        Play(control, new Animation { Duration = Ms(140), Easing = new CubicEaseIn(), Children = { from, to } }, () =>
        {
            done(); // closes first: the control may be reused by its strip, so it is made clickable only after the close ran
            control.IsHitTestVisible = true;
        });
    }

    /// <summary>The content of a folding list or panel: a short fade, and a 4 px drop into place when movement is allowed.</summary>
    public static void Fold(Control control)
    {
        if (Instant) return;
        var from = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.55) } };
        var to = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0) } };
        if (MotionPolicy.AllowsMovement(Level))
        {
            from.Setters.Add(new Setter(ShiftYProperty, -4.0));
            to.Setters.Add(new Setter(ShiftYProperty, 0.0));
        }

        Play(control, new Animation { Duration = Ms(120), Easing = new CubicEaseOut(), Children = { from, to } });
    }

    /// <summary>Moves a control from an offset back to its place (a reorder, a panel opening from its side).</summary>
    public static void Slide(Control control, double fromX, double fromY)
    {
        // A control that is leaving (hit testing off) keeps its fade: a new run on it would cancel the fade and close it at once.
        if (Instant || !MotionPolicy.AllowsMovement(Level) || !control.IsHitTestVisible) return;
        var from = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ShiftXProperty, fromX), new Setter(ShiftYProperty, fromY) } };
        var to = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ShiftXProperty, 0.0), new Setter(ShiftYProperty, 0.0) } };
        Play(control, new Animation { Duration = Ms(160), Easing = new CubicEaseOut(), Children = { from, to } });
    }

    /// <summary>
    /// Scrolls a list to an item and glides the visible rows the last stretch (at most 48 px) with a render transform, so the jump reads as movement.
    /// The list itself scrolls at once; nothing is measured again per frame.
    /// </summary>
    public static void ScrollTo(ListBox list, int index)
    {
        var before = list.Scroll is ScrollViewer s1 ? s1.Offset.Y : 0;
        list.ScrollIntoView(index);
        if (Instant || list.Scroll is not ScrollViewer { Content: Control rows } scroll) return;
        var shift = Math.Clamp(before - scroll.Offset.Y, -48, 48);
        if (Math.Abs(shift) >= 1) Slide(rows, 0, shift);
    }

    /// <summary>
    /// Makes a panel slide its children into place when they move (a tab closes or is dragged to a new order). It remembers each child's position
    /// after every layout and animates the difference, so it needs no hook in the code that reorders. Skipped while instant, while the panel itself changes
    /// size, and for panels with more than <see cref="FollowLimit"/> children.
    /// </summary>
    public static void Follow(Panel panel)
    {
        if (Followed.TryGetValue(panel, out _)) return;
        var last = new Dictionary<Control, (double X, double Y)>();
        // A dragged tab drops into its new place and nothing else moves (like a browser): the layout change right after a real drag is not animated.
        Point? pressedAt = null;
        var dragged = false;
        var quietUntil = 0L;
        var size = default(Size);
        static void Noop() { }
        void Layout(object? sender, EventArgs e)
        {
            if (Instant || panel.Children.Count > FollowLimit)
            {
                last.Clear();
                return;
            }

            var steady = panel.Bounds.Size == size;
            size = panel.Bounds.Size;
            var next = new Dictionary<Control, (double X, double Y)>(panel.Children.Count);
            foreach (var child in panel.Children)
                if (child.IsVisible) next[child] = (child.Bounds.X, child.Bounds.Y);
            if (steady && Environment.TickCount64 >= quietUntil) Settle(last, next);
            last = next;
        }

        void Pressed(object? sender, PointerPressedEventArgs e)
        {
            pressedAt = e.GetPosition(panel);
            dragged = false;
        }

        void Moved(object? sender, PointerEventArgs e)
        {
            if (pressedAt is { } from && e.GetPosition(panel) is var at && Math.Abs(at.X - from.X) + Math.Abs(at.Y - from.Y) > 6) dragged = true;
        }

        void Released(object? sender, PointerReleasedEventArgs e)
        {
            if (dragged) quietUntil = Environment.TickCount64 + 400;
            pressedAt = null;
            dragged = false;
        }

        void Detach(object? sender, VisualTreeAttachmentEventArgs e)
        {
            panel.LayoutUpdated -= Layout;
            panel.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
            panel.RemoveHandler(InputElement.PointerMovedEvent, Moved);
            panel.RemoveHandler(InputElement.PointerReleasedEvent, Released);
            panel.DetachedFromVisualTree -= Detach;
            Followed.Remove(panel);
        }

        Followed.Add(panel, Noop);
        panel.LayoutUpdated += Layout;
        panel.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        panel.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
        panel.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
        panel.DetachedFromVisualTree += Detach;
    }

    private static void Settle(IReadOnlyDictionary<Control, (double X, double Y)> from, IReadOnlyDictionary<Control, (double X, double Y)> to)
    {
        foreach (var (child, dx, dy) in LayoutShifts.Between(from, to)) Slide(child, dx, dy);
    }

    private static Animation Fade(double from, double to, int ms) => new()
    {
        Duration = Ms(ms),
        Easing = new CubicEaseOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, from) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, to) } },
        },
    };

    /// <summary>Runs one animation on a control, replacing the one it had. <paramref name="done"/> runs when the animation ends, completed or cancelled.</summary>
    internal static void Play(Control control, Animation animation, Action? done = null) => _ = RunAsync(control, animation, done);

    private static async Task RunAsync(Control control, Animation animation, Action? done = null)
    {
        Cancel(control);
        var cts = new CancellationTokenSource();
        Runs.AddOrUpdate(control, cts);
        Live.Add(cts);
        EntersStarted++;
        try { await animation.RunAsync(control, cts.Token); }
        catch (OperationCanceledException) { Cancelled++; } // the finally below still hands the values back
        catch (Exception e) { LastFailure = e; } // an animation must never take the app down; a test reads this
        finally
        {
            Live.Remove(cts);
            if (Runs.TryGetValue(control, out var current) && current == cts) Runs.Remove(control);
            cts.Dispose();
            // Avalonia ends an animation when its control is detached, which resumes this code inside the detach of the tree; removing a child there makes the parent run past its own list.
            if (done is not null) Dispatcher.UIThread.Post(done, DispatcherPriority.Send);
        }
    }
}
