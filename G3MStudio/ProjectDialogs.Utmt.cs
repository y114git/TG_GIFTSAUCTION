using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Projects;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>The two dialogs of the UTMT project conversion: where from and where to, and what the conversion did.</summary>
internal static partial class ProjectDialogs
{
    private static readonly Dictionary<string, string> WarningKeys = new()
    {
        ["options"] = "project.utmt.warn.options",
        ["scripts"] = "project.utmt.warn.scripts",
        ["ignoredFolders"] = "project.utmt.warn.ignoredFolders",
        ["history"] = "project.utmt.warn.history",
        ["library"] = "project.utmt.warn.library",
        ["export"] = "project.utmt.warn.export",
        ["gitMissing"] = "project.utmt.warn.gitMissing",
        ["gitFailed"] = "project.utmt.warn.gitFailed",
    };

    /// <summary>The text of one conversion warning, in the language of the program.</summary>
    public static string WarningText(UtmtWarning warning) => Loc.Current.Format(WarningKeys[warning.Key], ("detail", warning.Detail));

    /// <summary>Asks for the original data file and the new project folder. Null when cancelled.</summary>
    public static async Task<UtmtImportRequest?> AskUtmtAsync(Window owner, UtmtProjectInfo info, string suggestedFolder, bool gitInstalled)
    {
        var window = Shell("project.utmt.title", 620);
        var dataCaption = new TextBlock { Text = Loc.Current["project.utmt.dataFile"] };
        var data = new TextBox { Name = "DataFile", MinWidth = 300 }.LabeledBy(dataCaption);
        var folderCaption = new TextBlock { Text = Loc.Current["project.utmt.destination"] };
        var folder = new TextBox { Name = "Destination", Text = suggestedFolder, MinWidth = 300 }.LabeledBy(folderCaption);
        var git = new CheckBox { Name = "Git", Content = Loc.Current["project.utmt.git"], IsChecked = gitInstalled, IsEnabled = gitInstalled };
        var scripts = new CheckBox { Name = "Scripts", Content = Loc.Current.Format("project.utmt.scripts", ("detail", string.Join(", ", info.Scripts))), IsChecked = false, IsVisible = info.Scripts.Count > 0 };
        var error = new TextBlock { Name = "Error", TextWrapping = TextWrapping.Wrap, Foreground = (IBrush?)Application.Current?.FindResource("Danger") }.HideWhenEmpty();
        UtmtImportRequest? answer = null;

        Button Browse(string name, Func<Task<string?>> pick, TextBox target)
        {
            var button = Btn(window, "project.utmt.browse", name, async () =>
            {
                if (await pick() is { } picked) target.Text = picked;
            });
            DockPanel.SetDock(button, Avalonia.Controls.Dock.Right);
            button.Margin = new Thickness(8, 0, 0, 0);
            return button;
        }

        var browseData = Browse("BrowseData", async () =>
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Loc.Current["project.utmt.pickData"] });
            return files is [{ } file, ..] ? file.TryGetLocalPath() : null;
        }, data);
        var browseFolder = Browse("BrowseFolder", async () =>
        {
            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Loc.Current["project.utmt.pickFolder"] });
            return folders is [{ } picked, ..] ? picked.TryGetLocalPath() : null;
        }, folder);
        var convert = Btn(window, "project.utmt.convert", "Ok", () =>
        {
            if (string.IsNullOrWhiteSpace(data.Text) || string.IsNullOrWhiteSpace(folder.Text))
            {
                error.Text = Loc.Current["project.utmt.needFields"];
                Motion.Shake(string.IsNullOrWhiteSpace(data.Text) ? data : folder);
                return;
            }

            answer = new UtmtImportRequest(info.MainFile, data.Text.Trim(), folder.Text.Trim(), git.IsChecked == true, scripts.IsChecked == true);
            window.Close();
        });
        Frame(window, new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Loc.Current["project.utmt.intro"], TextWrapping = TextWrapping.Wrap },
                new TextBlock { Name = "ProjectLine", Text = Loc.Current.Format("project.utmt.project", ("name", info.Name), ("count", info.Total)), TextWrapping = TextWrapping.Wrap },
                dataCaption,
                new DockPanel { LastChildFill = true, Children = { browseData, data } },
                folderCaption,
                new DockPanel { LastChildFill = true, Children = { browseFolder, folder } },
                git,
                scripts,
                error,
                Buttons(convert, Btn(window, "common.cancel", "Cancel", window.Close)),
            },
        }, DialogFrame.Snapshot(data, folder, git, scripts));
        await window.ShowDialog(owner);
        return answer;
    }

    /// <summary>The result of a conversion: what was written, the warnings, and the counts. True when the user wants the project opened.</summary>
    public static async Task<bool> ShowUtmtReportAsync(Window owner, UtmtImportReport report)
    {
        var window = Shell("project.utmt.report.title", 620);
        var open = false;
        var git = Loc.Current[report.Git switch { UtmtGitState.Initialized => "project.utmt.report.gitInitialized", UtmtGitState.Existing => "project.utmt.report.gitExisting", _ => "project.utmt.report.gitNone" }];
        var warnings = new StackPanel { Name = "Warnings", Spacing = 6 };
        foreach (var warning in report.Warnings) warnings.Children.Add(new TextBlock { Text = WarningText(warning), TextWrapping = TextWrapping.Wrap });
        if (report.Warnings.Count == 0) warnings.Children.Add(new TextBlock { Name = "NoWarnings", Text = Loc.Current["project.utmt.report.none"], TextWrapping = TextWrapping.Wrap });
        var counts = string.Join(Environment.NewLine, report.Info.Resources.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{r.Key}: {r.Value}")
            .Append(report.ProjectFile).Append(report.DataFile));
        var details = new Expander
        {
            Name = "Details",
            Header = Loc.Current["project.utmt.report.details"],
            Content = new TextBox { Name = "DetailsText", Text = counts, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 200 },
        };
        Frame(window, new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Name = "Summary", Text = Loc.Current.Format("project.utmt.report.summary", ("count", report.Resources), ("folder", report.ProjectFolder), ("file", report.DataFile)), TextWrapping = TextWrapping.Wrap },
                new TextBlock { Name = "GitLine", Text = git, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = Loc.Current["project.utmt.report.warnings"], FontWeight = FontWeight.SemiBold },
                warnings,
                details,
                Buttons(Btn(window, "project.utmt.report.open", "Ok", () => { open = true; window.Close(); }), Btn(window, "project.utmt.report.close", "Cancel", window.Close)),
            },
        });
        await window.ShowDialog(owner);
        return open;
    }
}
