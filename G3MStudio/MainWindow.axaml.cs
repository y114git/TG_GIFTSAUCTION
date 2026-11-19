using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;
using G3MStudio.Editor;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Notifications;
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
        if (workspaces.UserData.Problem is { } problem) Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, Loc.Current.Format("project.dataWriteFailed", ("reason", problem))));
        _views.LayersDamaged += kept => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, Loc.Current.Format("view.layersDamaged", ("file", kept))));
        ReportLockedEdits();
        _organizers = new OrganizerStore(workspaces.UserData);
        _inspector = new Panels.InspectorPanel(FindUsagesOfResource, () => Registry?.Execute("edit.findUses"));
        Explorer = new ExplorerView { Name = "Explorer" };
        Docking = new StudioDock(PanelContent, CreateEditor);
        Docking.Attach(DockHost); Docking.CloseGuard = GuardImageClose;
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
        _usages.DetailOf = UsageDetailAsync;
        WireNicknames();
        _usages.RemoveRequested += hits => _ = RemoveUnreferencedAsync(hits);
        _reference.InsertRequested += text => ActiveCodeTab?.InsertAtCaret(text);
        _reference.ManualRequested += OpenManual;
        _reference.FindUsagesRequested += name => { if (Workspaces.Current is { } open) _ = ShowNameUsagesAsync(open, name); };
        _reference.DefinitionRequested += OpenDefinition;
        _reference.CurrentCode = () => ActiveCodeTab?.Text;
        _reference.GameSource = () => Workspaces.Current is { } open ? Editor.GameFunctions.All(open) : [];
        _reference.GameDetail = name => Workspaces.Current is { } open ? Editor.GameFunctions.Find(open, name) : null;
        _reference.PlaceLabel = entry => Workspaces.Current?.Nicknames.CodeLabel(entry) ?? entry;
        void SaveFavourites(IReadOnlyList<string> names) => Workspaces.Settings.Update(x => x with { FavouriteFunctions = names });
        Editor.EditorOptions.FavouritesChanged += SaveFavourites;
        Closed += (_, _) => Editor.EditorOptions.FavouritesChanged -= SaveFavourites; // the event is static: a closed window must not stay referenced
        InitProject();
        Notifications.Changed += UpdateTexts;
        // The list is built each time it opens, and opening it reads everything.
        var notifyFlyout = new Flyout { Placement = PlacementMode.TopEdgeAlignedRight };
        notifyFlyout.Opening += (_, _) =>
        {
            notifyFlyout.Content = new NotificationList(Notifications);
            Notifications.MarkRead();
        };
        NotifyButton.Flyout = notifyFlyout;
        TabBackButton.Click += (_, _) => Registry?.Execute("tabs.historyBack");
        TabForwardButton.Click += (_, _) => Registry?.Execute("tabs.historyForward");
        Avalonia.Automation.AutomationProperties.SetLiveSetting(StatusText, Avalonia.Automation.AutomationLiveSetting.Polite);
        Docking.Changed += RebuildOwed; // before the handlers that read the views
        Docking.Changed += UpdateTexts;
        Docking.Changed += UpdateTabContexts;
        Docking.Changed += PruneViews;
        Closed += (_, _) =>
        {
            SaveView();
            Workspaces.Close();
            Dispose();
        };
        Registry = AppCommands.Create(this); Registry.Executed += id => { FailureReport.Trail("command", id); Telemetry.Command(id); };
        Telemetry.KnowCommands(Registry.All.Select(c => c.Id));
        BuildMenu();
        InitPlugins();
        Registry.ShortcutChanged += id => { if (_menuItems.TryGetValue(id, out var item)) item.InputGesture = Shortcuts.Parse(Registry.Get(id).Shortcut); };
        Start.Bind(workspaces.Recent);
        workspaces.Recent.Changed += RefreshRecentMenu;
        RefreshRecentMenu();
        Start.Browse += () => Registry.Execute("file.open");
        Start.Settings += () => Registry.Execute("settings.open");
        Start.OpenPath += path => _ = OpenRecentAsync(path);
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

    /// <summary>Asks the close prompt of ADR-0011 item 6; tests replace it.</summary>
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
        CrashReporting.TabChanged(active is not null && Docking.Tabs.Get(active)?.Key is { } tabKey && ResourceTypes.SplitKey(tabKey) is var (tabType, _) ? tabType : null);
        ShowInInspector();
        UpdateSelectionContext();
        _reference.RefreshForCode();
    }

    private static bool IsOfType(string key, string kind) => ResourceTypes.SplitKey(key) is var (type, _) && type == kind;

    private static bool IsSound(string key) => IsOfType(key, "sounds");

    /// <summary>Greys out Rename, Duplicate, Move, Delete and Copy Name until the Explorer or the tab in front holds a resource.</summary>
    private void UpdateSelectionContext()
    {
        Registry?.SetContext(AppCommands.FontTab, (Docking.ActiveTabId is { } fontTab && Docking.Tabs.Get(fontTab)?.Key is { } fontKey && IsOfType(fontKey, "fonts")) || (Explorer.SelectedKey is { } fontRow && IsOfType(fontRow, "fonts")));
        Registry?.SetContext(AppCommands.TileSetTab, (Docking.ActiveTabId is { } setTab && Docking.Tabs.Get(setTab)?.Key is { } setKey && IsOfType(setKey, "tileSets")) || (Explorer.SelectedKey is { } setRow && IsOfType(setRow, "tileSets")));
        Registry?.SetContext(AppCommands.SoundTab, (Docking.ActiveTabId is { } active && Docking.Tabs.Get(active)?.Key is { } soundKey && IsSound(soundKey)) || (Explorer.SelectedKey is { } soundRow && IsSound(soundRow)));
        Registry?.SetContext(AppCommands.ResourceSelected, Workspaces.Current is not null
            && (Explorer.SelectedKeys.Count > 0 || Explorer.SelectedRow is { Kind: ExplorerRowKind.Folder or ExplorerRowKind.Collection } || (Docking.ActiveTabId is { } id && Docking.Tabs.Get(id)?.Key is not null)));
    }

    private string? _inspected;
    private ExplorerRow? _inspectedHeader;

    /// <summary>The Inspector shows the resource of the tab in front, or the Explorer's selection when no tab is open. It only reloads when that changes.</summary>
    private void ShowInInspector()
    {
        _inspector.SetContext(Docking.ActiveTabId is { } active && _roomViews.TryGetValue(active, out var front) ? front.SidePanel : null);
        var key = Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null;
        key ??= Explorer.SelectedKey;
        foreach (var room in _roomViews.Values) room.RefreshPlaceTarget();
        var header = key is null ? Explorer.SelectedHeader : null;
        if (key == _inspected && header == _inspectedHeader) return; // a header row is a record: a new name, count or language makes it unequal
        (_inspected, _inspectedHeader) = (key, header);
        if (header is not null) _inspector.ShowRow(header);
        else _ = _inspector.ShowAsync(Workspaces.Current, key);
    }

    private void UpdateTexts()
    {
        Docking.RelabelPanels();
        StatusText.Text = _status();
        ToolTip.SetTip(StatusText, string.IsNullOrEmpty(StatusText.Text) ? null : StatusText.Text);
        UpdateRunStatus();
        var current = Workspaces.Current;
        // "{active resource} - {workspace name} - G3MStudio", an asterisk after the workspace name marks unsaved changes.
        var active = Docking.ActiveTabId is { } tab ? Docking.DocumentOf(tab)?.Title : null;
        Title = current is null
            ? Loc.Current["app.title"]
            : string.Join(" - ", new[] { active, current.Name + (current.IsDirty ? "*" : ""), Loc.Current["app.title"] }.Where(s => !string.IsNullOrEmpty(s)));
        ShowAssetId(current, Docking.ActiveTabId is { } open ? Docking.Tabs.Get(open)?.Key : null);
        ProjectButton.IsVisible = current is not null;
        Registry?.SetContext(AppCommands.ProjectOn, current?.Project is not null);
        Registry?.SetContext(AppCommands.ProjectOff, current is not null && current.Project is null);
        ProjectButton.Content = ProjectText();
        Avalonia.Automation.AutomationProperties.SetName(ProjectButton, (string)ProjectButton.Content);
        NotifyButton.Content = Notifications.Unread > 0 ? Loc.Current.Format("notify.button", ("count", Notifications.Unread)) : Loc.Current["notify.title"];
        Avalonia.Automation.AutomationProperties.SetName(NotifyButton, (string)NotifyButton.Content);
        if (Progress.IsVisible) ShowProgress();
        ShowMemory();
        UpdateChevrons();
        UpdateTabHistory();
    }

    /// <summary>The chevron of a side points to where the dock goes when it folds, and its tooltip shows the key of the toggle command.</summary>
    private void UpdateChevrons()
    {
        if (Registry is null) return;
        // The mark points where the dock goes: away from the edge it sits on while open, back to it when folded.
        void Set(Button button, string side, string id, string title, Avalonia.Layout.Orientation axis, bool openTowardsEnd)
        {
            button.Content = G3MStudio.Theme.GlyphIcons.Chevron(axis, Docking.IsFolded(side) ? !openTowardsEnd : openTowardsEnd);
            ToolTip.SetTip(button, KeyTips.For(Registry, id, title));
            Avalonia.Automation.AutomationProperties.SetName(button, title);
        }

        Set(FoldLeft, "left", "dock.toggleLeft", Loc.Current["cmd.dock.toggleLeft"], Avalonia.Layout.Orientation.Horizontal, false);
        Set(FoldRight, "right", "dock.toggleRight", Loc.Current["cmd.dock.toggleRight"], Avalonia.Layout.Orientation.Horizontal, true);
        Set(FoldBottom, "bottom", "dock.toggleBottom", Loc.Current["cmd.dock.toggleBottom"], Avalonia.Layout.Orientation.Vertical, true);
    }

    private (Workspace?, string?, int) _assetId;

    /// <summary>The number of the open resource inside its list, from 0, at the right of the status bar (UTMT-APP-06); found again only when the tab or the list changes.</summary>
    private void ShowAssetId(Workspace? workspace, string? key)
    {
        if (workspace is null || key is null || ResourceTypes.SplitKey(key) is null)
        {
            AssetIdText.IsVisible = false;
            return;
        }

        var version = workspace.History.Version;
        if (_assetId.Item1 != workspace || _assetId.Item2 != key || _assetId.Item3 != version)
        {
            var index = ResourceTypes.IdOf(workspace.Data, key) ?? -1;
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

    private bool _resetDue;

    /// <summary>
    /// Renews the layout for another file. When the file closes, its tabs leave first and the layout follows after a frame has dropped their drawing:
    /// swapped at once, the panels move to the new layout while the compositor still points them into the old one, which keeps the last tab view, its editor and the
    /// closed file reachable.
    /// </summary>
    private void ResetDocking(bool afterFrame)
    {
        if (afterFrame)
        {
            Docking.CloseAll();
            if (_resetDue) return;
            _resetDue = true;
            Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                if (_resetDue) ResetDocking(false);
            }, TimeSpan.FromMilliseconds(150));
            return;
        }

        _resetDue = false;
        Docking.Reset();
    }

    private void ShowWorkspace(Workspace? workspace)
    {
        Registry.SetContext(AppCommands.WorkspaceOpen, workspace is not null);
        foreach (var type in ResourceListCommand.Creatable) Registry.SetContext(AppCommands.CanNew(type), workspace is not null && ResourceListCommand.CanHold(workspace.Data, type));
        UpdateSelectionContext();
        CloseFileTools();
        (_inspected, _inspectedHeader) = (null, null); // another file: whatever was shown no longer exists
        _assetId = default; // it names the workspace of the status bar, which must not outlive the file
        _cut = []; // entries cut in one file are not pasted into another
        _ = _inspector.ShowAsync(workspace, null);
        Start.IsVisible = workspace is null;
        WorkArea.IsVisible = workspace is not null;
        _viewPath = null;
        _restoring = true;
        try
        {
            Docking.Tabs.Load([]);
            ResetDocking(workspace is null);
            _codeTabs.Clear();
            _frameViews.Clear();
            _spriteViews.Clear();
            _savedSprites.Clear();
            _roomViews.Clear();
            foreach (var id in _soundViews.Keys.ToList()) ForgetSound(id);
            _health?.Stop();
            _health = null; // another file: its failures are not these
            BindNicknames(workspace);
            Explorer.Load(workspace?.Name ?? "", workspace?.Data);
            UpdateHiding();
            Explorer.Model.Quick = NewQuick();
            Explorer.Model.Organizer = workspace is null ? Organizer.Empty : _organizers.Load(workspace.Path);
            _history.Bind(workspace);
            _problems.Bind(workspace?.Problems);
            _search.Bind(workspace);
            _usages.Clear();
            _deletedMembership.Clear();
            _thumbs?.Clear();
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
                _layerAutosave = new LayerAutosave(LayerStore.Of(workspace.Data), layers => _views.SaveLayers(workspace.Path, layers), LayerAutosave.DefaultDelay) { Enabled = Workspaces.Settings.Current.Image.LayerAutosave };
                _layerAutosave.Failed += e => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, Loc.Current.Format("view.autosaveFailed", ("reason", e.Message))));
                if (saved is not null)
                {
                    _savedRooms = saved.Rooms;
                    Docking.Restore(saved, tab => ResourceTypes.Find(workspace.Data, tab.Key) is not null);
                    Explorer.Model.Quick = saved.Quick ?? NewQuick();
                    Explorer.Select(saved.Selected);
                }
                else Docking.FoldBottomForHeight((double.IsNaN(Height) || Height <= 0 ? Bounds.Height : Height) * 100 / Math.Max(1, Workspaces.Settings.Current.UiScale));
            }
        }
        finally
        {
            _restoring = false;
        }

        _viewPath = workspace?.Path;
        _viewSaved = null;
        ReclaimAfterFile();
        if (workspace is not null)
        {
            workspace.History.Recorded += FollowRename;
            workspace.History.Changed += () =>
            {
                PruneDeletedMembership(workspace.History);
                Explorer.Refresh(workspace.History.Last?.ChangesNames ?? true);
                Explorer.RefreshPictures(workspace.History.Last?.Touches);
                UpdateTexts();
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshSounds); // the history raises Changed under the write lock: the tabs look at the audio after it
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
        var sheetHandle = DragHandle.For(sheet, "font.sheet");
        return new StackPanel { Spacing = 8, Children = { sheet, sheetHandle, new FontGlyphsView(font, workspace) { Sheet = sheet, EditGlyphRequested = EditImageKey, Notify = Notify, ReplaceRequested = name => _ = ReplaceFontAsync(name) } } };
    }

    private Task<bool>? _closeQuestion;

    /// <summary>One question at a time: a second Close, Exit or Ctrl+Q while the prompt is open joins it instead of stacking another prompt.</summary>
    public Task<bool> ConfirmCloseAsync() => _closeQuestion is { IsCompleted: false } asking ? asking : _closeQuestion = ApplyCodeEditors() ? CloseGuard.ConfirmAsync(Workspaces.Current, AskClose, SaveForCloseAsync, Workspaces.Settings.Current.AskOnClose) : Task.FromResult(false);

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
