using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using G3MStudio.Core.Settings;

namespace G3MStudio.Theme;

/// <summary>
/// The central animation policy. Every animation takes its duration from <see cref="Ms"/> and skips itself when <see cref="Instant"/> is true,
/// so the Animations setting changes all of them at once and live. Only containers and single elements animate, never rows of a big list.
/// </summary>
public static partial class Motion
{
    private static bool Headless { get; } = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Avalonia.Headless");
    private static Styles? _styles;
    private static bool _applied;
    private static Application? _owner; // the styles belong to one application; a later one (a new test session) must get its own


    /// <summary>The effective level (<see cref="MotionPolicy"/>).</summary>
    public static string Level { get; private set; } = MotionPolicy.Medium;

    /// <summary>Tests that check real transitions turn this on; otherwise headless runs finish every animation at once.</summary>
    public static bool TimedInHeadless { get; set; }

    /// <summary>Counts the entrance animations started, so a test can prove a rebuild of a big list starts none.</summary>
    public static int EntersStarted { get; private set; }

    public const int ProgressDelayMs = MotionPolicy.ProgressDelayMs;

    public static event Action? Changed;

    public static bool Instant => Level == MotionPolicy.Off || (Headless && !TimedInHeadless);

    public static TimeSpan Ms(int baseMs) => Instant ? TimeSpan.Zero : TimeSpan.FromMilliseconds(MotionPolicy.Milliseconds(Level, baseMs));

    /// <summary>Applies the stored choice (empty = not chosen) with the OS preference as the tie-break, and restyles the application.</summary>
    public static void Set(string chosen) => SetLevel(MotionPolicy.Resolve(chosen, OsReducesMotion()));

    public static void SetLevel(string level)
    {
        if (level == Level && _applied && ReferenceEquals(_owner, Application.Current)) return;
        _applied = true;
        Level = level;
        if (level == MotionPolicy.Off) CancelAll();
        Restyle();
        Changed?.Invoke();
    }

