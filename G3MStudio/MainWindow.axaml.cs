using Avalonia.Controls;
using Avalonia.Input;
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
                GameMakerGameObject obj => new ObjectEventsView(obj, OpenEntry),
                GameMakerBackground background => new TextureItemView(background.Texture, _textures),
                GameMakerFont font => new StackPanel { Spacing = 8, Children = { new TextureItemView(font.Texture, _textures), new FontGlyphsView(font, workspace) } },
                GameMakerEmbeddedImage image => new TextureItemView(image.TextureEntry, _textures),
                GameMakerPath path => new PathEditor(path, workspace),
                GameMakerParticleSystem system => new ParticleEmittersView(system, workspace),
                GameMakerSequence sequence => new SequenceTracksView(sequence, workspace),
                GameMakerAnimationCurve curve => new AnimationCurveView(curve),
                GameMakerShader shader => new ShaderSourceView(shader, workspace),
                GameMakerTimeline timeline => new TimelineStepsView(timeline, workspace, OpenEntry),
                GameMakerEmbeddedTexture page => new TexturePagePreview(page, _textures),
                _ => null,
            };
            // A plugin editor adds a view for any resource type that has no built-in one above.
            header ??= _plugins?.CreateEditorView(kind, resource) as Control;
            var grid = new PropertyGridTab(workspace, kind, entry, header);
            grid.OpenRequested += OpenEntry;
            return grid;
        }

        var editor = new CodeEditorTab(workspace, entry);
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
        _inspector = new Panels.InspectorPanel(() => Registry?.Execute("edit.findUsages"));
        Explorer = new ExplorerView { Name = "Explorer" };
        Docking = new StudioDock(PanelContent, CreateEditor);
        Docking.Attach(DockHost);
        Docking.Changed += SaveView;
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
        Registry = AppCommands.Create(this, Loc.Current);
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
        LanguageStatus.Text = Loc.Current.Format("app.language", ("name", Loc.Current.LanguageName));
        StatusText.Text = _status();
        var current = Workspaces.Current;
        // "{active resource} - {workspace name} - G3MStudio", a filled dot before the workspace name marks unsaved changes.
        var active = Docking.ActiveTabId is { } tab ? Docking.DocumentOf(tab)?.Title : null;
        Title = current is null
            ? Loc.Current["app.title"]
            : string.Join(" - ", new[] { active, (current.IsDirty ? "● " : "") + current.Name, Loc.Current["app.title"] }.Where(s => !string.IsNullOrEmpty(s)));
        ProjectButton.IsVisible = current is not null;
        ProjectButton.Content = ProjectText();
        NotifyButton.IsVisible = Notifications.Unread > 0;
        NotifyButton.Content = Loc.Current.Format("notify.button", ("count", Notifications.Unread));
        if (Progress.IsVisible) ShowProgress();
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
        _ = new RenameWindow(name, newName => ApplyRename(type, name, newName)).AskAsync(this);
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

    /// <summary>The first file of a drop or of the command line opens; the others are ignored.</summary>
    public void OpenDropped(IEnumerable<string> paths)
    {
        if (paths.FirstOrDefault() is { } path) _ = OpenAsync(path);
    }

    public async Task OpenAsync(string path)
    {
        var previous = _opening;
        var source = _opening = new CancellationTokenSource(); // takes over first, so a request that comes in meanwhile cancels this one
        if (previous is not null) await previous.CancelAsync();
        (_openingName, _openingFraction) = (Path.GetFileName(path), 0);
        ShowProgress();
        try
        {
            await Workspaces.OpenAsync(path, new Progress<GameMakerProgress>(p => { _openingFraction = p.Fraction; ShowProgress(); }), source.Token);
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
        return await SaveOrSyncAsync(workspace, async () => workspace.Project is { } link ? await link.SaveAsync(Pipeline) : await workspace.SaveAsync(Pipeline));
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("save.failed", ("name", name), ("reason", e.Message)));
            return false;
        }
    }

    /// <summary>Command line: the first argument that is not a flag is a data file to open.</summary>
    public void OpenStartupFile(IEnumerable<string> args)
    {
        if (args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is { } path) _ = OpenAsync(path);
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
