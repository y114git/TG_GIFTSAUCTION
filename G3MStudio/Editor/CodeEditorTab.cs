using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Core.Localization;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// Minimal code entry editor (design/shell.md 9.2 item 10, 14.4 item 4): the decompiled GML in a highlighted AvaloniaEdit view.
/// Typing is local until Apply, which commits one <see cref="EditCodeCommand"/> to the workspace (undo, redo, journal).
/// shortcut: the text is decompiled once and highlighted by re-tokenizing the whole text 300 ms after a change; add incremental
/// tokens and diagnostics with spans when the full code editor comes in M2.
/// </summary>
public sealed partial class CodeEditorTab : DockPanel
{
    private readonly Workspace _workspace;
    private readonly GmlColorizer _colorizer;
    private readonly Func<bool, CodeLoad>? _loader;
    private CodeLoad? _shown;

    private readonly SelectableTextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Name = "Message", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Button _apply = new() { Name = "Apply" };
    private readonly Button _revert = new() { Name = "Revert" };
    private readonly DispatcherTimer _retokenize = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly SearchPanel _search;
    private readonly FoldingManager _folding;
    private string _applied = "";
    private bool _loading, _applying;
    private int _loadId, _loadedVersion = -1, _loadedRevision = -1;

    /// <param name="workspace">The open data file.</param>
    /// <param name="entry">Name of the code entry.</param>
    /// <param name="loader">Produces the text instead of the decompiler (tests).</param>
    public CodeEditorTab(Workspace workspace, string entry, Func<bool, CodeLoad>? loader = null)
    {
        _workspace = workspace;
        _loader = loader;
        Entry = entry;
        Editor = GmlColorizer.CreateEditor("", readOnly: true, out _colorizer);
        Editor.Name = "Code";
        _colorizer.Classifier = GmlNames.For(workspace);
        _ = new GmlCompletion(Editor, GmlNames.Completions(workspace), name => GmlNames.SignatureOf(workspace, name), (doc, dot) => GmlMembers.After(doc, dot, InstanceVariables, MemberContext(workspace)));
        InstallGoTo(workspace);
        InstallIntel(workspace);
        InstallLive();
        InstallGrowth();
        _search = SearchPanel.Install(Editor);
        _search.IsReplaceMode = true;
        _folding = FoldingManager.Install(Editor.TextArea);
        _apply.Content = Loc.Current["code.apply"];
        _revert.Content = Loc.Current["code.revert"];
        _apply.Click += (_, _) => Apply();
        _revert.Click += (_, _) => Revert();
        var mode = new TextBlock { Name = "TypingMode", Text = Loc.Current["editor.typingInsert"], Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Editor.TextArea.PropertyChanged += (_, e) =>
        {
            if (e.Property == AvaloniaEdit.Editing.TextArea.OverstrikeModeProperty) mode.Text = Loc.Current[e.NewValue is true ? "editor.typingOverwrite" : "editor.typingInsert"];
        };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _apply, _revert } };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(8, 4), Children = { buttons, _message, _copyDetails, mode } };
        Grid.SetColumn(_message, 1);
        Grid.SetColumn(_copyDetails, 2);
        Grid.SetColumn(mode, 3);
        _copyDetails.Click += (_, _) => CopyDetails();
        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        Children.Add(BuildBanner());
        Children.Add(Crumbs);
        Children.Add(DiffPanel());
        Children.Add(Editor);
        Editor.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _retokenize.Stop();
            _retokenize.Start();
            ScheduleLive();
            UpdateState();
        };
        _retokenize.Tick += (_, _) => Retokenize();
        // Only a shown tab follows the model; a tab that was hidden meanwhile catches up when it is shown again.
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= OnHistoryChanged;
        void Relabel(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Localizer.Language)) SearchPanelTexts.Apply(_search);
        }

        AttachedToVisualTree += (_, _) => Loc.Current.PropertyChanged += Relabel;
        DetachedFromVisualTree += (_, _) => Loc.Current.PropertyChanged -= Relabel;
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += OnHistoryChanged;
            // A tab shown again keeps its text, caret and scroll unless an edit reached the model while it was hidden.
            if (Ready.IsCompleted && !HasPending && (_loadedVersion != workspace.History.Version || _loadedRevision != workspace.DecompilerRevision)) Ready = LoadAsync();
        };
        Ready = LoadAsync();
    }

    private GmlMemberContext MemberContext(Workspace workspace)
    {
        var variables = GmlNames.ObjectVariables(workspace);
        return new GmlMemberContext(variables, GmlNames.OwnerObject(Entry, variables));
    }

    private void InstallGoTo(Workspace workspace)
    {
        void GoTo(int offset) => GoToDefinition(offset);

        Editor.TextArea.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.F12 || e.KeyModifiers != Avalonia.Input.KeyModifiers.None) return;
            GoTo(Editor.CaretOffset);
            e.Handled = true;
        };
        // Ctrl+click goes to the name under the pointer; the tunnel keeps the editor from moving its caret first.
        Editor.TextArea.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.KeyModifiers != Avalonia.Input.KeyModifiers.Control || !e.GetCurrentPoint(Editor.TextArea).Properties.IsLeftButtonPressed) return;
            if (Editor.GetPositionFromPoint(e.GetPosition(Editor)) is not { } position) return;
            GoTo(Editor.Document.GetOffset(position.Location));
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // A right click on a number lists the assets with that index; a choice goes to the asset (UTMT-CODE-09).
        Editor.TextArea.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(Editor.TextArea).Properties.IsRightButtonPressed || Editor.GetPositionFromPoint(e.GetPosition(Editor)) is not { } position) return;
            if (NumberAssets.NumberAt(Editor.Text, Editor.Document.GetOffset(position.Location)) is not { } number) return;
            var assets = NumberAssets.Find(workspace.Data, number);
            if (assets.Count == 0) return;
            var menu = new ContextMenu();
            foreach (var (type, name) in assets)
            {
                var item = new MenuItem { Header = Loc.Current["explorer.type." + type] + ": " + name };
                item.Click += (_, _) => GoToRequested?.Invoke(ResourceTypes.KeyOf(type, name));
                menu.Items.Add(item);
            }

            menu.Open(Editor.TextArea);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public string Entry { get; }

    public TextEditor Editor { get; }

    public string Text => Editor.Text;

    /// <summary>Text typed here that is not in the model yet.</summary>
    public bool HasPending => !_loading && Editor.Text != _applied;

    /// <summary>The user typed: the tab stops being a preview tab and shows its modified mark.</summary>
    /// <summary>The Explorer key of a resource the user asked to go to (F12 or Ctrl+click on its name).</summary>
    public event Action<string>? GoToRequested;

    public event Action? Edited;

    public event Action? PendingChanged;

    /// <summary>True once the first decompile finished (or failed).</summary>
    public Task Ready { get; private set; } = Task.CompletedTask;

    /// <summary>Commits the typed text. False when it does not compile; the message shows why and nothing changed.</summary>
    public bool Apply()
    {
        if (!HasPending) return true;
        _applying = true;
        CancelLive();
        try
        {
            _workspace.Edit(Mode == CodeMode.Assembly ? new EditAssemblyCommand(Entry, Editor.Text) : new EditCodeCommand(Entry, Editor.Text));
            _applied = Editor.Text;
            _workspace.Problems.Clear(Entry);
            Show("", false);
            return true;
        }
        catch (InvalidOperationException e)
        {
            if (e is CodeCompileException compile)
            {
                _workspace.Problems.Set(Entry, compile.Diagnostics);
                _problemsVersion = _textVersion;
                RedrawOverlays();
            }

            Show(e.Message, true);
            return false;
        }
        finally
        {
            _applying = false;
            UpdateState(false);
        }
    }

    /// <summary>Opens the search box of the editor (Ctrl+F).</summary>
    public void ShowFind()
    {
        _search.Open();
        // The panel builds its parts when it first shows, so the texts are put on once that has happened.
        Dispatcher.UIThread.Post(() => SearchPanelTexts.Apply(_search), DispatcherPriority.Loaded);
    }

    /// <summary>Puts the caret on a one-based line and column and scrolls there (a double click in the Problems panel).</summary>
    public void GoTo(int line, int column)
    {
        var position = new TextViewPosition(Math.Clamp(line, 1, Editor.Document.LineCount), Math.Max(column, 1));
        Editor.TextArea.Caret.Position = position;
        Editor.ScrollToLine(position.Line);
    }

    /// <summary>Drops the typed text and shows the model's code again.</summary>
    public void Revert()
    {
        _workspace.Problems.Clear(Entry);
        Ready = LoadAsync();
    }

    private void OnHistoryChanged()
    {
        // An undo, redo or rename reached the model: show it, unless the user is in the middle of typing.
        if (!_applying && !HasPending) Ready = LoadAsync();
    }

    private async Task LoadAsync(bool parent = false)
    {
        var id = ++_loadId;
        var version = _workspace.History.Version;
        var revision = _workspace.DecompilerRevision;
        _loading = true;
        Editor.IsReadOnly = true;
        Show(Loc.Current["code.decompiling"], false);
        try
        {
            var shown = _shown;
            var load = await Task.Run(() => Load(parent ? shown : null));
            if (id != _loadId) return;
            (_loadedVersion, _loadedRevision) = (version, revision);
            // The same text again (an edit elsewhere) is left alone, so the caret, scroll and selection stay and nothing is tokenized twice.
            if (Editor.Text != load.Text)
            {
                Editor.Text = load.Text;
                Retokenize();
            }

            (_shown, Mode) = (load, load.Mode);
            _grown.Clear();
            _applied = load.Text;
            _original ??= load.Text;
            Editor.IsReadOnly = load.Mode == CodeMode.Parent;
            Show("", false);
            ShowFallback(load);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (id == _loadId) Show(Loc.Current.Format("code.showFailed", ("entry", Entry), ("reason", e.Message)), true);
        }
        finally
        {
            if (id == _loadId) _loading = false;
            UpdateState(false);
        }
    }

    // parentOf is the failed load whose parent text is wanted, or null for a normal load.
    private CodeLoad Load(CodeLoad? parentOf)
    {
        if (_loader is not null) return _loader(parentOf is not null);
        using (_workspace.Lock.Read())
        {
            var data = _workspace.Data;
            var context = _workspace.DecompileContext();
            if (parentOf is not null) return CodeLoader.LoadParent(parentOf.Reason, parentOf.ParentName, () => CodeText.Decompile(data, parentOf.ParentName ?? Entry, context));
            var parentName = data.Code.ByName(Entry)?.ParentEntry?.Name?.Content;
            return CodeLoader.Load(() => CodeText.Decompile(data, Entry, context), () => CodeText.Disassemble(data, Entry), parentName);
        }
    }

    private void Retokenize()
    {
        _retokenize.Stop();
        var text = Editor.Text;
        _colorizer.Tokenize(text);
        (_doc, _docVersion) = (GmlDoc.FromTokens(text, _colorizer.Tokens), _textVersion);
        _folding.UpdateFoldings(GmlStructure.Foldings(_doc), -1);
        UpdateBreadcrumb();
        RefreshDiff();
        Editor.TextArea.TextView.Redraw();
    }

    private void Show(string text, bool error)
    {
        _message.Text = text;
        ToolTip.SetTip(_message, text.Length > 0 ? text : null);
        _copyDetails.IsVisible = error && text.Length > 0;
        _message.Foreground = error ? (IBrush?)Application.Current?.FindResource("Danger") : (IBrush?)Application.Current?.FindResource("Text.Muted");
    }

    private void UpdateState(bool typed = true)
    {
        _apply.IsEnabled = HasPending;
        _revert.IsEnabled = HasPending;
        if (typed && HasPending) Edited?.Invoke();
        PendingChanged?.Invoke();
    }
}
