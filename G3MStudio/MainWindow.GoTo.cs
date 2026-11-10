using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Localization;
using G3MStudio.Core.Navigation;
using G3MStudio.Editor;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Go To (Ctrl+T): the index over the open data file, the registry, the settings pages and the open tabs.</summary>
public sealed partial class MainWindow
{
    private const int PreviewLines = 40;

    public GoToWindow? GoToBox { get; private set; }

    public void GoTo() => ShowGoTo("", false);

    /// <summary>Ctrl+P: Go To limited to the resources and the code.</summary>
    public void QuickOpen() => ShowGoTo("", true);

    /// <summary>Ctrl+Shift+P: Go To with the command prefix typed; it also works with no data file open.</summary>
    public void ShowPalette() => ShowGoTo(">", false);

    private void ShowGoTo(string text, bool namesOnly)
    {
        var workspace = Workspaces.Current;
        if (workspace is null && text.Length == 0) return;
        var entries = GoToEntries(workspace);
        if (namesOnly) entries = [.. entries.Where(e => e.Kind is GoToKind.Resource or GoToKind.Code)];
        // Recent first: what was opened last, then the open tabs, latest first.
        var recent = Explorer.Model.Quick.Recent.Concat(Docking.Tabs.Tabs.Select(t => t.Key).Reverse()).ToList();
        GoToBox = new GoToWindow(query => GoToSearch.Run(entries, query, recent), entry => GoToPreviewAsync(workspace, entry));
        GoToBox.Closed += (_, _) => GoToBox = null;
        GoToBox.Chosen += OpenGoTo;
        if (text.Length > 0) GoToBox.Text = text;
        if (text == ">") GoToBox.Title = Loc.Current["quick.goTo.paletteTitle"];
        _ = GoToBox.ShowDialog(this);
    }

    /// <summary>The enabled commands as Go To entries; the extra search text of a command that took over a removed duplicate's job is matched too.</summary>
    public static IEnumerable<GoToEntry> CommandEntries(CommandRegistry registry, Localizer loc) =>
        registry.All.Where(registry.IsEnabled).Select(c => new GoToEntry(GoToKind.Command, c.Title ?? PaletteTitle(c.Id, loc), c.Id, "", Shortcuts.Display(Shortcuts.Parse(c.Shortcut)?.ToString()),
            AppCommands.PaletteAlt.TryGetValue(c.Id, out var alt) ? loc[alt] : ""));

    /// <summary>The palette name of a command. The image editor's names repeat the main ones (Copy, Paste), so they carry their menu path: Image > Selection > Copy.</summary>
    public static string PaletteTitle(string id, Localizer loc)
    {
        var title = loc[AppCommands.TitleKey(id)];
        if (id.StartsWith("view.panel.", StringComparison.Ordinal)) return loc.Format("palette.showPanel", ("panel", title));
        if (!id.StartsWith("img.", StringComparison.Ordinal)) return title;
        var group = AppCommands.Submenu("menu.image", id);
        return group is null ? $"{loc["menu.image"]} > {title}" : $"{loc["menu.image"]} > {loc["menu.sub." + group]} > {title}";
    }

    private List<GoToEntry> GoToEntries(Workspace? workspace)
    {
        var list = new List<GoToEntry>();
        foreach (var type in workspace is null ? [] : ResourceTypes.All)
        {
            GoToKind? kind = type.Key switch
            {
                "code" => GoToKind.Code,
                "strings" => GoToKind.Text,
                "functions" => GoToKind.Function,
                "variables" => GoToKind.Variable,
                _ => type.IsData ? null : GoToKind.Resource,
            };
            if (kind is not { } k || workspace is null || type.Items(workspace.Data) is not { } items) continue;
            list.AddRange(items.Select(ResourceTypes.NameOf).Select(name => new GoToEntry(k, name, ResourceTypes.KeyOf(type.Key, name), type.Key)));
        }

        // Built-in names with a description; a constant group is a pattern and not a name to open.
        if (workspace is not null) list.AddRange(GmlNames.Reference.Entries.Where(e => e.Kind != G3MStudio.Core.GmlDocs.RefKind.Constant).Select(e => new GoToEntry(GoToKind.Reference, e.Name, "ref:" + e.Name, "", e.Summary)));
        list.AddRange(CommandEntries(Registry, Loc.Current));
        list.AddRange(AppCommands.SettingsPages.Select(page => new GoToEntry(GoToKind.Setting, Loc.Current["settings." + page], page)));
        list.AddRange(Docking.Tabs.Tabs.Select(t => new GoToEntry(GoToKind.Tab, t.Key[(t.Key.IndexOf(':', StringComparison.Ordinal) + 1)..], t.Key, t.Kind)));
        return list;
    }

    private void OpenGoTo(GoToEntry entry, GoToMode mode)
    {
        switch (entry.Kind)
        {
            case GoToKind.Command:
                Registry.Execute(entry.Key);
                return;
            case GoToKind.Setting:
                ShowSettings(Math.Max(0, Array.IndexOf(AppCommands.SettingsPages, entry.Key)));
                return;
            case GoToKind.Reference:
                Docking.ShowPanel("Reference");
                _reference.Show(entry.Name);
                return;
        }

        var kind = entry.Kind == GoToKind.Tab ? entry.Type : entry.Kind == GoToKind.Code ? "code" : "resource";
        if (Docking.OpenTab(kind, entry.Key, false) is not null && mode == GoToMode.Beside) SplitRight();
    }

    private static async Task<string> GoToPreviewAsync(Workspace? workspace, GoToEntry entry)
    {
        switch (entry.Kind)
        {
            case GoToKind.Command:
                return $"{entry.Name}\n{entry.Detail}".Trim();
            case GoToKind.Setting:
                return entry.Name;
            case GoToKind.Reference:
                return entry.Name + "\n" + entry.Detail;
            case GoToKind.Text:
                return entry.Name.Length > 600 ? entry.Name[..600] + "..." : entry.Name;
        }

        var name = entry.Key[(entry.Key.IndexOf(':', StringComparison.Ordinal) + 1)..];
        if (workspace is null) return name;
        if (entry.Kind != GoToKind.Code && entry.Type != "code") return $"{name}\n{Loc.Current["explorer.type." + entry.Type]}";
        var text = await Task.Run(() =>
        {
            using (workspace.Lock.Read()) return CodeText.Decompile(workspace.Data, name, workspace.DecompileContext());
        });
        return string.Join('\n', text.Split('\n', PreviewLines + 1).Take(PreviewLines));
    }
}
