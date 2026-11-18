using Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>The buttons every editor shares, so they look and behave the same: Edit Image, and the rule that a disabled control says why.</summary>
public static class EditorButtons
{
    // Tip is the tooltip the control had before it was gated; Why and Key are what the gate put there (Key follows the language).
    private sealed class Own
    {
        public object? Tip;
        public string? Why;
        public string? Key;
        public bool Gated;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Own> Tips = [];

    /// <summary>The Edit Image button; <paramref name="open"/> opens the image editor on the picture the editor shows.</summary>
    public static Button EditImage(string name, Action open)
    {
        var button = new Button { Name = name, Content = Loc.Current["textureItem.edit"] };
        button.Click += (_, _) => open();
        return button;
    }

    /// <summary>Enables the control, or disables it and makes the tooltip say <paramref name="why"/>. The tooltip it had is shown again when it is enabled, unless something set a newer one meanwhile.</summary>
    public static T Gate<T>(this T control, string? why) where T : Control => Apply(control, why, null);

    /// <summary>Same as <see cref="Gate{T}"/> with the text of a language key; the text follows a language change while the control is disabled.</summary>
    public static T GateKey<T>(this T control, string? whyKey) where T : Control => Apply(control, whyKey is null ? null : Loc.Current[whyKey], whyKey);

    private static T Apply<T>(T control, string? why, string? key) where T : Control
    {
        var own = Tips.GetOrCreateValue(control);
        // The gate's own text is still the tooltip only when nobody replaced it (KeyTips, Labeled, a relabel).
        var ours = own.Gated && Equals(ToolTip.GetTip(control), own.Key is { } k ? Loc.Current[k] : own.Why);
        if (why is null)
        {
            if (ours) ToolTip.SetTip(control, own.Tip);
            own.Gated = false;
            control.IsEnabled = true;
            return control;
        }

        if (!own.Gated || !ours) own.Tip = ToolTip.GetTip(control);
        (own.Gated, own.Why, own.Key) = (true, why, key);
        control.IsEnabled = false;
        // A disabled control shows no tooltip unless told to, and the tooltip is the reason it is disabled.
        ToolTip.SetShowOnDisabled(control, true);
        if (key is null) ToolTip.SetTip(control, why);
        else control.Bind(ToolTip.TipProperty, LocBinding.To(key));
        return control;
    }
}
