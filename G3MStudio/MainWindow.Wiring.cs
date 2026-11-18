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
        Explorer.OptionsFoldChanged += folded => Workspaces.Settings.Update(x => x with { ExplorerOptionsFolded = folded });
        Explorer.CommandRequested += id => Registry?.Execute(id);
        Explorer.Pictures = _thumbs = new G3MStudio.Explorer.ThumbnailService(() => Workspaces.Current, () => _textures, () => Memory.IsLow);
        G3MStudio.Editor.LinkContext.SetPictures(this, _thumbs);
        AddHandler(G3MStudio.Editor.LinkContext.RequestedEvent, OnLinkRequested);
        // A floated editor or panel is in a window of its own, out of reach of the two above.
        Docking.FloatCreated = window =>
        {
            G3MStudio.Editor.LinkContext.SetPictures(window, _thumbs);
            window.AddHandler(G3MStudio.Editor.LinkContext.RequestedEvent, OnLinkRequested);
        };
        G3MStudio.Explorer.HoverCards.Bind(this,
            key => Workspaces.Current is { } ws ? ResourceHover.Describe(ws.Data, ws.Nicknames, key) : null,
            name => Workspaces.Current is { } ws ? G3MStudio.Editor.GmlNames.CardKey(ws, name) : null,
            (key, cancel) => _thumbs?.GetAsync(key, cancel) ?? Task.FromResult<Avalonia.Media.IImage?>(null));
        Closed += (_, _) => G3MStudio.Explorer.HoverCards.Release(this); // the statics must not keep a closed window alive
        Explorer.OpenRequested += (row, preview) => Docking.OpenTab(row.Key == "code" ? "code" : "resource", ResourceTypes.KeyOf(row.Key, row.Text), preview);
        Explorer.SelectionChanged += SaveView;
        Explorer.SelectionChanged += ShowInInspector;
        Explorer.Model.Changed += ShowInInspector; // a rename or a language change gives the selected header row new text
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
