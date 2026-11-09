using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using G3MLib.DataFile;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The loose files next to the data file: list with a filter, and Add, Replace, Export, Rename and Delete. Changes go straight to the game folder and are not on the undo stack;
/// a rename or delete of a file that a sound or string names asks first (the usual risky action prompt).
/// </summary>
public sealed class IncludedFilesView : Grid
{
    private readonly Workspace _workspace;
    private readonly IncludedFiles _files;
    private readonly Func<RiskyRefusalException, string, Task<bool>> _askRisky;
    private readonly Action<NotificationLevel, string> _notify;
    private readonly TextBlock _summary = new() { Name = "IncludedSummary", Classes = { "muted" } };
    private readonly TextBox _filter = new() { Name = "IncludedFilter", PlaceholderText = Loc.Current["included.filter"], MinWidth = 200 };
    private readonly ListBox _list = new() { Name = "IncludedList", };
    private IncludedScan _scan = new([], false);

    public IncludedFilesView(Workspace workspace, UserData home, Func<RiskyRefusalException, string, Task<bool>> askRisky, Action<NotificationLevel, string> notify)
    {
        Name = "IncludedFilesView";
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*");
        Margin = new Thickness(8); // keeps the path line off the tab border
        _workspace = workspace;
        _files = new IncludedFiles(workspace.Path, home);
        _askRisky = askRisky;
        _notify = notify;
        Button Make(string name, string key, Func<IncludedFile?, Task> act, bool needsSelection = true)
        {
            var button = new Button { Name = name, Content = Loc.Current[key] };
            button.Click += async (_, _) =>
            {
                var selected = _list.SelectedItem as IncludedFile;
                if (needsSelection && selected is null) return;
                try
                {
                    await act(selected);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    _notify(NotificationLevel.Error, e.Message);
                }

                await RefreshAsync();
            };
            return button;
        }

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                Make("IncludedAdd", "included.add", _ => AddAsync(), false),
                Make("IncludedReplace", "included.replace", ReplaceAsync),
                Make("IncludedExport", "included.export", ExportAsync),
                Make("IncludedRename", "included.rename", RenameAsync),
                Make("IncludedDelete", "included.delete", DeleteAsync),
                Make("IncludedRefresh", "included.refresh", _ => Task.CompletedTask, false),
            },
        };
        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<IncludedFile>((f, _) =>
        {
            var row = Columns();
            Cell(row, 0, f.Path);
            Cell(row, 1, Loc.Current[KindKeys[f.Kind]]);
            Cell(row, 2, $"{f.Size / 1024.0:0.#} KB", true);
            Cell(row, 3, $"{f.Modified:yyyy-MM-dd HH:mm}");
            return row;
        });
        _filter.TextChanged += (_, _) => Show();
        var header = Columns();
        header.Margin = new Thickness(8, 0, 8, 2); // lines up with the list rows, which carry 8 px of padding
        foreach (var (column, key) in new[] { (0, "included.colName"), (1, "included.colType"), (2, "included.colSize"), (3, "included.colModified") }) Cell(header, column, Loc.Current[key], column == 2, true);
        var rows = new Control[] { _summary, buttons, _filter, header, _list };
        for (var i = 0; i < rows.Length; i++)
        {
            SetRow(rows[i], i);
            if (i < rows.Length - 1) rows[i].Margin = new Thickness(rows[i].Margin.Left, 0, rows[i].Margin.Right, 6);
            Children.Add(rows[i]);
        }

        _ = RefreshAsync();
    }

    private static readonly Dictionary<string, string> KindKeys = new() { ["audio"] = "included.kind.audio", ["images"] = "included.kind.images", ["text"] = "included.kind.text", ["libraries"] = "included.kind.libraries", ["other"] = "included.kind.other" };

    // Header and rows share these columns: name takes the rest, the others have a fixed width.
    private static Grid Columns() => new() { ColumnDefinitions = new ColumnDefinitions("*,110,90,150") };

    private static void Cell(Grid row, int column, string text, bool right = false, bool muted = false)
    {
        var cell = new TextBlock { Text = text, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, TextAlignment = right ? Avalonia.Media.TextAlignment.Right : Avalonia.Media.TextAlignment.Left, Margin = new Thickness(0, 0, 8, 0) };
        if (muted) cell.Classes.Add("muted");
        SetColumn(cell, column);
        row.Children.Add(cell);
    }

    private int _scanVersion;

    // The folder walk can be slow on a big game folder or a network share, so it runs off the UI thread; only the newest scan is shown.
    private async Task RefreshAsync()
    {
        var version = ++_scanVersion;
        var scan = await Task.Run(() => _files.Scan());
        if (version != _scanVersion) return;
        _scan = scan;
        Show();
    }

    private void Show()
    {
        var needle = _filter.Text?.Trim() ?? "";
        var shown = _scan.Files.Where(f => needle.Length == 0 || f.Path.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        _list.ItemsSource = shown;
        _summary.Text = _scan.Files.Count == 0
            ? Loc.Current["included.empty"]
            : Loc.Current.Format(_scan.Truncated ? "included.summaryTruncated" : "included.summary", ("folder", _files.Root), ("count", _scan.Files.Count), ("size", $"{_scan.Files.Sum(f => f.Size) / 1048576.0:0.#}"));
    }

    private IStorageProvider? Storage => TopLevel.GetTopLevel(this)?.StorageProvider;

    private async Task AddAsync()
    {
        if (Storage is not { } storage) return;
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true });
        var (added, skipped, failed) = _files.Add(picked.Select(p => p.TryGetLocalPath()).OfType<string>());
        if (failed.Count > 0) _notify(NotificationLevel.Error, Loc.Current.Format("included.failed", ("files", string.Join(", ", failed))));
        _notify(skipped.Count > 0 ? NotificationLevel.Warning : NotificationLevel.Info, Loc.Current.Format("included.added", ("added", added.Count), ("skipped", skipped.Count)));
    }

    private async Task ReplaceAsync(IncludedFile? file)
    {
        if (file is null || Storage is not { } storage) return;
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } source) _files.Replace(file.Path, source);
    }

    private async Task ExportAsync(IncludedFile? file)
    {
        if (file is null || Storage is not { } storage) return;
        var target = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = file.Name });
        if (target?.TryGetLocalPath() is { } path) _files.Export(file.Path, path);
    }

    private async Task RenameAsync(IncludedFile? file)
    {
        if (file is null || TopLevel.GetTopLevel(this) is not Window window) return;
        var name = await ProjectDialogs.AskTextAsync(window, Loc.Current["included.rename"], Loc.Current.Format("included.renamePrompt", ("name", file.Name)), file.Name, false);
        if (string.IsNullOrWhiteSpace(name) || name == file.Name) return;
        await ForceAsync((data, force) => _files.Rename(file.Path, name.Trim(), data, force), file.Name);
    }

    private Task DeleteAsync(IncludedFile? file) => file is null ? Task.CompletedTask : ForceAsync((data, force) => _files.Delete(file.Path, data, force), file.Name);

    // Same shape as the risky edits: try, warn on a refusal, then repeat with the force flag when the owner agrees.
    // The file IO runs off the UI thread; the lock is held only to read the model.
    private async Task ForceAsync(Action<GameMakerData?, bool> act, string name)
    {
        GameMakerData? data;
        using (_workspace.Lock.Read()) data = _workspace.Data;
        IncludedFileUseException refusal;
        try
        {
            await Task.Run(() => act(data, false));
            return;
        }
        catch (IncludedFileUseException e)
        {
            refusal = e;
        }

        if (await _askRisky(refusal, name)) await Task.Run(() => act(data, true));
    }
}
