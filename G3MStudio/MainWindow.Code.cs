using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio;

// Code editor commands that need the window: they act on the code tab in front.
public sealed partial class MainWindow
{
    private CodeEditorTab? ActiveCodeTab => Docking.ActiveTabId is { } id && _codeTabs.TryGetValue(id, out var tab) ? tab : null;

    public void CodeCommand(string id) => ActiveCodeTab?.Run(id);

    /// <summary>Wires the window-level requests of a code editor: opening, find usages, rename and hover pictures.</summary>
    private void ConnectCodeTab(CodeEditorTab editor, Workspace workspace)
    {
        editor.Registry = Registry;
        editor.ResourceMenu = FrontResourceMenu;
        editor.FindUsagesRequested += name => _ = ShowNameUsagesAsync(workspace, name, editor);
        editor.RenameResourceRequested += AskRenameResource;
        editor.HelpRequested += word =>
        {
            Docking.ShowPanel("Reference");
            _reference.Show(word);
        };
        editor.ManualRequested += OpenManual;
        editor.RenameAcrossRequested += name =>
        {
            if (ApplyCodeEditors()) _ = CodeRenameDialog.AskAsync(this, workspace, name);
        };
    }

    /// <summary>Opens the code a game function is defined in, with the caret on its declaration.</summary>
    private void OpenDefinition(string key, string name)
    {
        if (Docking.OpenTab("code", key, false) is not { } tab) return;
        Dispatcher.UIThread.Post(async () =>
        {
            if (!_codeTabs.TryGetValue(tab.Id, out var editor)) return;
            await editor.Ready; // the text must be in before a declaration has an offset
            editor.GoToDeclaration(name);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Find Usages for a name typed in code: the usages of the resource of that name, else every place the name is used in code.</summary>
    private async Task ShowNameUsagesAsync(Workspace workspace, string name, CodeEditorTab? from = null)
    {
        // A var local or parameter of the function at the caret belongs to that function alone: its uses there, not every same name in the game.
        if (from?.LocalUsages(name) is { } own)
        {
            Docking.ShowPanel("Usages");
            await _usages.ShowAsync(name, Task.FromResult<IReadOnlyList<UsageHit>>([.. own.Select(h => new UsageHit("code", h.Entry, $"{h.Line}: {h.Text}"))]));
            return;
        }

        ApplyCodeEditorsForSearch();
        Docking.ShowPanel("Usages");
        if (GmlUsages.Resolve(workspace, name) is var (type, resource)) await _usages.ShowAsync(resource, UsageSearch.FindAsync(workspace, type, resource), workspace.Nicknames.Both(type, resource));
        else await _usages.ShowAsync(name, CodeNameUsagesAsync(workspace, name));
    }

    private static async Task<IReadOnlyList<UsageHit>> CodeNameUsagesAsync(Workspace workspace, string name) =>
        [.. (await CodeScan.FindNameAsync(workspace, name)).Select(h => new UsageHit("code", h.Entry, $"{h.Line}: {h.Text}"))];

    /// <summary>The line number at the start of a usage row of code ("12: x = 1;"), or 1.</summary>
    private static int LineOf(string where) => int.TryParse(where.Split(':', 2)[0], out var line) ? line : 1;
}
