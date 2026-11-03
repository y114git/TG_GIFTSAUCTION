using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Start;

/// <summary>What one card on the start page shows; strings are localized when the card is made.</summary>
public sealed record RecentCard(string Path, string Name, string Version, string Opened)
{
    public bool Exists { get; init; }

    public string Missing { get; init; } = "";

    public string PinText { get; init; } = "";

    public string RemoveText { get; init; } = "";
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
            Exists = File.Exists(e.Path), Missing = loc["start.missing"], PinText = loc[e.Pinned ? "start.unpin" : "start.pin"], RemoveText = loc["start.remove"],
        })];
        Cards.ItemsSource = Shown;
        EmptyRecent.IsVisible = Shown.Count == 0;
        ClearButton.IsVisible = Shown.Count > 0;
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
