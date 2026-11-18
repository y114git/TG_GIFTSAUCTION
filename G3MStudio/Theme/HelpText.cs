using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using G3MStudio.Core.Settings;

namespace G3MStudio.Theme;

/// <summary>What an explanatory text block is: a hint tells how to use something, a detail tells about the current thing.</summary>
public enum HelpKind
{
    None,
    Hint,
    Detail,
}

/// <summary>
/// One switch for all explanatory text (Settings > Appearance > Explanatory text). A control tagged with <see cref="KindProperty"/> (<c>.Hint()</c>, <c>.Detail()</c>) is visible only while
/// both the code that owns it wants it visible and the chosen <see cref="HelpLevel"/> allows its kind; the code keeps setting <c>IsVisible</c> as before, and a level change applies at once.
/// Error messages, warnings and questions are never tagged: the user needs them to act.
/// </summary>
public static class HelpText
{
    public static readonly AttachedProperty<HelpKind> KindProperty = AvaloniaProperty.RegisterAttached<Control, HelpKind>("Kind", typeof(HelpText));

    // The tagged controls, held weakly (a closed view is dropped on its own), each with the hold that hides it while the level does not allow its kind.
    // The hold sets IsVisible at animation priority, above the owner's own value, so the owner's value is its wish whatever the level does to the control.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Hold> Tagged = [];

    private sealed class Hold
    {
        public IDisposable? Hidden;
    }

    static HelpText()
    {
        KindProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            if (e.NewValue is HelpKind kind) OnKind(c, kind);
        });
    }

    public static HelpKind GetKind(Control control) => control.GetValue(KindProperty);

    public static void SetKind(Control control, HelpKind kind) => control.SetValue(KindProperty, kind);

    /// <summary>The level in force; changing it shows or hides every tagged control.</summary>
    public static HelpLevel Level
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            foreach (var (control, hold) in Tagged.ToArray()) Apply(control, hold);
        }
    } = HelpLevel.All;

    /// <summary>Whether a level shows a kind of text.</summary>
    public static bool Allows(HelpLevel level, HelpKind kind) => kind == HelpKind.None || level switch
    {
        HelpLevel.All => true,
        HelpLevel.Hints => kind == HelpKind.Hint,
        HelpLevel.Details => kind == HelpKind.Detail,
        _ => false,
    };

    /// <summary>Tags a control as a hint (how to use something).</summary>
    public static T Hint<T>(this T control) where T : Control
    {
        SetKind(control, HelpKind.Hint);
        return control;
    }

    /// <summary>Tags a control as a detail (about the current thing).</summary>
    public static T Detail<T>(this T control) where T : Control
    {
        SetKind(control, HelpKind.Detail);
        return control;
    }

    private static void OnKind(Control control, HelpKind kind)
    {
        if (kind != HelpKind.None)
        {
            Apply(control, Tagged.GetOrCreateValue(control));
            return;
        }

        // An untagged control shows whatever its owner wants.
        if (!Tagged.TryGetValue(control, out var hold)) return;
        hold.Hidden?.Dispose();
        Tagged.Remove(control);
    }

    private static void Apply(Control control, Hold hold)
    {
        var hide = !Allows(Level, GetKind(control));
        if (hide == hold.Hidden is not null) return;
        if (hide) hold.Hidden = control.SetValue(Visual.IsVisibleProperty, false, BindingPriority.Animation);
        else
        {
            hold.Hidden?.Dispose();
            hold.Hidden = null;
        }
    }
}
