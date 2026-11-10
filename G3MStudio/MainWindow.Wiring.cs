using G3MStudio.Core.Workspace;

namespace G3MStudio;

public sealed partial class MainWindow
{
    private void WireExplorer()
    {
        Explorer.MenuRequested += ShowExplorerMenu;
        Explorer.EditQuick = EditQuick;
        Explorer.DropOnto = DropOntoRow;
        Explorer.ViewToggleRequested += ToggleFolderView;
        Explorer.CommandRequested += id => Registry?.Execute(id);
        Explorer.Thumbnail = LoadThumbnailAsync;
        Explorer.OpenRequested += (row, preview) => Docking.OpenTab(row.Key == "code" ? "code" : "resource", ResourceTypes.KeyOf(row.Key, row.Text), preview);
        Explorer.SelectionChanged += SaveView;
        Explorer.SelectionChanged += ShowInInspector;
        Explorer.SelectionChanged += UpdateSelectionContext;
    }

    private void WireClosing()
    {
        // Teardown of the dock after the last save must not overwrite it.
        Closing += (_, e) =>
        {
            if (!_closeConfirmed && Workspaces.Current is { IsDirty: true })
            {
                e.Cancel = true;
                _ = ExitAsync();
                return;
            }

            SaveWindowPlacement();
            SaveView();
            _viewPath = null;
        };
    }
}
