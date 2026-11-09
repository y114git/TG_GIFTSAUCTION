using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.History;
using G3MStudio.Git;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Rooms;
using G3MLib.DataFile.Models;

namespace G3MStudio;

public sealed partial class MainWindow
{
    private Control? PanelContent(string id) => id switch
    {
        "Explorer" => Explorer,
        "Inspector" => _inspector,
        "History" => _history,
        "Git" => _git,
        "Problems" => _problems,
        "Search" => _search,
        "Usages" => _usages,
        "Reference" => _reference,
        "Output" => _output,
        "EmptyEditors" => new TextBlock { Text = Loc.Current["editor.empty"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextAlignment = TextAlignment.Center },
        _ => null,
    };

    private readonly Panels.InspectorPanel _inspector;
    private readonly HistoryView _history = new();
    private readonly GitPanel _git = new();
    private readonly ProblemsPanel _problems = new();
    private readonly Panels.SearchPanel _search = new();
    private readonly UsagesPanel _usages = new();
    private readonly ReferencePanel _reference = new(G3MStudio.Core.GmlDocs.GmlReference.Default);
    private TextureBitmaps _textures = new();
    private readonly OutputPanel _output = new();
    private readonly Dictionary<string, CodeEditorTab> _codeTabs = [];
    private readonly Dictionary<string, SpriteFramesView> _frameViews = [];
    private readonly Dictionary<string, ImageEditorView> _imageViews = [];
    private readonly Dictionary<string, RoomPreview> _roomViews = [];
    private readonly Dictionary<string, SoundPlayerView> _soundViews = [];

    private Dictionary<string, double[]>? _savedRooms;

    private RoomPreview RememberRoom(string tabId, RoomPreview view, string key)
    {
        view.PlaceSource = () => Explorer.SelectedKey;
        if (_savedRooms?.GetValueOrDefault(key) is { } saved) _ = view.RestoreViewAsync(saved);
        _roomViews[tabId] = view;
        // The tab is made while it is being shown, after the Inspector looked for its layers: look again once the view is registered.
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded); // the tab was made after the dock told its listeners, so the menus and the Inspector look again
        return view;
    }

    /// <summary>The canvas views of the open room tabs, by resource key.</summary>
    private Dictionary<string, double[]> RoomViewStates() => _roomViews
        .Select(pair => (Docking.Tabs.Get(pair.Key)?.Key, pair.Value))
        .Where(r => r.Key is not null)
        .ToDictionary(r => r.Key ?? "", r => r.Value.ViewState);

    // A closed tab's views are dropped here; kept in these lists they held a sprite's pictures or a room's scene alive for the whole session.
    private void PruneViews()
    {
        var open = Docking.Tabs.Tabs.Select(t => t.Id).ToHashSet();
        Prune(_frameViews, open);
        PruneImages(open);
        Prune(_roomViews, open);
        PruneSounds(open);
        Prune(_codeTabs, open);
    }

    private static void Prune<T>(Dictionary<string, T> views, HashSet<string> open)
    {
        foreach (var id in views.Keys.Where(id => !open.Contains(id)).ToList()) views.Remove(id);
    }

    private SoundPlayerView RememberSound(string tabId, SoundPlayerView view)
    {
        _soundViews[tabId] = view;
        return view;
    }

    // A sound stops with its tab.
    private void PruneSounds(HashSet<string> open)
    {
        foreach (var id in _soundViews.Keys.Where(id => !open.Contains(id)).ToList()) ForgetSound(id);
    }

    private void ForgetSound(string tabId)
    {
        if (_soundViews.Remove(tabId, out var view)) view.StopPlayback();
    }