    /// <summary>Fades a control in (and, when the level allows movement, grows it from 97%). Runs on one element, on the render clock.</summary>
    public static void Enter(Control control, bool scale = false, double fromX = 0, double fromY = 0)
    {
        if (Instant) return;
        var move = scale && MotionPolicy.AllowsMovement(Level);
        var from = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } };
        var to = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0) } };
        if (move)
        {
            control.RenderTransformOrigin = RelativePoint.Center;
            from.Setters.Add(new Setter(GrowProperty, 0.97));
            to.Setters.Add(new Setter(GrowProperty, 1.0));
        }

        // An element that arrives from an edge (a toast) also slides in from there; Minimal keeps only the fade.
        if ((fromX != 0 || fromY != 0) && MotionPolicy.AllowsMovement(Level))
        {
            from.Setters.Add(new Setter(ShiftXProperty, fromX));
            from.Setters.Add(new Setter(ShiftYProperty, fromY));
            to.Setters.Add(new Setter(ShiftXProperty, 0.0));
            to.Setters.Add(new Setter(ShiftYProperty, 0.0));
        }

        Play(control, new Animation { Duration = Ms(140), Easing = new CubicEaseOut(), Children = { from, to } });
    }

    private static void Restyle()
    {
        var app = Application.Current;
        if (app is null) return;
        if (!ReferenceEquals(_owner, app)) (_owner, _styles) = (app, null);
        if (_styles is not null) app.Styles.Remove(_styles);
        _styles = Instant ? null : BuildStyles();
        if (_styles is not null) app.Styles.Add(_styles);
    }

    private static Styles BuildStyles()
    {
        var colour = Ms(180);
        var styles = new Styles
        {
            // Hover, pressed and selection fills fade instead of snapping.
            Brushes(x => x.OfType<Button>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), ContentPresenter.BackgroundProperty, colour),
            Brushes(x => x.OfType<ToggleButton>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), ContentPresenter.BackgroundProperty, colour),
            Brushes(x => x.OfType<MenuItem>().Template().OfType<Border>().Name("PART_LayoutRoot"), Border.BackgroundProperty, colour),
            Brushes(x => x.OfType<ListBox>().Not(n => n.Class("NoMotion")).Descendant().OfType<ListBoxItem>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), ContentPresenter.BackgroundProperty, colour),
            Brushes(x => x.OfType<ComboBoxItem>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), ContentPresenter.BackgroundProperty, colour),
            Brushes(x => x.OfType<TreeViewItem>().Template().OfType<ContentPresenter>().Name("PART_HeaderPresenter"), ContentPresenter.BackgroundProperty, colour),
            Brushes(x => x.OfType<TabItem>(), TemplatedControl.BackgroundProperty, colour),
            Brushes(x => x.OfType<CheckBox>().Template().OfType<Border>().Name("NormalRectangle"), Border.BackgroundProperty, colour),
            // Not while a tab is moved: the strip makes a new control for it, which would fade in from nothing (a flash).
            Brushes(x => x.OfType<Dock.Avalonia.Controls.DockControl>().Not(n => n.Class(G3MStudio.Docking.StudioDock.MovingClass)).Descendant().OfType<Dock.Avalonia.Controls.DocumentTabStripItem>(), TemplatedControl.BackgroundProperty, colour),
            Brushes(x => x.OfType<Dock.Avalonia.Controls.DockControl>().Not(n => n.Class(G3MStudio.Docking.StudioDock.MovingClass)).Descendant().OfType<Dock.Avalonia.Controls.ToolTabStripItem>(), TemplatedControl.BackgroundProperty, colour),
        };
        // Popups, menus and tooltips fade in; a tab opens with Open.
        foreach (var fade in new Style[]
        {
            Fade(x => x.OfType<ToolTip>()),
            Fade(x => x.OfType<ContextMenu>(), grow: true),
            Fade(x => x.OfType<MenuFlyoutPresenter>(), grow: true),
            Fade(x => x.OfType<FlyoutPresenter>(), grow: true),
        }) styles.Add(fade);
        if (MotionPolicy.AllowsMovement(Level)) styles.Add(Press());
        return styles;
    }

    private static Style Brushes(Func<Selector?, Selector> selector, AvaloniaProperty property, TimeSpan duration) => new(selector)
    {
        Setters = { new Setter(Animatable.TransitionsProperty, new Transitions { new BrushTransition { Property = property, Duration = duration, Easing = new CubicEaseOut() } }) },
    };

    /// <summary>A button gives under the finger: it shrinks to 98% while pressed and eases back, so a click is felt before it lands.</summary>
    private static Style Press() => new(x => x.OfType<Button>().Class(":pressed"))
    {
        Setters = { new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("scale(0.98)")) },
    };

    /// <summary>Fades in; a popup (<paramref name="grow"/>) also grows from 96% at its top-left corner, the corner it opens from, when movement is allowed.</summary>
    private static Style Fade(Func<Selector?, Selector> selector, bool grow = false)
    {
        var from = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } };
        var to = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0) } };
        var style = new Style(selector);
        if (grow && MotionPolicy.AllowsMovement(Level))
        {
            style.Setters.Add(new Setter(Visual.RenderTransformOriginProperty, RelativePoint.TopLeft));
            // RenderTransform has no animator in a style animation (it crashed on the first flyout); GrowProperty drives it.
            from.Setters.Add(new Setter(GrowProperty, 0.96));
            to.Setters.Add(new Setter(GrowProperty, 1.0));
        }

        style.Animations.Add(new Animation { Duration = Ms(120), Easing = new CubicEaseOut(), Children = { from, to } });
        return style;
    }

    public static bool OsReducesMotion()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return SystemParametersInfo(0x1042, 0, out var on, 0) && !on; } // SPI_GETCLIENTAREAANIMATION
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
}
