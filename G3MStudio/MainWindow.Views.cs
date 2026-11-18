using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.History;
using G3MStudio.Git;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Rooms;
using G3MLib.DataFile.Models;
using G3MStudio.Theme;

namespace G3MStudio;

public sealed partial class MainWindow
{
    internal Control? PanelContent(string id) => id switch
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
        "EmptyEditors" => new TextBlock { Text = Loc.Current["editor.empty"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextAlignment = TextAlignment.Center },
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
    private G3MStudio.Explorer.ThumbnailService? _thumbs; // made in WireExplorer
    private readonly OutputPanel _output = new();
    private readonly Dictionary<string, CodeEditorTab> _codeTabs = [];
    private readonly Dictionary<string, SpriteFramesView> _frameViews = [];
    private readonly Dictionary<string, SpriteEditorView> _spriteViews = [];
    private readonly Dictionary<string, int[]> _savedSprites = []; // a sprite tab built again (language change) takes its zoom and frame back, once
    private readonly Dictionary<string, ImageEditorView> _imageViews = [];
    private readonly Dictionary<string, RoomPreview> _roomViews = [];
    private readonly Dictionary<string, SoundPlayerView> _soundViews = [];
    private readonly Dictionary<string, ShaderSourceView> _shaderViews = [];

    private Dictionary<string, double[]>? _savedRooms;

    private RoomPreview RememberRoom(string tabId, RoomPreview view, string key)
    {
        view.PlaceSource = () => Explorer.SelectedKey;
        view.EditImageRequested += EditImageKey;
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
        Prune(_spriteViews, open);
        PruneImages(open);
        Prune(_roomViews, open);
        PruneSounds(open);
        Prune(_codeTabs, open);
        Prune(_shaderViews, open);
    }

    private static void Prune<T>(Dictionary<string, T> views, HashSet<string> open)
    {
        foreach (var id in views.Keys.Where(id => !open.Contains(id)).ToList()) views.Remove(id);
    }

    private ShaderSourceView RememberShader(string tabId, ShaderSourceView view)
    {
        _shaderViews[tabId] = view;
        return view;
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

    // The player reads the audio once, so a replaced (or restored) audio builds the open sound tabs again.
    private void RefreshSounds()
    {
        foreach (var id in _soundViews.Where(x => x.Value.IsStale).Select(x => x.Key).ToList())
        {
            ForgetSound(id);
            Docking.Rebuild(id);
        }
    }

    private void ForgetSound(string tabId)
    {
        if (_soundViews.Remove(tabId, out var view)) view.StopPlayback();
    }

    private SpriteEditorView RememberEditor(string tabId, SpriteEditorView view, string key)
    {
        _frameViews[tabId] = view.Frames;
        _spriteViews[tabId] = view;
        if (_savedSprites.Remove(key, out var saved)) _ = view.RestoreViewAsync(saved);
        view.Frames.ImportRequested += ImportSheet;
        view.Frames.EditImageRequested += frame =>
        {
            if (Docking.Tabs.Get(tabId)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, sprite)) return;
            _imageFrame = frame;
            EditImageOf("sprites", sprite);
        };
        view.Tools.EditMaskRequested += index =>
        {
            if (Docking.Tabs.Get(tabId)?.Key is { } key && ResourceTypes.SplitKey(key) is var (_, sprite)) EditImageOf("spriteMasks", $"{sprite}#{index}");
        };
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded); // the tab was made after the dock told its listeners, so the menus look again
        return view;
    }

    // The headers of most editors (object events, font glyphs, sound player) read their texts once, so a language change builds the open ones again.
    // A room keeps its canvas view through the rebuild, and a sprite its zoom, frame, onion skin and origin lock.
    // Typed text is never lost: code waiting in an editor is applied first (a tab whose code does not apply keeps its old texts), and shader sources not saved yet are put back.
    // Only the tabs in front are built at once (10 open tabs took 400 to 700 ms); the others are built when they come to the front, see RebuildOwed.
    private readonly HashSet<string> _languageOwed = [];