    private SpriteEditorView RememberEditor(string tabId, SpriteEditorView view)
    {
        _frameViews[tabId] = view.Frames;
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded); // the tab was made after the dock told its listeners, so the menus look again
        return view;
    }

    private Control? CreateEditor(TabInfo tab)
    {
        if (Workspaces.Current is not { } workspace || ResourceTypes.SplitKey(tab.Key) is not var (kind, entry)) return new TextBlock { Text = tab.Key, Classes = { "muted" } };
        if (tab.Kind == "image") return ImageTab(tab, workspace, entry);
        if (tab.Kind != "code")
        {
            var resource = ResourceTypes.Find(workspace.Data, tab.Key);
            Control? header = resource switch
            {
                GameMakerSprite sprite => RememberEditor(tab.Id, new SpriteEditorView(sprite, _textures, workspace)),
                GameMakerRoom room => RememberRoom(tab.Id, new RoomPreview(room, _textures, workspace, ShowCreationOrder, ShowCreationCode, ShowInstanceCreationCode) { ResourceMenu = FrontResourceMenu, AutoTiles = AutoTilesOf(workspace) }, tab.Key),
                GameMakerSound sound => RememberSound(tab.Id, new SoundPlayerView(sound, UrlOpener, () => Workspaces.Settings.Current.Audio)),
                GameMakerGeneralInfo => new EntryScriptsView(workspace, OpenEntry),
                GameMakerVariable => new VariableCountsView(workspace),
                GameMakerGameObject obj => new StackPanel { Spacing = 8, Children = { new ObjectEventsView(obj, OpenEntry, workspace, code => InlineCode(tab.Id, workspace, code)), new PhysicsVerticesView(obj, workspace) } },
                GameMakerBackground background => TilePicker(background, workspace),
                GameMakerUIRootNode uiLayer => new UiLayerView(uiLayer, workspace),
                GameMakerFont font => FontTab(font, workspace),
                GameMakerTexturePageItem pageItem => new TextureItemView(pageItem, _textures),
                GameMakerEmbeddedImage image => new TextureItemView(image.TextureEntry, _textures),
                GameMakerPath path => new PathEditor(path, workspace),
                GameMakerParticleSystem system => ParticleTab(system, workspace),
                GameMakerSequence sequence => SequencePlayerView.Tab(sequence, _textures, workspace),
                GameMakerAnimationCurve curve => new CurveEditor(curve, workspace),
                GameMakerExtension extension => new ExtensionView(extension),
                GameMakerShader shader => new ShaderSourceView(shader, workspace),
                GameMakerTimeline timeline => new TimelineStepsView(timeline, workspace, OpenEntry),
                GameMakerEmbeddedTexture page => new TexturePagePreview(page, _textures, workspace.History, workspace.Data.TexturePageItems, (ExportPage, ReplacePage, () => new GroupsWindow(workspace, false).Show(this))),
                _ => null,
            };
            // A plugin editor adds a view for any resource type that has no built-in one above.
            if (resource is IncludedFilesEntry) return new IncludedFilesView(workspace, Workspaces.UserData, AskRiskyAsync, Notify);
            header ??= _plugins?.CreateEditorView(kind, resource) as Control;
            var grid = new PropertyGridTab(workspace, kind, entry, header);
            grid.OpenRequested += OpenEntry;
            return grid;
        }

        var editor = new CodeEditorTab(workspace, entry);
        editor.GoToRequested += key => Docking.OpenTab(key.StartsWith("code:", StringComparison.Ordinal) ? "code" : "resource", key, false);
        editor.DisassemblyRequested += () => new DisassemblyWindow(workspace, entry).Show(this);
        ConnectCodeTab(editor, workspace);
        _codeTabs[tab.Id] = editor;
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded); // the tab was made after the dock told its listeners, so the menus look again
        editor.Edited += () => Docking.KeepOpen(tab.Id);
        editor.PendingChanged += () =>
        {
            if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = editor.HasPending;
        };
        return editor;
    }

    private static StackPanel ParticleTab(GameMakerParticleSystem system, Workspace workspace)
    {
        var preview = new ParticlePreviewView(system, workspace);
        return new StackPanel { Spacing = 8, Children = { preview, new ParticleEmittersView(system, workspace, emitter => preview.Solo = emitter) } };
    }

    // The editor beside an object's events takes the object tab's id, so saving applies it and the close of the tab drops it.
    private CodeEditorTab InlineCode(string tabId, Workspace workspace, string entry)
    {
        var editor = new CodeEditorTab(workspace, entry) { MinHeight = 300 };
        ConnectCodeTab(editor, workspace);
        editor.GoToRequested += key => Docking.OpenTab(key.StartsWith("code:", StringComparison.Ordinal) ? "code" : "resource", key, false);
        editor.Edited += () => Docking.KeepOpen(tabId);
        editor.PendingChanged += () =>
        {
            if (Docking.DocumentOf(tabId) is { } doc) doc.IsModified = editor.HasPending;
        };
        _codeTabs[tabId] = editor;
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded); // the tab was made after the dock told its listeners, so the menus look again
        return editor;
    }

    /// <summary>Writes the tabs, layout and selection of the open data file; nothing while a saved view is being put back.</summary>
    private void SaveView()
    {
        if (_restoring || _viewPath is null) return;
        var state = Docking.Capture(Explorer.SelectedKey) with { Rooms = RoomViewStates(), Layers = Workspaces.Current is { } open ? LayerStore.Of(open.Data).Export() : null, Quick = Explorer.Model.Quick };
        var text = System.Text.Json.JsonSerializer.Serialize(state);
        if (text == _viewSaved) return;
        _viewSaved = text;
        _views.Save(_viewPath, state);
    }
}
