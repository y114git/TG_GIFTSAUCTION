using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The Advanced part of the Resize dialogs, collapsed at first: a page to try first, whether a page may grow, whether a new page may be made, and the largest
/// page size. The last three are remembered in the settings for the next dialog and for the canvas handles; the page belongs to one file and is not.
/// </summary>
public sealed class FitOptionsPanel : Expander
{
    private static readonly int[] Sizes = [0, 256, 512, 1024, 2048, 4096, TextureFit.MaxPageSide];
    private readonly ComboBox _page = new() { Name = "FitPage", MinWidth = 200 };
    private readonly CheckBox _grow = new() { Name = "FitGrow" };
    private readonly CheckBox _newPage = new() { Name = "FitNewPage" };
    private readonly ComboBox _max = new() { Name = "FitMaxSide", MinWidth = 200 };
    private readonly IReadOnlyList<string> _pages;

    // pages: the pages a picture may go to first; automatic: the largest page side when none is chosen
    public FitOptionsPanel(IReadOnlyList<string> pages, int automatic)
    {
        Name = "FitAdvanced";
        Header = Loc.Current["pages.advanced"];
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _pages = pages;
        _page.ItemsSource = new[] { Loc.Current["pages.optPageAuto"] }.Concat(pages).ToList();
        _page.SelectedIndex = 0;
        _grow.Content = Loc.Current["pages.optGrow"];
        _newPage.Content = Loc.Current["pages.optNewPage"];
        _max.ItemsSource = Sizes.Select(s => s == 0 ? Loc.Current.Format("pages.optMaxAuto", ("size", automatic)) : $"{s} x {s}").ToList();
        var last = Current;
        (_grow.IsChecked, _newPage.IsChecked, _max.SelectedIndex) = (last.Grow, last.NewPage, Math.Max(0, Array.IndexOf(Sizes, last.MaxSide)));
        _page.SelectionChanged += (_, _) => Changed?.Invoke();
        _max.SelectionChanged += (_, _) => Changed?.Invoke();
        _grow.IsCheckedChanged += (_, _) => Changed?.Invoke();
        _newPage.IsCheckedChanged += (_, _) => Changed?.Invoke();
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(DialogFrame.Form((Loc.Current["pages.optPage"], _page), (Loc.Current["pages.optMaxSide"], _max)));
        body.Children.Add(_grow);
        body.Children.Add(_newPage);
        Content = body;
    }

    /// <summary>The page is null for Automatic, and the largest size 0 for the file's own largest page (at least 2048).</summary>
    public FitOptions Options => new(_page.SelectedIndex > 0 ? _pages[_page.SelectedIndex - 1] : null, _grow.IsChecked == true, _newPage.IsChecked == true, Sizes[Math.Max(0, _max.SelectedIndex)]);

    /// <summary>The fields, for the dialog's check for unsaved choices.</summary>
    public Control[] Fields => [_page, _grow, _newPage, _max];

    public event Action? Changed;

    /// <summary>Keeps the choices for the next dialog (an applied dialog calls this).</summary>
    public void Remember() => Current = Options with { Page = null };

    private static WeakReference<SettingsStore>? _store;

    /// <summary>The window's settings, where the choices are kept between runs; without it (tests) they last for the run.</summary>
    public static void Hook(SettingsStore store) => _store = new WeakReference<SettingsStore>(store);

    /// <summary>The remembered choices; the canvas handles place with them too.</summary>
    public static FitOptions Current
    {
        get => _store?.TryGetTarget(out var store) == true ? new FitOptions(null, store.Current.Image.FitGrow, store.Current.Image.FitNewPage, store.Current.Image.FitMaxSide) : field;
        private set
        {
            field = value;
            if (_store?.TryGetTarget(out var store) == true)
                store.Update(s => s with { Image = s.Image with { FitGrow = value.Grow, FitNewPage = value.NewPage, FitMaxSide = value.MaxSide } }, false);
        }
    } = FitOptions.Default;
}
