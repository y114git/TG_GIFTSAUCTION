using System.Globalization;
using Avalonia.Controls;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// Resources that nothing refers to, grouped by type with the space they take. The safe ones can be unchecked and deleted together as one
/// undo step; the ones that may be used (named in code, in the room order, with a nickname and so on) are only listed.
/// </summary>
public sealed class DeadResourcesWindow : Window
{
    private readonly Nicknames _nicknames;
    private readonly Func<Task<IReadOnlyList<DeadResource>>> _scan;
    private readonly Func<IReadOnlyList<DeadResource>, Task<string?>> _delete;
    private readonly StackPanel _list = new() { Name = "DeadList", Spacing = 2 };
    private readonly TextBlock _summary = new() { Name = "DeadSummary", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _message = new() { Name = "DeadMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _rescan = new() { Name = "DeadScan" }, _deleteSafe = new() { Name = "DeadDelete", IsEnabled = false };
    private readonly List<(CheckBox Box, DeadResource Item)> _rows = [];
    private bool _busy;
    private Task _running = Task.CompletedTask;

    /// <param name="nicknames">Shown next to the names.</param>
    /// <param name="scan">Finds the dead resources of the open file.</param>
    /// <param name="delete">Asks, then deletes the given safe ones as one edit; returns what to tell the user, or null when nothing was done.</param>
    public DeadResourcesWindow(Nicknames nicknames, Func<Task<IReadOnlyList<DeadResource>>> scan, Func<IReadOnlyList<DeadResource>, Task<string?>> delete)
    {
        (_nicknames, _scan, _delete) = (nicknames, scan, delete);
        Title = Loc.Current["dead.title"];
        Width = 620;
        Height = 560;
        MinWidth = 420;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _rescan.Content = Loc.Current["dead.scan"];
        _deleteSafe.Content = Loc.Current["dead.deleteSafe"];
        _rescan.Click += (_, _) => _ = RefreshAsync();
        _deleteSafe.Click += (_, _) => _ = DeleteAsync();
        var content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                Docked(new TextBlock { Text = Loc.Current["dead.hint"], Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, 8) }),
                Docked(_summary),
                new ScrollViewer { Name = "DeadScroll", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = _list },
            },
        };
        DialogFrame.Show(this, content, [_deleteSafe, _rescan, DialogFrame.Close(this)], _message);
        Opened += (_, _) => _ = RefreshAsync();
    }

    /// <summary>The rows that Delete all safe would take: checked and safe.</summary>
    internal IReadOnlyList<DeadResource> Checked => [.. _rows.Where(r => r.Box.IsChecked == true).Select(r => r.Item)];

    internal Task RefreshAsync() => RunAsync(() => Task.CompletedTask);

    internal Task DeleteAsync() => RunAsync(async () =>
    {
        if (Checked is not { Count: > 0 } chosen) return;
        _message.Text = await _delete(chosen) ?? "";
    });

    private static Control Docked(Control control)
    {
        DockPanel.SetDock(control, Avalonia.Controls.Dock.Top);
        return control;
    }

    // One scan or delete at a time: a second request while one runs waits for it instead of starting a scan whose older list could land last.
    private Task RunAsync(Func<Task> work) => _busy ? _running : _running = RunNowAsync(work);

    private async Task RunNowAsync(Func<Task> work)
    {
        _busy = true;
        _rescan.IsEnabled = _deleteSafe.IsEnabled = false;
        try
        {
            await work();
            await Fill(_scan());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            FailureReport.Report(e, "dead.scan");
            _summary.Text = e.Message; // in place of "Scanning...", which would otherwise stay for good
        }
        finally
        {
            _busy = false;
            _rescan.IsEnabled = true;
            UpdateDelete();
        }
    }

    private async Task Fill(Task<IReadOnlyList<DeadResource>> scan)
    {
        _summary.Text = Loc.Current["dead.scanning"];
        var found = await scan;
        _rows.Clear();
        _list.Children.Clear();
        var safe = found.Where(d => d.IsSafe).ToList();
        var maybe = found.Where(d => !d.IsSafe).ToList();
        _summary.Text = found.Count == 0
            ? Loc.Current["dead.none"]
            : Loc.Current.Format("dead.summary", ("safe", safe.Count), ("size", Size(safe.Sum(d => d.Bytes))), ("maybe", maybe.Count));
        AddSection(Loc.Current["dead.safeHeader"], safe, checkable: true);
        AddSection(Loc.Current["dead.maybeHeader"], maybe, checkable: false);
        UpdateDelete();
    }

    private void AddSection(string title, List<DeadResource> items, bool checkable)
    {
        if (items.Count == 0) return;
        _list.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.Bold, Margin = new Avalonia.Thickness(0, 8, 0, 2), TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        foreach (var group in items.GroupBy(d => d.Type))
        {
            _list.Children.Add(new TextBlock
            {
                Text = Loc.Current.Format("dead.group", ("type", Loc.Current["explorer.type." + group.Key]), ("count", group.Count()), ("size", Size(group.Sum(d => d.Bytes)))),
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Margin = new Avalonia.Thickness(0, 4, 0, 0),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            foreach (var item in group.OrderBy(d => d.Name, StringComparer.Ordinal))
            {
                var text = _nicknames.Both(item.Type, item.Name) + (item.Bytes > 0 ? "  " + Size(item.Bytes) : "") + (item.IsSafe ? "" : "  - " + ReasonText(item.Reason));
                if (!checkable)
                {
                    _list.Children.Add(new TextBlock { Text = text, Margin = new Avalonia.Thickness(16, 0, 0, 0), TextWrapping = Avalonia.Media.TextWrapping.Wrap });
                    continue;
                }

                var box = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, IsChecked = true, Margin = new Avalonia.Thickness(16, 0, 0, 0) };
                box.IsCheckedChanged += (_, _) => UpdateDelete();
                _rows.Add((box, item));
                _list.Children.Add(box);
            }
        }
    }

    private static string ReasonText(DeadReason reason) => reason switch
    {
        DeadReason.NameInCode => Loc.Current["dead.reason.nameincode"],
        DeadReason.NamePrefixInCode => Loc.Current["dead.reason.nameprefixincode"],
        DeadReason.GlobalInit => Loc.Current["dead.reason.globalinit"],
        DeadReason.DefaultFont => Loc.Current["dead.reason.defaultfont"],
        DeadReason.NumericRefs => Loc.Current["dead.reason.numericrefs"],
        DeadReason.Nickname => Loc.Current["dead.reason.nickname"],
        DeadReason.Tag => Loc.Current["dead.reason.tag"],
        _ => Loc.Current["dead.reason.favourite"],
    };

    private void UpdateDelete() => _deleteSafe.IsEnabled = !_busy && _rows.Any(r => r.Box.IsChecked == true);

    /// <summary>"0 KB", "12 KB" or "1.5 MB": the estimate of picture or audio data the resources hold.</summary>
    internal static string Size(long bytes) => bytes >= 1 << 20 ? (bytes / (double)(1 << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : (bytes + 1023) / 1024 + " KB";
}
