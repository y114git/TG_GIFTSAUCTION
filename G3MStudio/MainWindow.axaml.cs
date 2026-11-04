using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using G3MLib.DataFile;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;
using G3MStudio.Editor;
using G3MStudio.History;
using G3MStudio.Explorer;
using G3MStudio.Git;
using G3MStudio.Localization;
using G3MStudio.Panels;
using G3MStudio.Rooms;
using G3MLib.DataFile.Models;

namespace G3MStudio;

public sealed partial class MainWindow : Window, IShell, IDisposable
{
    private const double ZoomStep = 0.1, MinZoom = 0.5, MaxZoom = 2;
    private Func<string> _status = () => Loc.Current["app.ready"];
    private CancellationTokenSource? _opening;
    private string _openingName = "";
    private double _openingFraction;
    private bool _closeConfirmed;
    private readonly ViewStateStore _views;
    private string? _viewPath, _viewSaved;
    private bool _restoring;

    private Control? PanelContent(string id) => id switch
    {
        "Explorer" => Explorer,
        "Inspector" => _inspector,
        "History" => _history,
        "Git" => _git,
        "Problems" => _problems,
        "Search" => _search,
        "Usages" => _usages,
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
    private TextureBitmaps _textures = new();
    private readonly OutputPanel _output = new();
    private readonly Dictionary<string, CodeEditorTab> _codeTabs = [];
    private readonly Dictionary<string, SpriteFramesView> _frameViews = [];
    private readonly Dictionary<string, RoomPreview> _roomViews = [];

    private Dictionary<string, double[]>? _savedRooms;

    private RoomPreview RememberRoom(string tabId, RoomPreview view, string key)
    {
        view.PlaceSource = () => Explorer.SelectedKey;
        if (_savedRooms?.GetValueOrDefault(key) is { } saved) _ = view.RestoreViewAsync(saved);
        return _roomViews[tabId] = view;
    }

    /// <summary>The canvas views of the open room tabs, by resource key.</summary>
    private Dictionary<string, double[]> RoomViewStates() => _roomViews
        .Select(pair => (Docking.Tabs.Get(pair.Key)?.Key, pair.Value))
        .Where(r => r.Key is not null)
        .ToDictionary(r => r.Key ?? "", r => r.Value.ViewState);

    private SpriteFramesView RememberFrames(string tabId, SpriteFramesView view) => _frameViews[tabId] = view;

    private Control? CreateEditor(TabInfo tab)
    {
        if (Workspaces.Current is not { } workspace || ResourceTypes.SplitKey(tab.Key) is not var (kind, entry)) return new TextBlock { Text = tab.Key, Classes = { "muted" } };
        if (tab.Kind != "code")
        {
            var resource = ResourceTypes.Find(workspace.Data, tab.Key);
            Control? header = resource switch
            {
                GameMakerSprite sprite => RememberFrames(tab.Id, new SpriteFramesView(sprite, _textures, workspace.History)),
                GameMakerRoom room => RememberRoom(tab.Id, new RoomPreview(room, _textures, workspace, ShowCreationOrder, ShowCreationCode), tab.Key),
                GameMakerSound sound => new SoundPlayerView(sound, UrlOpener),
                GameMakerGeneralInfo => new EntryScriptsView(workspace, OpenEntry),
                GameMakerVariable => new VariableCountsView(workspace),
                GameMakerGameObject obj => new StackPanel { Spacing = 8, Children = { new ObjectEventsView(obj, OpenEntry, workspace), new PhysicsVerticesView(obj, workspace) } },
                GameMakerBackground background => TilePicker(background, workspace),
                GameMakerFont font => FontTab(font, workspace),
                GameMakerTexturePageItem pageItem => new TextureItemView(pageItem, _textures),
                GameMakerEmbeddedImage image => new TextureItemView(image.TextureEntry, _textures),
                GameMakerPath path => new PathEditor(path, workspace),
                GameMakerParticleSystem system => new StackPanel { Spacing = 8, Children = { new ParticlePreviewView(system), new ParticleEmittersView(system, workspace) } },
                GameMakerSequence sequence => SequencePlayerView.Tab(sequence, _textures, workspace),
                GameMakerAnimationCurve curve => new AnimationCurveView(curve),
                GameMakerShader shader => new ShaderSourceView(shader, workspace),
                GameMakerTimeline timeline => new TimelineStepsView(timeline, workspace, OpenEntry),
                GameMakerEmbeddedTexture page => new TexturePagePreview(page, _textures, workspace.History, workspace.Data.TexturePageItems),
                _ => null,
            };
            // A plugin editor adds a view for any resource type that has no built-in one above.
            header ??= _plugins?.CreateEditorView(kind, resource) as Control;
            var grid = new PropertyGridTab(workspace, kind, entry, header);
            grid.OpenRequested += OpenEntry;
            return grid;
        }

        var editor = new CodeEditorTab(workspace, entry);
        editor.GoToRequested += key => Docking.OpenTab(key.StartsWith("code:", StringComparison.Ordinal) ? "code" : "resource", key, false);
        _codeTabs[tab.Id] = editor;
        editor.Edited += () => Docking.KeepOpen(tab.Id);
        editor.PendingChanged += () =>
        {
            if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = editor.HasPending;
        };
        return editor;
    }

    /// <summary>Writes the tabs, layout and selection of the open data file; nothing while a saved view is being put back.</summary>
    private void SaveView()
    {
        if (_restoring || _viewPath is null) return;
        var state = Docking.Capture(Explorer.SelectedKey) with { Rooms = RoomViewStates() };
        var text = System.Text.Json.JsonSerializer.Serialize(state);
        if (text == _viewSaved) return;
        _viewSaved = text;
        _views.Save(_viewPath, state);
    }

    public MainWindow() : this(DefaultWorkspaces())
    {
    }

    private static WorkspaceService DefaultWorkspaces()
    {
        var userData = UserData.Default();
        return new WorkspaceService(userData, RecentFiles.Load(userData));
    }

    public MainWindow(WorkspaceService workspaces)
    {
        InitializeComponent();
        Workspaces = workspaces;
        _views = new ViewStateStore(workspaces.UserData);
        _inspector = new Panels.InspectorPanel(() => Registry?.Execute("edit.findUsages"), () => Registry?.Execute("edit.findUses"));
        Explorer = new ExplorerView { Name = "Explorer" };
        Docking = new StudioDock(PanelContent, CreateEditor);
        Docking.Attach(DockHost);
        Docking.Changed += SaveView;
        Explorer.MenuRequested += ShowExplorerMenu;
        Explorer.Thumbnail = LoadThumbnailAsync;
        Explorer.OpenRequested += (row, preview) => Docking.OpenTab(row.Key == "code" ? "code" : "resource", ResourceTypes.KeyOf(row.Key, row.Text), preview);
        Explorer.SelectionChanged += SaveView;
        Explorer.SelectionChanged += ShowInInspector;
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
        AskClose = prompt => new ClosePromptWindow(prompt).AskAsync(this);
        Toasts.Bind(Notifications);
        _output.Bind(Notifications);
        _problems.OpenRequested += OpenProblem;
        _search.OpenRequested += hit => OpenCodeAt(hit.Entry, hit.Line, 1);
        _search.BeforeRun = ApplyCodeEditors;
        _usages.OpenRequested += OpenUsage;
        InitProject();
        Notifications.Changed += UpdateTexts;
        NotifyButton.Click += (_, _) => Notifications.DismissAll();
        Docking.Changed += UpdateTexts;
        Docking.Changed += UpdateTabContexts;
        Closed += (_, _) =>
        {
            SaveView();
            Workspaces.Close();
            Dispose();
        };
        CrashReporting.Apply(Workspaces.Settings.Current.CrashReports);
        Workspaces.Settings.Changed += () => CrashReporting.Apply(Workspaces.Settings.Current.CrashReports);
        Registry = AppCommands.Create(this);
        BuildMenu();
        InitPlugins();
        Registry.ShortcutChanged += id => { if (_menuItems.TryGetValue(id, out var item)) item.InputGesture = Shortcuts.Parse(Registry.Get(id).Shortcut); };
        Start.Bind(workspaces.Recent);
        workspaces.Recent.Changed += RefreshRecentMenu;
        RefreshRecentMenu();
        Start.Browse += () => Registry.Execute("file.open");
        Start.OpenPath += path => _ = OpenAsync(path);
        CancelButton.Click += (_, _) =>
        {
            _opening?.Cancel();
            _exporting?.Cancel();
        };
        workspaces.CurrentChanged += ShowWorkspace;
        InitSettings();
        UpdateTexts();
        Loc.Current.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Loc.Current.Language)) UpdateTexts(); };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, (_, e) => OpenDropped(e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>() ?? []));
    }

    public WorkspaceService Workspaces { get; }

    public ExplorerView Explorer { get; }

    public StudioDock Docking { get; }

    public NotificationCenter Notifications { get; } = new();

    /// <summary>Asks the close prompt of ADR-0010 item 6; tests replace it.</summary>
    public Func<ClosePrompt, Task<CloseChoice>> AskClose { get; set; }

    public CommandRegistry Registry { get; }

    public double ZoomFactor { get; private set; } = 1;

    public PaletteWindow? Palette { get; private set; }

    /// <summary>Turns the Sprite and Room menu commands on while a sprite or a room tab is in front.</summary>
    private void UpdateTabContexts()
    {
        var active = Docking.ActiveTabId;
        Registry?.SetContext(AppCommands.SpriteTab, active is not null && _frameViews.ContainsKey(active));
        Registry?.SetContext(AppCommands.RoomTab, active is not null && _roomViews.ContainsKey(active));
        if (active is not null && Docking.Tabs.Get(active)?.Key is { } activeKey && activeKey != Explorer.SelectedKey) Explorer.Select(activeKey);
        ShowInInspector();
    }

    private string? _inspected;

    /// <summary>The Inspector shows the resource of the tab in front, or the Explorer's selection when no tab is open. It only reloads when that changes.</summary>
    private void ShowInInspector()
    {
        _inspector.SetContext(Docking.ActiveTabId is { } active && _roomViews.TryGetValue(active, out var front) ? front.SidePanel : null);
        var key = Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null;
        key ??= Explorer.SelectedKey;
        foreach (var room in _roomViews.Values) room.RefreshPlaceTarget();
        if (key == _inspected) return;
        _inspected = key;
        _ = _inspector.ShowAsync(Workspaces.Current, key);
    }

    private void UpdateTexts()
    {
        Docking.RelabelPanels();
        StatusText.Text = _status();
        var current = Workspaces.Current;
        // "{active resource} - {workspace name} - G3MStudio", a filled dot before the workspace name marks unsaved changes.
        var active = Docking.ActiveTabId is { } tab ? Docking.DocumentOf(tab)?.Title : null;
        Title = current is null
            ? Loc.Current["app.title"]
            : string.Join(" - ", new[] { active, (current.IsDirty ? "● " : "") + current.Name, Loc.Current["app.title"] }.Where(s => !string.IsNullOrEmpty(s)));
        ShowAssetId(current, Docking.ActiveTabId is { } open ? Docking.Tabs.Get(open)?.Key : null);
        ProjectButton.IsVisible = current is not null;
        Registry?.SetContext(AppCommands.ProjectOn, current?.Project is not null);
        Registry?.SetContext(AppCommands.ProjectOff, current is not null && current.Project is null);
        ProjectButton.Content = ProjectText();
        NotifyButton.IsVisible = Notifications.Unread > 0;
        NotifyButton.Content = Loc.Current.Format("notify.button", ("count", Notifications.Unread));
        if (Progress.IsVisible) ShowProgress();
    }

    private (Workspace?, string?, int) _assetId;

    /// <summary>The number of the open resource inside its list, from 0, at the right of the status bar (UTMT-APP-06); found again only when the tab or the list changes.</summary>
    private void ShowAssetId(Workspace? workspace, string? key)
    {
        if (workspace is null || key is null || ResourceTypes.SplitKey(key) is not var (type, _))
        {
            AssetIdText.IsVisible = false;
            return;
        }

        var version = workspace.History.Version;
        if (_assetId.Item1 != workspace || _assetId.Item2 != key || _assetId.Item3 != version)
        {
            var index = ResourceTypes.Find(workspace.Data, key) is { } item && ResourceTypes.All.FirstOrDefault(t => t.Key == type)?.Items(workspace.Data) is { } items ? items.ToList().IndexOf(item) : -1;
            _assetId = (workspace, key, version);
            AssetIdText.Tag = index;
            AssetIdText.Text = index < 0 ? "" : Loc.Current.Format("status.assetId", ("index", index));
        }

        AssetIdText.IsVisible = AssetIdText.Text is { Length: > 0 };
    }

    private void SetStatus(Func<string> status)
    {
        _status = status;
        UpdateTexts();
    }

    private void ShowWorkspace(Workspace? workspace)
    {
        Registry.SetContext(AppCommands.WorkspaceOpen, workspace is not null);
        _inspected = null; // another file: whatever was shown no longer exists
        _ = _inspector.ShowAsync(workspace, null);
        Start.IsVisible = workspace is null;
        WorkArea.IsVisible = workspace is not null;
        _viewPath = null;
        _restoring = true;
        try
        {
            Docking.Tabs.Load([]);
            Docking.Reset();
            _codeTabs.Clear();
            _frameViews.Clear();
            _roomViews.Clear();
            Explorer.Load(workspace?.Name ?? "", workspace?.Data);
            _history.Bind(workspace);
            _problems.Bind(workspace?.Problems);
            _search.Bind(workspace);
            _usages.Clear();
            _textures.Dispose();
            _textures = new TextureBitmaps(workspace?.Lock);
            if (workspace is not null && _views.Load(workspace.Path) is { } saved)
            {
                _savedRooms = saved.Rooms;
                Docking.Restore(saved, tab => ResourceTypes.Find(workspace.Data, tab.Key) is not null);
                Explorer.Select(saved.Selected);
            }
        }
        finally
        {
            _restoring = false;
        }

        _viewPath = workspace?.Path;
        _viewSaved = null;
        if (workspace is not null)
        {
            workspace.History.Recorded += FollowRename;
            workspace.History.Changed += () =>
            {
                Explorer.Refresh(workspace.IsDirty);
                UpdateTexts();
            };
            SetStatus(() => Loc.Current["app.ready"]);
            ShowProject(workspace);
            if (workspace.RecoveredEdits > 0) Notify(NotificationLevel.Info, Loc.Current.Format("app.recovered", ("count", workspace.RecoveredEdits)));
        }
        else
        {
            SetStatus(() => Loc.Current["app.ready"]);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Settings.ShortcutsEditor.Capturing) return; // the Settings page is waiting for keys to assign
        if (Shortcuts.Match(Registry, e) is { } command)
        {
            Registry.Execute(command.Id);
            e.Handled = true;
        }
    }

    public void ShowPalette()
    {
        Palette = new PaletteWindow(Registry, Loc.Current);
        Palette.Closed += (_, _) => Palette = null;
        _ = Palette.ShowDialog(this);
    }

    public void ToggleFullscreen() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    public void Zoom(int step)
    {
        // Rounded so repeated steps do not drift (0.1 is not exact in binary).
        var factor = step == 0 ? 1 : Math.Round(Math.Clamp(ZoomFactor + step * ZoomStep, MinZoom, MaxZoom), 2);
        Workspaces.Settings.Update(s => s with { UiScale = (int)Math.Round(factor * 100) });
        ApplySettings();
    }

    public void Exit() => _ = ExitAsync();

    /// <summary>Asks about unsaved changes first (also the entry for a restart to update); false when the user cancelled.</summary>
    /// <summary>The font tab: its sheet above, then the glyph list; a picked glyph is outlined on the sheet and a click on the sheet picks the glyph under it (UTMT-FNT-03).</summary>
    private StackPanel FontTab(GameMakerFont font, Workspace workspace)
    {
        // A short sheet (the picture scrolls inside it) keeps the glyph list and the kerning pairs in view; a taller fixed size was cut to its middle by a smaller border.
        var sheet = new TextureItemView(font.Texture, _textures) { Height = 150 };
        return new StackPanel { Spacing = 8, Children = { sheet, new FontGlyphsView(font, workspace) { Sheet = sheet, ReplaceRequested = name => _ = ReplaceFontAsync(name) } } };
    }

    public Task<bool> ConfirmCloseAsync() => ApplyCodeEditors() ? CloseGuard.ConfirmAsync(Workspaces.Current, AskClose, SaveForCloseAsync, Workspaces.Settings.Current.AskOnClose) : Task.FromResult(false);

    private async Task ExitAsync()
    {
        if (!await ConfirmCloseAsync()) return;
        _closeConfirmed = true;
        Close();
    }

    private async Task<bool> SaveForCloseAsync(Workspace workspace) => await SaveAsync();

    public void Notify(NotificationLevel level, string text) => Notifications.Post(level, text);


    public void OpenDataFile() => _ = PickAndOpenAsync();

    public void NewDataFile() => _ = NewDataFileAsync();

    /// <summary>Asks where to save, writes an empty GameMaker LTS data file there (UTMT-FILE-01) and opens it. An existing file is never replaced without the picker's own question.</summary>
    public async Task NewDataFileAsync()
    {
        if (await PickPatchFile(Loc.Current["cmd.file.newDataPick"], true, ".win") is not { } path) return;
        try
        {
            using (var data = G3MLib.DataFile.GameMakerData.CreateNew2026LTS())
            await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                await G3MLib.DataFile.GameMakerIO.WriteAsync(stream, data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("cmd.file.newDataFailed", ("reason", e.Message)));
            return;
        }

        await OpenAsync(path);
    }

    public void CloseWorkspace() => _ = CloseWorkspaceAsync();

    public async Task CloseWorkspaceAsync()
    {
        if (await ConfirmCloseAsync()) Workspaces.Close();
    }

    public void Save() => _ = SaveAsync();

    public void Undo()
    {
        if (Workspaces.Current is { History.CanUndo: true } workspace) workspace.Undo();
    }

    public void Redo()
    {
        if (Workspaces.Current is { History.CanRedo: true } workspace) workspace.Redo();
    }

    /// <summary>A renamed resource keeps its tabs: they follow the new name, and back on undo.</summary>
    private void FollowRename(string op, EditCommand command)
    {
        if (command is not RenameResourceCommand rename) return;
        var (from, to) = op == "undo" ? (rename.Args["new"], rename.Args["old"]) : (rename.Args["old"], rename.Args["new"]);
        Docking.Rekey(ResourceTypes.KeyOf(rename.Args["type"], from), ResourceTypes.KeyOf(rename.Args["type"], to));
    }

    public void ShowPanel(string id) => Docking.ShowPanel(id);

    private void OpenProblem(Problem problem) => OpenCodeAt(problem.Source, problem.Line, problem.Column);

    /// <summary>Opens a code entry in a tab and puts the caret on a one-based line and column.</summary>
    private void OpenCodeAt(string entry, int line, int column)
    {
        if (Docking.OpenTab("code", ResourceTypes.KeyOf("code", entry), false) is not { } tab) return;
        Dispatcher.UIThread.Post(() => _ = GoToAsync(tab.Id, line, column), DispatcherPriority.Loaded);
    }

    private async Task GoToAsync(string tabId, int line, int column)
    {
        if (!_codeTabs.TryGetValue(tabId, out var editor)) return;
        await editor.Ready; // the text must be in before a position means anything
        if (_codeTabs.TryGetValue(tabId, out var current) && current == editor) editor.GoTo(line, column);
    }

    /// <summary>Commits the text typed in the open code editors; false (with a notification) when one does not compile.</summary>
    private bool ApplyCodeEditors()
    {
        foreach (var (id, editor) in _codeTabs.ToList())
        {
            if (Docking.Tabs.Get(id) is null)
            {
                _codeTabs.Remove(id);
                continue;
            }

            if (editor.Apply()) continue;
            Notify(NotificationLevel.Error, Loc.Current.Format("code.failed", ("entry", editor.Entry), ("reason", Loc.Current["code.notApplied"])));
            return false;
        }

        return true;
    }

    public void Rename()
    {
        if (Workspaces.Current is null) return;
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name) || type == "general") return;
        _ = new RenameWindow(name, newName => ApplyRename(type, name, newName), preview: RenamePreviewText(type, name)).AskAsync(this);
    }

    /// <summary>The preview line of the rename dialog: the refusal, or how many names change and the first few of them.</summary>
    private Func<string, string?> RenamePreviewText(string type, string name)
    {
        var workspace = Workspaces.Current!;
        var index = new Lazy<G3MLib.DataFile.References.ReferenceIndex>(() => G3MLib.DataFile.References.ReferenceIndex.Build(workspace.Data));
        return newName =>
        {
            RenamePreview found;
            using (workspace.Lock.Read()) found = RenamePreview.Of(workspace.Data, type, name, newName, index.Value);
            if (found.Refusal is not null) return found.Refusal;
            var more = found.Changes.Count > 6 ? Environment.NewLine + Loc.Current.Format("rename.more", ("count", found.Changes.Count - 6)) : "";
            return Loc.Current.Format("rename.preview", ("count", found.Changes.Count), ("uses", found.Uses)) + Environment.NewLine + string.Join(Environment.NewLine, found.Changes.Take(6)) + more;
        };
    }

    private string? ApplyRename(string type, string name, string newName)
    {
        try
        {
            Workspaces.Current?.Edit(new RenameResourceCommand(type, name, newName));
            return null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return e.Message;
        }
    }

    public void CloseTab()
    {
        if (Docking.ActiveTabId is not { } id) return;
        // Closing a tab applies its typed code first; one that does not compile stays open.
        if (_codeTabs.TryGetValue(id, out var editor) && !editor.Apply()) return;
        Docking.CloseTab(id);
    }

    public void CloseAllTabs() => Docking.CloseAll();

    public void CloseOtherTabs()
    {
        if (Docking.ActiveTabId is { } id && ApplyCodeEditors()) Docking.CloseOthers(id);
    }

    private bool Exists(string kind, string key) => Workspaces.Current is { } workspace && ResourceTypes.Find(workspace.Data, key) is not null;

    public void NextTab() => Docking.CycleTab(1);

    public void PreviousTab() => Docking.CycleTab(-1);

    public void CopyName()
    {
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || Clipboard is not { } clipboard) return;
        _ = clipboard.SetTextAsync(ResourceTypes.SplitKey(key) is var (_, name) ? name : key);
    }

    public void OpenDataFolder()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Workspaces.UserData.Root, UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Notify(NotificationLevel.Info, Workspaces.UserData.Root); // no file manager here: the path is the answer
        }
    }

    public void ReopenTab() => Docking.ReopenClosed(Exists);

    public void TabBack() => Navigate(-1);

    public void TabForward() => Navigate(1);

    // The tab in front shows the other resource, so code typed in it is applied first; one that does not compile stays.
    private void Navigate(int direction)
    {
        if (Docking.ActiveTabId is { } id && _codeTabs.TryGetValue(id, out var editor) && !editor.Apply()) return;
        // What the old content registered for the tab goes before the new content registers its own.
        Docking.Navigate(direction, Exists, shown =>
        {
            _codeTabs.Remove(shown);
            _roomViews.Remove(shown);
            _frameViews.Remove(shown);
        });
    }

    public void PinTab()
    {
        if (Docking.ActiveTabId is { } id && Docking.Tabs.Get(id) is { } tab) Docking.SetPinned(id, !tab.Pinned);
    }

    public void KeepTabOpen()
    {
        if (Docking.ActiveTabId is { } id) Docking.KeepOpen(id);
    }

    public void SplitRight()
    {
        if (Docking.ActiveTabId is { } id) Docking.SplitTab(id, Dock.Model.Core.DockOperation.Right);
    }

    private async Task PickAndOpenAsync()
    {
        if (!StorageProvider.CanOpen) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GameMaker data") { Patterns = ["*.win", "*.unx", "*.ios", "*.droid", "audiogroup*.dat"] }, FilePickerFileTypes.All],
        });
        if (files is [{ } file, ..] && file.TryGetLocalPath() is { } path) await OpenAsync(path);
    }

    private string? FrontTabKey() => Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null;

    /// <summary>Pictures, sounds and GML files dropped on an open file become resources; otherwise the first file opens and the others are ignored.</summary>
    public void OpenDropped(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        if (Workspaces.Current is { } open && list is [var one] && FrontTabKey() is { } front && ResourceTypes.SplitKey(front) is var (frontType, frontName))
        {
            // One picture on a sprite tab goes onto the selected frame, one sound file on a sound tab replaces its audio; anywhere else a file makes a new resource.
            if (frontType == "sprites" && FileImport.TypeOf(one) == "sprites" && Docking.ActiveTabId is { } sid && _frameViews.TryGetValue(sid, out var frames))
            {
                _ = ReplaceFrameFromAsync(open, frontName, frames.SelectedFrame, one);
                return;
            }

            if (frontType == "sounds" && FileImport.TypeOf(one) == "sounds")
            {
                _ = ReplaceAudioFromAsync(open, frontName, one);
                return;
            }
        }

        if (Workspaces.Current is not null && list.Any(p => FileImport.TypeOf(p) is not null)) ImportFilePaths(list);
        else if (list.FirstOrDefault() is { } path) _ = OpenAsync(path);
    }

    public async Task OpenAsync(string path, bool askAboutUnsaved = true)
    {
        // Opening another file closes the current one: unsaved edits get the same question as on close.
        if (askAboutUnsaved && Workspaces.Current is { IsDirty: true } current && current.Path != Path.GetFullPath(path) && !await ConfirmCloseAsync()) return;
        var previous = _opening;
        var source = _opening = new CancellationTokenSource(); // takes over first, so a request that comes in meanwhile cancels this one
        if (previous is not null) await previous.CancelAsync();
        (_openingName, _openingFraction) = (Path.GetFileName(path), 0);
        ShowProgress();
        try
        {
            await Workspaces.OpenAsync(path, new Progress<GameMakerProgress>(p => { _openingFraction = p.Fraction; ShowProgress(); }), source.Token);
            if (Workspaces.Current is { } opened)
            {
                ApplyDecompiler(Workspaces.Settings.Current.Decompiler);
                foreach (var (key, count) in LoadChecks.Find(opened.Data)) Notify(NotificationLevel.Warning, Loc.Current.Format(key, ("count", count)));
            }
        }
        catch (OperationCanceledException)
        {
            // The user (or a newer open) cancelled; nothing to report.
        }
        catch (Exception e)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("app.openFailed", ("name", Path.GetFileName(path)), ("reason", e.Message)));
            _ = OfferRebuildAsync(path);
        }
        finally
        {
            if (_opening == source)
            {
                _opening = null;
                Progress.IsVisible = false;
            }
        }
    }

    private void ShowProgress()
    {
        // A progress report can arrive after its operation ended (Progress<T> posts to the UI thread); it must not bring the overlay back.
        if (_opening is null && _exportName is null) return;
        Progress.IsVisible = true;
        ProgressBar.IsIndeterminate = _exportName is not null && _exportTotal == 0;
        if (_exportName is not null)
        {
            ProgressBar.Value = _exportTotal == 0 ? 0 : (double)_exportDone / _exportTotal;
            ProgressText.Text = _exportTotal == 0 ? Loc.Current.Format("project.working", ("name", _exportName)) : Loc.Current.Format("project.exporting", ("name", _exportName), ("done", _exportDone), ("total", _exportTotal));
            return;
        }

        ProgressBar.Value = _openingFraction;
        ProgressText.Text = Loc.Current.Format("explorer.opening", ("name", _openingName), ("percent", (int)(_openingFraction * 100)));
    }

    private async Task<bool> SaveAsync()
    {
        if (Workspaces.Current is not { } workspace) return true;
        // Save commits the text typed in code editors first (design/shell.md 14.2 item 1); one that does not compile stops it.
        if (!ApplyCodeEditors()) return false;
        // Writing and checking a large file takes seconds: the progress overlay with its Cancel button shows unless an export already owns it.
        using var source = new CancellationTokenSource();
        var overlay = _exporting is null;
        if (overlay)
        {
            _exporting = source;
            (_exportName, _exportDone, _exportTotal) = (workspace.Name, 0, 0);
            ShowProgress();
        }

        try
        {
            return await SaveOrSyncAsync(workspace, async () => workspace.Project is { } link ? await link.SaveAsync(Pipeline, null, source.Token) : await workspace.SaveAsync(Pipeline, null, source.Token));
        }
        finally
        {
            if (overlay)
            {
                _exporting = null;
                _exportName = null;
                Progress.IsVisible = false;
            }
        }
    }

    /// <summary>Runs a save or a sync and tells the user how it went. In Project mode Save may only write the project (null result).</summary>
    private async Task<bool> SaveOrSyncAsync(Workspace workspace, Func<Task<SaveResult?>> save, string? fileName = null)
    {
        var name = fileName ?? Path.GetFileName(workspace.Path);
        try
        {
            var result = await save();
            if (result is null) return true;
            var (level, text) = result switch
            {
                { Saved: true } => (NotificationLevel.Info, Loc.Current.Format("save.done", ("name", name))),
                { Failure: SaveFailure.Locked, Holder: { } holder } => (NotificationLevel.Error, Loc.Current.Format("save.locked", ("name", name), ("holder", holder))),
                { Failure: SaveFailure.Locked } => (NotificationLevel.Error, Loc.Current.Format("save.lockedUnknown", ("name", name))),
                _ => (NotificationLevel.Error, Loc.Current.Format("save.invalid", ("name", name), ("detail", result.Message ?? ""))),
            };
            Notify(level, text);
            return result.Saved;
        }
        catch (OperationCanceledException)
        {
            // Cancelled from the progress overlay: the file on disk is untouched and the edits stay in memory and in the journal.
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or G3MLib.DataFile.GameMakerSerializationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("save.failed", ("name", name), ("reason", e.Message)));
            return false;
        }
    }

    /// <summary>Command line: the first argument that is not a flag is a data file to open.</summary>
    public void OpenStartupFile(IEnumerable<string> args)
    {
        var given = args.ToList();
        if (given.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is not { } path) return;
        if (path.StartsWith(Core.Links.StudioLink.Scheme, StringComparison.OrdinalIgnoreCase)) OpenLink(path);
        else if (given.Contains("--run")) _ = OpenAndRunAsync(path, false);
        else if (given.Contains("--run-with-runtime")) _ = OpenAndRunAsync(path, true);
        else _ = OpenAsync(path);
    }

    /// <summary>The Explorer verb "Run game with G3MStudio": opens the file, then runs the game once it is open.</summary>
    private async Task OpenAndRunAsync(string path, bool pickRuntime)
    {
        await OpenAsync(path);
        if (Workspaces.Current is null) return;
        if (pickRuntime) await RunWithRuntimeAsync();
        else await RunGameAsync();
    }

    /// <summary>Opens what a g3mstudio:// link names: a local data file, or one of the actions. A web address is not downloaded (it would need the confirmation and the cache of the mod import).</summary>
    public void OpenLink(string text)
    {
        switch (Core.Links.StudioLink.Parse(text))
        {
            case { Kind: Core.Links.StudioLinkKind.File, Target: var file }:
                _ = OpenAsync(file);
                break;
            case { Kind: Core.Links.StudioLinkKind.Action, Target: "open" }:
                _ = Registry.Execute("file.open");
                break;
            case { Kind: Core.Links.StudioLinkKind.Action, Target: "patch-tools" }:
                _ = Registry.Execute("mods.applyPatch");
                break;
            case { Kind: Core.Links.StudioLinkKind.Action, Target: "new-mod" }:
                // A new mod starts from the changes of the open game: the Export Mod command, which needs a game open.
                if (!Registry.Execute("mods.exportMod")) Notify(NotificationLevel.Info, Loc.Current["links.needsGame"]);
                break;
            case { Kind: Core.Links.StudioLinkKind.Download, Target: var address }:
                _ = DownloadLinkAsync(address);
                break;
            default:
                Notify(NotificationLevel.Warning, Loc.Current["links.unknown"]);
                break;
        }
    }

    public void Dispose()
    {
        _opening?.Dispose();
        _exporting?.Dispose();
        _game?.Dispose();
        _pluginWatcher?.Dispose();
        _textures.Dispose();
    }
}
