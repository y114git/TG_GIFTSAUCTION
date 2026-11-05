using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Projects;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Small modal dialogs of Project mode. Each returns its answer or null when cancelled.</summary>
internal static class ProjectDialogs
{
    private static Window Shell(string titleKey, double width = 460)
    {
        var window = new Window
        {
            Title = Loc.Current[titleKey],
            Width = width,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        return window;
    }

    private static Button Btn(string key, string name, Action click) => Make(Loc.Current[key], name, click);

    private static Button Make(string text, string name, Action click)
    {
        var button = new Button { Content = text, Name = name };
        button.Click += (_, _) => click();
        return button;
    }

    private static StackPanel Buttons(params Button[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var button in buttons) panel.Children.Add(button);
        return panel;
    }

    /// <summary>Q48: where projects are saved. The folder is prefilled; Remember this choice is on by default.</summary>
    public static async Task<(string Root, bool Remember)?> AskLocationAsync(Window owner, string suggested)
    {
        var window = Shell("project.location.title");
        var path = new TextBox { Name = "Folder", Text = suggested, MinWidth = 300 };
        var remember = new CheckBox { Name = "Remember", Content = Loc.Current["project.location.remember"], IsChecked = true };
        (string, bool)? answer = null;
        var browse = Btn("project.location.browse", "Browse", async () =>
        {
            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (folders is [{ } folder, ..] && folder.TryGetLocalPath() is { } picked) path.Text = picked;
        });
        var ok = Btn("project.enable.button", "Ok", () =>
        {
            if (string.IsNullOrWhiteSpace(path.Text)) return;
            answer = (path.Text.Trim(), remember.IsChecked == true);
            window.Close();
        });
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = Loc.Current["project.location.label"] },
                new DockPanel { Children = { path } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { browse } },
                remember,
                Buttons(ok, Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>Autosave and autosync are separate settings, each with a trigger and a delay (Q49).</summary>
    public static async Task<ProjectSettings?> AskSettingsAsync(Window owner, ProjectSettings current)
    {
        var window = Shell("project.settings.title");
        var triggers = Enum.GetValues<Trigger>();
        ComboBox Combo(string name, Trigger value) => new() { Name = name, ItemsSource = triggers.Select(t => Loc.Current["project.trigger." + t]).ToList(), SelectedIndex = Array.IndexOf(triggers, value), MinWidth = 220 };
        NumericUpDown Seconds(string name, int value) => new() { Name = name, Minimum = 1, Maximum = 3600, Value = value, FormatString = "0", Width = 110 };
        var save = Combo("AutoSave", current.AutoSave);
        var saveSeconds = Seconds("AutoSaveSeconds", current.AutoSaveSeconds);
        var sync = Combo("AutoSync", current.AutoSync);
        var syncSeconds = Seconds("AutoSyncSeconds", current.AutoSyncSeconds);
        ProjectSettings? answer = null;
        var ok = Btn("rename.ok", "Ok", () =>
        {
            answer = new ProjectSettings(triggers[save.SelectedIndex], (int)(saveSeconds.Value ?? current.AutoSaveSeconds), triggers[sync.SelectedIndex], (int)(syncSeconds.Value ?? current.AutoSyncSeconds));
            window.Close();
        });
        StackPanel Row(string labelKey, Control combo, Control seconds) => new()
        {
            Spacing = 4,
            Children = { new TextBlock { Text = Loc.Current[labelKey] }, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { combo, seconds, new TextBlock { Text = Loc.Current["project.settings.seconds"], VerticalAlignment = VerticalAlignment.Center } } } },
        };
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children = { Row("project.settings.autosave", save, saveSeconds), Row("project.settings.autosync", sync, syncSeconds), Buttons(ok, Btn("explorer.cancel", "Cancel", window.Close)) },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>Q50: Move, Copy or Recreate from the project. <paramref name="apply"/> returns the reason it failed, or null when done.</summary>
    public static async Task AskMoveAsync(Window owner, string current, Func<string, RelocateMode, Task<string?>> apply)
    {
        var window = Shell("project.move.title", 520);
        var path = new TextBox { Name = "NewPath", Text = current, MinWidth = 400 };
        var error = new TextBlock { Name = "Error", TextWrapping = TextWrapping.Wrap, Foreground = (IBrush?)Application.Current?.FindResource("Danger") };
        var all = new List<Button>();
        Button Mode(string key, RelocateMode mode)
        {
            var button = Make(Loc.Current[key], mode.ToString(), async () =>
            {
                foreach (var b in all) b.IsEnabled = false;
                var refused = await apply(path.Text?.Trim() ?? "", mode);
                error.Text = refused ?? "";
                foreach (var b in all) b.IsEnabled = true;
                if (refused is null) window.Close();
            });
            all.Add(button);
            return button;
        }

        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = Loc.Current["project.move.path"] },
                path,
                error,
                Buttons(Mode("project.move.move", RelocateMode.Move), Mode("project.move.copy", RelocateMode.Copy), Mode("project.move.recreate", RelocateMode.Recreate), Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
    }

    /// <summary>Disable project: keep it archived, delete it, or cancel. True means delete; null means cancel.</summary>
    public static async Task<bool?> AskDisableAsync(Window owner)
    {
        var window = Shell("project.disable.title");
        bool? answer = null;
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = Loc.Current["project.disable.message"], TextWrapping = TextWrapping.Wrap },
                Buttons(
                    Btn("project.disable.keep", "Keep", () => { answer = false; window.Close(); }),
                    Btn("project.disable.delete", "Delete", () => { answer = true; window.Close(); }),
                    Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>The data file changed outside: bring its changes in, replace it from the project, keep both, or decide later (null).</summary>
    public static async Task<ExternalChange?> AskExternalChangeAsync(Window owner, string name)
    {
        var window = Shell("project.external.title");
        ExternalChange? answer = null;
        Button Choice(string key, string id, ExternalChange change) => Btn(key, id, () => { answer = change; window.Close(); });
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = Loc.Current.Format("project.external.message", ("name", name)), TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Choice("project.external.bring", "BringIn", ExternalChange.BringIn),
                        Choice("project.external.overwrite", "Overwrite", ExternalChange.Overwrite),
                        Choice("project.external.keepBoth", "KeepBoth", ExternalChange.KeepBoth),
                        Btn("project.external.later", "Later", window.Close),
                    },
                },
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>One line per resource that changed on both sides: keep the project's version or take the data file's. Null when cancelled.</summary>
    public static async Task<Dictionary<string, MergeSide>?> AskConflictsAsync(Window owner, IReadOnlyList<MergeConflict> conflicts)
    {
        var window = Shell("project.conflicts.title", 560);
        var rows = new List<(string Path, ComboBox Side)>();
        var list = new StackPanel { Spacing = 6 };
        foreach (var conflict in conflicts)
        {
            var side = new ComboBox { Name = "Side", ItemsSource = new[] { Loc.Current["project.conflicts.ours"], Loc.Current["project.conflicts.theirs"] }, SelectedIndex = 0, MinWidth = 200, Tag = conflict.Path };
            rows.Add((conflict.Path, side));
            list.Children.Add(new DockPanel { Children = { new TextBlock { Text = conflict.Path, VerticalAlignment = VerticalAlignment.Center, Width = 280 }, side } });
        }

        Dictionary<string, MergeSide>? answer = null;
        var ok = Btn("project.conflicts.apply", "Ok", () =>
        {
            answer = rows.ToDictionary(r => r.Path, r => r.Side.SelectedIndex == 1 ? MergeSide.Theirs : MergeSide.Ours);
            window.Close();
        });
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Loc.Current["project.conflicts.message"], TextWrapping = TextWrapping.Wrap },
                new ScrollViewer { MaxHeight = 320, Content = list },
                Buttons(ok, Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    public static async Task<bool> AskConfirmAsync(Window owner, string title, string message)
    {
        var window = Shell("transfer.confirmTitle");
        window.Title = title;
        var answer = false;
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                Buttons(Btn("transfer.ok", "Ok", () => { answer = true; window.Close(); }), Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>A text box with a label (several lines when asked) and Ok and Cancel, for a script that asks for text; null when cancelled.</summary>
    public static async Task<string?> AskTextAsync(Window owner, string title, string label, string current, bool multiline)
    {
        var window = Shell("transfer.confirmTitle", multiline ? 560 : 460);
        window.Title = title;
        string? answer = null;
        var box = new TextBox { Name = "ScriptText", Text = current, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 140 : 0 };
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
                box,
                Buttons(Btn("transfer.ok", "Ok", () => { answer = box.Text ?? string.Empty; window.Close(); }), Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>A list to pick one entry from, with Ok and Cancel; the index of the entry, or null when cancelled. With <paramref name="columns"/> each item is cells split by a tab, shown under those headings.</summary>
    public static async Task<int?> AskChoiceAsync(Window owner, string title, string label, IReadOnlyList<string> items, IReadOnlyList<string>? columns = null, int selected = -1)
    {
        var window = Shell("transfer.confirmTitle", 520);
        window.Title = title;
        int? answer = null;
        var list = new ListBox { Name = "ChoiceList", ItemsSource = items, SelectedIndex = selected, MaxHeight = 280, ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) => columns is null ? new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } : ChoiceRow(text.Split('\t'), false)) };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        list.DoubleTapped += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;
            answer = list.SelectedIndex;
            window.Close();
        };
        var body = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
                list,
                Buttons(Btn("transfer.ok", "Ok", () => { if (list.SelectedIndex >= 0) answer = list.SelectedIndex; window.Close(); }), Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        if (columns is not null) body.Children.Insert(1, ChoiceRow([.. columns], true));
        window.Content = body;
        await window.ShowDialog(owner);
        return answer;
    }

    private static Grid ChoiceRow(string[] cells, bool header)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), Margin = header ? new Thickness(8, 0, 0, 0) : default };
        for (var i = 0; i < Math.Min(cells.Length, 2); i++)
        {
            var cell = new TextBlock { Text = cells[i], TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(cell, i);
            if (header) cell.Foreground = (IBrush?)Application.Current?.FindResource("Text.Muted");
            row.Children.Add(cell);
        }

        return row;
    }

    /// <summary>A data file that is gone or does not read has a project: offer to build it again from the project.</summary>
    public static async Task<bool> AskRebuildAsync(Window owner, string name)
    {
        var window = Shell("project.rebuild.title");
        var answer = false;
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = Loc.Current.Format("project.rebuild.message", ("name", name)), TextWrapping = TextWrapping.Wrap },
                Buttons(Btn("project.rebuild.button", "Rebuild", () => { answer = true; window.Close(); }), Btn("explorer.cancel", "Cancel", window.Close)),
            },
        };
        await window.ShowDialog(owner);
        return answer;
    }
}
