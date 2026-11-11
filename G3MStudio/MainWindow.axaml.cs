using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;
using G3MStudio.Editor;
using G3MStudio.Explorer;
using G3MStudio.Localization;
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
    private readonly OrganizerStore _organizers;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "Disposed when the window closes and when another data file opens.")]
    private LayerAutosave? _layerAutosave;
    private string? _viewPath, _viewSaved;
    private bool _restoring;

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
        _views.LayersDamaged += kept => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, Loc.Current.Format("view.layersDamaged", ("file", kept))));
        _organizers = new OrganizerStore(workspaces.UserData);
        _inspector = new Panels.InspectorPanel(() => Registry?.Execute("edit.findUsages"), () => Registry?.Execute("edit.findUses"));
        Explorer = new ExplorerView { Name = "Explorer" };
        Docking = new StudioDock(PanelContent, CreateEditor);
        Docking.Attach(DockHost);
        Docking.Changed += SaveView;
        Docking.Opened += tab => { if (!_restoring) EditQuick(q => q.Touch(tab.Key)); };
        DockHost.AddHandler(ContextRequestedEvent, OnDockContext, RoutingStrategies.Tunnel);
        FoldLeft.Click += (_, _) => Registry?.Execute("dock.toggleLeft");
        FoldRight.Click += (_, _) => Registry?.Execute("dock.toggleRight");
        FoldBottom.Click += (_, _) => Registry?.Execute("dock.toggleBottom");
        WireExplorer();
        WireClosing();
        AskClose = AskCloseAsync;
        Toasts.Bind(Notifications);
        InitRun();
        _problems.OpenRequested += OpenProblem;
        _search.OpenRequested += hit => OpenCodeAt(hit.Entry, hit.Line, 1);
        _search.BeforeRun = ApplyCodeEditors;
        _usages.OpenRequested += OpenUsage;
        _usages.RemoveRequested += hits => _ = RemoveUnreferencedAsync(hits);
        _reference.InsertRequested += text => ActiveCodeTab?.InsertAtCaret(text);
        _reference.ManualRequested += OpenManual;
        InitProject();
        Notifications.Changed += UpdateTexts;
        NotifyButton.Click += (_, _) => Notifications.DismissAll();
        Avalonia.Automation.AutomationProperties.SetLiveSetting(StatusText, Avalonia.Automation.AutomationLiveSetting.Polite);
        Docking.Changed += UpdateTexts;
        Docking.Changed += UpdateTabContexts;
        Docking.Changed += PruneViews;
        Closed += (_, _) =>
        {
            SaveView();
            Workspaces.Close();
            Dispose();
        };
        CrashReporting.Apply(Workspaces.Settings.Current.CrashReports);
        Registry = AppCommands.Create(this);
        BuildMenu();
        InitPlugins();
        Registry.ShortcutChanged += id => { if (_menuItems.TryGetValue(id, out var item)) item.InputGesture = Shortcuts.Parse(Registry.Get(id).Shortcut); };
        Start.Bind(workspaces.Recent);
        workspaces.Recent.Changed += RefreshRecentMenu;
        RefreshRecentMenu();
        Start.Browse += () => Registry.Execute("file.open");
        Start.Settings += () => Registry.Execute("settings.open");
        Start.GoTo += () => Registry.Execute("go.goTo");
        Start.OpenPath += path => _ = OpenAsync(path);
        CancelButton.Click += (_, _) =>
        {
            _opening?.Cancel();
            _exporting?.Cancel();
        };
        workspaces.CurrentChanged += ShowWorkspace;
        InitSettings();
        InitMemory();
        UpdateTexts();
        FollowStatics();
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


    /// <summary>Turns the Sprite and Room menu commands on while a sprite or a room tab is in front.</summary>
    private void UpdateTabContexts()
    {
        var active = Docking.ActiveTabId;
        Registry?.SetContext(AppCommands.SpriteTab, active is not null && _frameViews.ContainsKey(active));
        Registry?.SetContext(AppCommands.PictureTab, (active is not null && Docking.Tabs.Get(active)?.Key is { } pictureKey && PictureKey(pictureKey)) || (Explorer.SelectedKey is { } row && PictureKey(row)));
        Registry?.SetContext(AppCommands.RoomTab, active is not null && _roomViews.ContainsKey(active));
        Registry?.SetContext(AppCommands.CodeTab, active is not null && _codeTabs.ContainsKey(active));
        Registry?.SetContext(AppCommands.ImageTab, active is not null && _imageViews.ContainsKey(active));
        if (active is not null && Docking.Tabs.Get(active)?.Key is { } activeKey && activeKey != Explorer.SelectedKey) Explorer.Select(activeKey);
        ShowInInspector();
        UpdateSelectionContext();
    }

    /// <summary>Greys out Rename, Duplicate, Move, Delete and Copy Name until the Explorer or the tab in front holds a resource.</summary>
    private void UpdateSelectionContext() =>
        Registry?.SetContext(AppCommands.ResourceSelected, Workspaces.Current is not null
            && (Explorer.SelectedKeys.Count > 0 || Explorer.SelectedRow is { Kind: ExplorerRowKind.Folder or ExplorerRowKind.Collection } || (Docking.ActiveTabId is { } id && Docking.Tabs.Get(id)?.Key is not null)));

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
        ToolTip.SetTip(StatusText, string.IsNullOrEmpty(StatusText.Text) ? null : StatusText.Text);
        UpdateRunStatus();
        var current = Workspaces.Current;
        // "{active resource} - {workspace name} - G3MStudio", a filled dot before the workspace name marks unsaved changes.
        var active = Docking.ActiveTabId is { } tab ? Docking.DocumentOf(tab)?.Title : null;
        Title = current is null
            ? Loc.Current["app.title"]
            : string.Join(" - ", new[] { active, (current.IsDirty ? "● " : "") + current.Name, Loc.Current["app.title"] }.Where(s => !string.IsNullOrEmpty(s)));
        ShowAssetId(current, Docking.ActiveTabId is { } open ? Docking.Tabs.Get(open)?.Key : null);
        ProjectButton.IsVisible = current is not null;
        Start.GoToButton.IsEnabled = current is not null;
        ToolTip.SetTip(Start.GoToButton, current is null ? Loc.Current["start.goToHint"] : null);
        Registry?.SetContext(AppCommands.ProjectOn, current?.Project is not null);
        Registry?.SetContext(AppCommands.ProjectOff, current is not null && current.Project is null);
        ProjectButton.Content = ProjectText();
        Avalonia.Automation.AutomationProperties.SetName(ProjectButton, (string)ProjectButton.Content);
        NotifyButton.IsVisible = Notifications.Unread > 0;
        NotifyButton.Content = Loc.Current.Format("notify.button", ("count", Notifications.Unread));
        Avalonia.Automation.AutomationProperties.SetName(NotifyButton, (string)NotifyButton.Content);
        if (Progress.IsVisible) ShowProgress();
        ShowMemory();
        UpdateChevrons();
    }

    /// <summary>The chevron of a side points to where the dock goes when it folds, and its tooltip shows the key of the toggle command.</summary>
    private void UpdateChevrons()
    {
        if (Registry is null) return;
        void Set(Button button, string side, string id, string title, string open, string folded)
        {
            button.Content = Docking.IsFolded(side) ? folded : open;
            ToolTip.SetTip(button, KeyTips.For(Registry, id, title));
            Avalonia.Automation.AutomationProperties.SetName(button, title);
        }

        Set(FoldLeft, "left", "dock.toggleLeft", Loc.Current["cmd.dock.toggleLeft"], "«", "»");
        Set(FoldRight, "right", "dock.toggleRight", Loc.Current["cmd.dock.toggleRight"], "»", "«");
        Set(FoldBottom, "bottom", "dock.toggleBottom", Loc.Current["cmd.dock.toggleBottom"], "⌄", "⌃");
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
        foreach (var type in ResourceListCommand.Creatable) Registry.SetContext(AppCommands.CanNew(type), workspace is not null && ResourceListCommand.CanHold(workspace.Data, type));
        UpdateSelectionContext();
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
            foreach (var id in _soundViews.Keys.ToList()) ForgetSound(id);
            Explorer.Load(workspace?.Name ?? "", workspace?.Data);
            Explorer.Model.Quick = NewQuick();
            Explorer.Model.Organizer = workspace is null ? Organizer.Empty : _organizers.Load(workspace.Path);
            _history.Bind(workspace);
            _problems.Bind(workspace?.Problems);
            _search.Bind(workspace);
            _usages.Clear();
            _deletedMembership.Clear();
            _textures.Dispose();
            _textures = new TextureBitmaps(workspace?.Lock);
            _layerAutosave?.Dispose();
            _layerAutosave = null;
            if (workspace is not null)
            {
                var path = workspace.Path;
                var saved = _views.Load(path);
                // The sidecar is newer than the view state whenever the last run did not end cleanly, so it is imported last and wins.
                LayerStore.Of(workspace.Data).ImportSaved(saved?.Layers, _views.LoadLayers(path));
                _layerAutosave = new LayerAutosave(LayerStore.Of(workspace.Data), layers => _views.SaveLayers(path, layers), LayerAutosave.DefaultDelay) { Enabled = Workspaces.Settings.Current.Image.LayerAutosave };
                _layerAutosave.Failed += e => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, Loc.Current.Format("view.autosaveFailed", ("reason", e.Message))));
                if (saved is not null)
                {
                    _savedRooms = saved.Rooms;
                    Docking.Restore(saved, tab => ResourceTypes.Find(workspace.Data, tab.Key) is not null);
                    Explorer.Model.Quick = saved.Quick ?? NewQuick();
                    Explorer.Select(saved.Selected);
                }
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
                PruneDeletedMembership(workspace.History);
                Explorer.Refresh(workspace.IsDirty, workspace.History.Last?.ChangesNames ?? true);
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
        if (Shortcuts.Match(Registry, e) is { } command && !(command.Id.StartsWith("img.", StringComparison.Ordinal) && e.Source is TextBox or NumericUpDown))
        {
            Registry.Execute(command.Id);
            e.Handled = true;
        }
    }

    public void ToggleFullscreen() => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    public void Zoom(int step)
    {
        // Rounded so repeated steps do not drift (0.1 is not exact in binary).
        var factor = Math.Round(Math.Clamp(ZoomFactor + step * ZoomStep, MinZoom, MaxZoom), 2);
        Workspaces.Settings.Update(s => s with { UiScale = step == 0 ? 0 : (int)Math.Round(factor * 100) }); // Ctrl+0 returns to the automatic scale
        ApplySettings();
    }

    public void Exit() => _ = ExitAsync();

    /// <summary>Asks about unsaved changes first (also the entry for a restart to update); false when the user cancelled.</summary>
    /// <summary>The font tab: its sheet above, then the glyph list; a picked glyph is outlined on the sheet and a click on the sheet picks the glyph under it (UTMT-FNT-03).</summary>
    private StackPanel FontTab(GameMakerFont font, Workspace workspace)
    {
        // A short sheet (the picture scrolls inside it) keeps the glyph list and the kerning pairs in view; a taller fixed size was cut to its middle by a smaller border.
        var sheet = new TextureItemView(font.Texture, _textures, () => ReplaceItemPictureAsync(workspace, "fonts", ResourceTypes.NameOf(font)), () => EditImageOf("fonts", ResourceTypes.NameOf(font)), workspace.History) { Height = 150 };
        return new StackPanel { Spacing = 8, Children = { sheet, new FontGlyphsView(font, workspace) { Sheet = sheet, EditGlyphRequested = EditImageKey, Notify = Notify, ReplaceRequested = name => _ = ReplaceFontAsync(name) } } };
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
        KillGame();
        _pluginWatcher?.Dispose();
        _textures.Dispose();
        _thumbs?.Dispose();
    }
}
