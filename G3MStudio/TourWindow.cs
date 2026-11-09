using G3MStudio.Theme;
using Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// The short tour (design/onboarding.md step 6): six cards that each name a part of the window and say what it is for. The panel a card talks about
/// is brought to the front while the card shows, through the callback of the constructor; a card with no panel (the editor tabs, the commands) shows nothing.
/// shortcut: a window beside the real one, not coach marks anchored to live regions with an arrow and a ring; move to popovers when the shell has an overlay layer.
/// </summary>
public sealed class TourWindow : Window
{
    /// <summary>The cards: the key under "tour." and the panel to bring forward, or null.</summary>
    public static readonly (string Key, string? Panel)[] Steps =
    [
        ("explorer", "Explorer"), ("tabs", null), ("inspector", "Inspector"), ("palette", null), ("problems", "Problems"), ("save", null),
    ];

    private readonly TextBlock _count = new() { Name = "TourCount", Classes = { "muted" } };
    private readonly TextBlock _title = new TextBlock { Name = "TourTitle" }.TextSize("FontSize.Title");
    private readonly TextBlock _text = new() { Name = "TourText", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _back = new() { Name = "TourBack" };
    private readonly Button _next = new() { Name = "TourNext", Classes = { "primary" } };
    private readonly Action<string> _show;

    public TourWindow(Action<string> show)
    {
        _show = show;
        this.Tr(TitleProperty, "tour.window");
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        var skip = DialogFrame.Wire(this, new Button { Name = "TourSkip" }.Tr(ContentControl.ContentProperty, "tour.skip"), Close, DialogRole.Normal);
        _back.Content = Loc.Current["tour.back"];
        DialogFrame.Wire(this, _back, () => Step = Math.Max(0, Step - 1), DialogRole.Normal);
        DialogFrame.Wire(this, _next, () =>
        {
            if (Step == Steps.Length - 1) Close();
            else Step++;
        }, DialogRole.Primary);
        KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Escape) return;
            Close();
            e.Handled = true;
        };
        _title.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        DialogFrame.Show(this, new StackPanel { Spacing = Mods.ModsUi.Space(2), Children = { _count, _title, _text } }, [skip, _back, _next], scroll: DialogScroll.Vertical);
        Step = 0;
    }

    /// <summary>The card shown, 0 to the number of cards minus one.</summary>
    public int Step
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, Steps.Length - 1);
            var (key, panel) = Steps[field];
            _count.Text = Loc.Current.Format("tour.count", ("n", field + 1), ("total", Steps.Length));
            _title.Text = Loc.Current["tour." + key + ".title"];
            _text.Text = Loc.Current["tour." + key + ".text"];
            _back.IsEnabled = field > 0;
            _next.Content = Loc.Current[field == Steps.Length - 1 ? "tour.done" : "tour.next"];
            if (panel is not null) _show(panel);
        }
    }
}
