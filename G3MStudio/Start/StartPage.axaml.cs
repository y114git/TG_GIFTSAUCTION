using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Interactivity;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Start;

/// <summary>What one card on the start page shows; strings are localized when the card is made.</summary>
public sealed record RecentCard(string Path, string Name, string Version, string Opened)
{
    public bool Exists { get; init; }

    // A lost file stays clickable: the click starts the lost-file steps, the dim look only says it is missing.
    public double Dim => Exists ? 1 : 0.6;

    public bool Pinned { get; init; }

    public string Missing { get; init; } = "";

    public string PinText { get; init; } = "";

    public string RemoveText { get; init; } = "";

    public string PinName { get; init; } = "";

    public string RemoveName { get; init; } = "";
}

/// <summary>Start page: open button, recent and pinned cards (design/shell.md section 15).</summary>
public partial class StartPage : UserControl
{
    private RecentFiles? _recent;

    public StartPage()
    {
        InitializeComponent();
        OpenButton.Click += (_, _) => Browse?.Invoke();
        ClearButton.Click += (_, _) => _recent?.Clear();
        SettingsButton.Click += (_, _) => Settings?.Invoke();
        Cards.SizeChanged += (_, e) => FitColumns(e.NewSize.Width);
        DropBox.Tapped += (_, _) => Browse?.Invoke();
        DropBox.KeyDown += (_, e) =>
        {
            if (e.Key is not (Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space)) return;
            e.Handled = true;
            Browse?.Invoke();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Loc.Current.PropertyChanged += OnLocalizerChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Loc.Current.PropertyChanged -= OnLocalizerChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLocalizerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Loc.Current.Language)) ShowCards();
    }

    public event Action? Browse;

    public event Action? Settings;

    public event Action<string>? OpenPath;

    public IReadOnlyList<RecentCard> Shown { get; private set; } = [];

    public void Bind(RecentFiles recent)
    {
        _recent = recent;
        recent.Changed += ShowCards;
        ShowCards();
    }

    private void ShowCards()
    {
        var loc = Loc.Current;
        Shown = [.. (_recent?.Entries ?? []).Select(e => new RecentCard(
            e.Path, e.Name, e.Version, loc.Format("start.opened", ("time", e.LastOpened.LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture))))
        {
            Exists = File.Exists(e.Path), Pinned = e.Pinned, Missing = loc["start.missing"], PinText = loc[e.Pinned ? "start.unpin" : "start.pin"], RemoveText = loc["start.remove"],
            PinName = loc.Format(e.Pinned ? "start.unpinNamed" : "start.pinNamed", ("name", e.Name)), RemoveName = loc.Format("start.removeNamed", ("name", e.Name)),
        })];
        Cards.ItemsSource = Shown;
        EmptyRecent.IsVisible = Shown.Count == 0;
        ClearButton.IsVisible = Shown.Count > 0;
        DropBox.IsVisible = Shown.Count == 0; // the hint is for a page with nothing else to show
    }

    // A card is at least this wide (the margin around it included); the grid takes as many columns as fit and shares the width out.
    private const double MinCard = 272;

    private void FitColumns(double width)
    {
        if (Cards.ItemsPanelRoot is UniformGrid grid) grid.Columns = Math.Max(1, (int)(width / MinCard));
    }

    private static string? PathOf(object? sender) => (sender as Button)?.Tag as string;

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (PathOf(sender) is { } path) OpenPath?.Invoke(path);
    }

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        if (PathOf(sender) is { } path && _recent?.Entries.FirstOrDefault(x => x.Path == path) is { } entry) _recent.SetPinned(path, !entry.Pinned);
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (PathOf(sender) is { } path) _recent?.Remove(path);
    }
}