    private void RebuildEditors()
    {
        if (Workspaces.Current is not { } workspace) return;
        foreach (var tab in Docking.Tabs.Tabs.ToList())
        {
            if (tab.Kind != "resource" || ResourceTypes.Find(workspace.Data, tab.Key) is null) continue;
            if (Docking.IsInFront(tab.Id)) RebuildEditor(tab);
            else _languageOwed.Add(tab.Id);
        }
    }

    /// <summary>Builds the editors that a language change left alone while they were hidden, now that they are in front.</summary>
    private void RebuildOwed()
    {
        if (_languageOwed.Count == 0 || Workspaces.Current is not { } workspace) return;
        foreach (var id in _languageOwed.ToList())
        {
            if (Docking.Tabs.Get(id) is not { } tab) _languageOwed.Remove(id);
            else if (Docking.IsInFront(id))
            {
                _languageOwed.Remove(id);
                if (ResourceTypes.Find(workspace.Data, tab.Key) is not null) RebuildEditor(tab);
            }
        }
    }

    private void RebuildEditor(TabInfo tab)
    {
        if (_codeTabs.TryGetValue(tab.Id, out var code) && !code.Apply())
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("code.failed", ("entry", CodeShown(code.Entry)), ("reason", Loc.Current["code.notApplied"])));
            return;
        }

        if (_roomViews.TryGetValue(tab.Id, out var room)) (_savedRooms ??= [])[tab.Key] = room.ViewState;
        if (_spriteViews.TryGetValue(tab.Id, out var sprite)) _savedSprites[tab.Key] = sprite.ViewState;
        ForgetSound(tab.Id);
        var shader = _shaderViews.GetValueOrDefault(tab.Id);
        var (unsaved, front) = (shader?.Unsaved(), shader?.SelectedIndex ?? -1);
        Docking.Rebuild(tab.Id);
        if (unsaved is { Count: > 0 } && _shaderViews.TryGetValue(tab.Id, out var rebuilt)) rebuilt.Restore(unsaved, front);
    }

    // The splitters of the editors keep what the user dragged in the settings, so it comes back after a restart.
    private PaneMemory PaneSizes => new(key => Workspaces.Settings.Current.PaneSizes.TryGetValue(key, out var size) ? size : null,
        (key, size) => Workspaces.Settings.Update(s => s with { PaneSizes = new Dictionary<string, double>(s.PaneSizes) { [key] = size } }));

    internal Control? CreateEditor(TabInfo tab)
    {
        using var span = Telemetry.Measure("tab.open", "ui.load").Tag("editor", Telemetry.EditorClass(tab.Kind == "resource" && ResourceTypes.SplitKey(tab.Key) is var (type, _) ? type : tab.Kind));
        return BuildEditor(tab);
    }

    private Control? BuildEditor(TabInfo tab)
    {
        if (Workspaces.Current is not { } workspace || ResourceTypes.SplitKey(tab.Key) is not var (kind, entry)) return new TextBlock { Text = tab.Key, Classes = { "muted" } };
        if (tab.Kind == "image") return ImageTab(tab, workspace, kind, entry);
        if (tab.Kind == "text") return TextFileTab(tab, workspace, entry);
        if (tab.Kind == "audio") return AudioTab(tab, workspace, kind, entry);
        if (tab.Kind != "code")
        {
            var resource = ResourceTypes.Find(workspace.Data, tab.Key);
            Control? header = resource switch
            {
                GameMakerSprite sprite => RememberEditor(tab.Id, new SpriteEditorView(sprite, _textures, workspace) { Notify = Notify }, tab.Key),
                GameMakerRoom room => RememberRoom(tab.Id, new RoomPreview(room, _textures, workspace, ShowCreationOrder, ShowCreationCode, ShowInstanceCreationCode) { ResourceMenu = FrontResourceMenu, AutoTiles = AutoTilesOf(workspace) }, tab.Key),
                GameMakerSound sound => RememberSound(tab.Id, new SoundPlayerView(sound, UrlOpener, () => Workspaces.Settings.Current.Audio, workspace.ExternalAudio)),
                GameMakerEmbeddedAudio => AudioTab(tab, workspace, kind, entry),
                GameMakerGeneralInfo => new EntryScriptsView(workspace, OpenEntry),
                GameMakerVariable => new VariableCountsView(workspace),
                GameMakerGameObject obj => new StackPanel { Spacing = 8, Children = { new ObjectLinksView(obj, workspace, OpenEntry, EditImageKey, _textures) { Margin = new Avalonia.Thickness(8, 8, 0, 0) }, new ObjectEventsView(obj, OpenEntry, workspace, code => InlineCode(tab.Id, workspace, code)), new PhysicsVerticesView(obj, workspace) } },
                GameMakerBackground background => TilePicker(background, workspace),
                GameMakerUIRootNode uiLayer => new UiLayerView(uiLayer, workspace),
                GameMakerFont font => FontTab(font, workspace),
                GameMakerTexturePageItem pageItem => DragHandle.Under(new TextureItemView(pageItem, _textures, null, () => EditImageOf(kind, entry), workspace.History), "preview.textureItem"),
                GameMakerEmbeddedImage image => DragHandle.Under(new TextureItemView(image.TextureEntry, _textures, null, () => EditImageOf(kind, entry), workspace.History), "preview.textureItem"),
                GameMakerPath path => new PathEditor(path, workspace),
                GameMakerParticleSystem system => ParticleTab(system, workspace, EditImageKey),
                GameMakerSequence sequence => SequencePlayerView.Tab(sequence, _textures, workspace, EditImageKey),
                GameMakerAnimationCurve curve => new CurveEditor(curve, workspace),
                GameMakerExtension extension => new ExtensionView(extension, workspace, (build, name) => EditOrForceAsync(workspace, build, name)),
                GameMakerShader shader => RememberShader(tab.Id, new ShaderSourceView(shader, workspace)),
                GameMakerTimeline timeline => new TimelineStepsView(timeline, workspace, OpenEntry),
                GameMakerEmbeddedTexture page => DragHandle.Under(new TexturePagePreview(page, _textures, workspace.History, workspace.Data.TexturePageItems, (ExportPage, ReplacePage, ShowTextureGroups, ResizePage), () => EditImageOf(kind, entry)) { FindRequested = item => FindUsagesOfKey(ResourceTypes.KeyOf("texturePageItems", ResourceTypes.NameOf(item))) }, "preview.texturePage"),
                _ => null,
            };
            // A plugin editor adds a view for any resource type that has no built-in one above.
            if (resource is IncludedFilesEntry) return new IncludedFilesView(workspace, Workspaces.UserData, AskRiskyAsync, Notify, path => EditImageOf("includedFiles", path), path => OpenIncludedFile(path));
            header ??= _plugins?.CreateEditorView(kind, resource) as Control;
            var grid = new PropertyGridTab(workspace, kind, entry, header, memory: PaneSizes);
            grid.OpenRequested += OpenEntry;
            grid.FindUsagesRequested += FindUsagesOfKey;
            grid.EditImageRequested += EditImageKey;
            return grid;
        }

        var editor = new CodeEditorTab(workspace, entry);
        editor.GoToRequested += key => Docking.OpenTab(key.StartsWith("code:", StringComparison.Ordinal) ? "code" : "resource", key, false);
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

    private static StackPanel ParticleTab(GameMakerParticleSystem system, Workspace workspace, Action<string> editImage)
    {
        var preview = new ParticlePreviewView(system, workspace);
        return new StackPanel { Spacing = 8, Children = { preview, new ParticleEmittersView(system, workspace, emitter => preview.Solo = emitter, editImage) } };
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

    private bool _viewWriteWarned;

    /// <summary>Writes the tabs, layout and selection of the open data file; nothing while a saved view is being put back.</summary>
    private void SaveView()
    {
        if (_restoring || _viewPath is null) return;
        var state = Docking.Capture(Explorer.SelectedKey) with { Rooms = RoomViewStates(), Layers = Workspaces.Current is { } open ? LayerStore.Of(open.Data).Export() : null, Quick = Explorer.Model.Quick };
        var text = System.Text.Json.JsonSerializer.Serialize(state);
        if (text == _viewSaved) return;
        try
        {
            _views.Save(_viewPath, state);
            _viewSaved = text;
            _viewWriteWarned = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Runs on every selection change: warn once until a write works again, and try again with the next change.
            if (_viewWriteWarned) return;
            _viewWriteWarned = true;
            Notify(NotificationLevel.Warning, Loc.Current.Format("project.dataWriteFailed", ("reason", e.Message)));
        }
    }
}
