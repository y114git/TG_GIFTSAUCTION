using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using G3MLib.DataFile;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
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
    private readonly Func<CodeLoad>? _loader;
    private CodeLoad? _shown;

    /// <summary>Where the gesture of file.save is read; null (tests, no shell) keeps the default Ctrl+S.</summary>
    public CommandRegistry? Registry
    {
        get;
        set
        {
            field?.ShortcutChanged -= OnShortcutChanged;
            field = value;
            if (TopLevel.GetTopLevel(this) is not null) value?.ShortcutChanged += OnShortcutChanged;
            ShowApplyTip();
        }
    }

    // The registry belongs to the window and outlives the tab, so the tab listens only while it is on screen.
    private void OnShortcutChanged(string id)
    {
        if (id == "file.save") ShowApplyTip();
    }

    private string SaveShortcut => Registry?.Find("file.save")?.Shortcut ?? "Ctrl+S";

    private void ShowApplyTip() => ToolTip.SetTip(_apply, Loc.Current.Format("code.applyTip", ("shortcut", Shortcuts.Display(SaveShortcut))));

    private bool IsSaveKey(KeyEventArgs e) =>
        Registry?.Find("file.save") is { } save ? save.Gestures.Any(g => Shortcuts.Parse(g) is { } gesture && gesture.Matches(e)) : e.Key == Key.S && e.KeyModifiers == Shortcuts.Command;

    private readonly SelectableTextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Name = "Message", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Button _apply = new() { Name = "Apply" };
    private readonly Button _revert = new() { Name = "Revert" };
    private readonly DispatcherTimer _retokenize = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly SearchPanel _search;
    private readonly FoldingManager _folding;
    private string _applied = "";
    private bool _loading, _applying;
    private bool _retokenizeOwed;
    private int _loadId, _loadedVersion = -1, _loadedRevision = -1;

    /// <param name="workspace">The open data file.</param>
    /// <param name="entry">Name of the code entry.</param>
    /// <param name="loader">Produces the text instead of the decompiler (tests).</param>
    public CodeEditorTab(Workspace workspace, string entry, Func<CodeLoad>? loader = null)
    {
        _workspace = workspace;
        _loader = loader;
        Entry = entry;
        Editor = GmlColorizer.CreateEditor("", readOnly: true, out _colorizer);
        Editor.Name = "Code";
        _colorizer.Classifier = GmlNames.For(workspace);
        _ = new GmlCompletion(Editor, new GmlCompletionSource(GmlNames.Completions(workspace), name => GmlNames.SignatureOf(workspace, name), (doc, dot) => GmlMembers.After(doc, dot, InstanceVariables, MemberContext(workspace)), name => GmlNames.IsGameFunction(workspace, name), () => GmlNames.Aliases(workspace), workspace.Nicknames.CodeLabel, () => _doc));
        InstallGoTo(workspace);
        InstallIntel(workspace);
        InstallLive();
        InstallGrowth();
        _search = SearchPanel.Install(Editor);
        _search.IsReplaceMode = true;
        _folding = FoldingManager.Install(Editor.TextArea);
        ChevronFoldingMargin.Replace(Editor.TextArea, _folding);
        _apply.Content = Loc.Current["code.save"];
        _revert.Content = Loc.Current["code.revert"];
        ShowApplyTip();
        _apply.Click += (_, _) => Apply();
        // The save gesture (rebindable) inside the code applies pending changes like the button; with none, the key goes on to the file save.
        Editor.TextArea.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (!HasPending || !IsSaveKey(e)) return;
            Apply();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _revert.Click += (_, _) => Revert();
        var mode = new TextBlock { Name = "TypingMode", Text = Loc.Current["editor.typingInsert"], Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Editor.TextArea.PropertyChanged += (_, e) =>
        {
            if (e.Property == AvaloniaEdit.Editing.TextArea.OverstrikeModeProperty) mode.Text = Loc.Current[e.NewValue is true ? "editor.typingOverwrite" : "editor.typingInsert"];
        };
        // The buttons and the message wrap onto more lines in a narrow tab or a long language; only the typing mode keeps its place at the end.
        var buttons = new WrapPanel { Children = { _apply, _revert, _assembly, _outlineButton, _message, _copyDetails } };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Margin = new Thickness(8, 4), Children = { buttons, mode } };
        Grid.SetColumn(mode, 1);
        _copyDetails.Click += (_, _) => CopyDetails();
        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        Children.Add(BuildBanner());
        SetDock(Crumbs, Avalonia.Controls.Dock.Top);
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
        WireLifetime(workspace, mode);
        Ready = LoadAsync();
    }

    // A tab follows the model, the language and the shortcuts only while it is shown.
    private void WireLifetime(Workspace workspace, TextBlock mode)
    {
        // A tab that was hidden meanwhile catches up when it is shown again.
        DetachedFromVisualTree += (_, _) =>
        {
            workspace.History.Changed -= OnHistoryChanged;
            workspace.Locks.Changed -= OnLocksChanged;
            // A running timer is a root of the dispatcher: a closed tab must not stay reachable until it fires. The colouring it owed is done when the tab is shown again.
            if (_retokenize.IsEnabled)
            {
                _retokenize.Stop();
                _retokenizeOwed = true;
            }
        };
        void Relabel(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Localizer.Language)) return;
            SearchPanelTexts.Apply(_search);
            _apply.Content = Loc.Current["code.save"];
            _revert.Content = Loc.Current["code.revert"];
            ShowApplyTip();
            RelabelBanner();
            mode.Text = Loc.Current[Editor.TextArea.OverstrikeMode ? "editor.typingOverwrite" : "editor.typingInsert"];
        }

        AttachedToVisualTree += (_, _) =>
        {
            Registry?.ShortcutChanged += OnShortcutChanged;
        };
        DetachedFromVisualTree += (_, _) => Registry?.ShortcutChanged -= OnShortcutChanged;
        LanguageWatch.Follow(this, Relabel);
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += OnHistoryChanged;
            workspace.Locks.Changed += OnLocksChanged;
            OnLocksChanged();
            if (_retokenizeOwed)
            {
                _retokenizeOwed = false;
                _retokenize.Start();
            }

            // A tab shown again keeps its text, caret and scroll unless an edit reached the model while it was hidden.
            if (Ready.IsCompleted && !HasPending && (_loadedVersion != workspace.History.Version || _loadedRevision != workspace.DecompilerRevision)) Ready = LoadAsync();
        };
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
            _workspace.Edit(Mode == CodeMode.Assembly ? new EditAssemblyCommand(Entry, Editor.Text) : new EditCodeCommand(Target, Editor.Text));
            _applied = Editor.Text;
            _workspace.Problems.Clear(Target);
            Show("", false);
            // The compile passed; the code check findings (warnings and hints) come back after the usual pause.
            ScheduleLive();
            return true;
        }
        catch (InvalidOperationException e)
        {
            if (e is CodeCompileException compile)
            {
                _workspace.Problems.Set(Target, compile.Diagnostics);
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
        ScrollToLineShown(position.Line);
    }

    private DispatcherTimer? _scrolling;

    /// <summary>A tab that was just opened is not laid out when its text arrives, and the editor drops a scroll until it is; the scroll is tried again for a few frames until the line shows.</summary>
    private void ScrollToLineShown(int line)
    {
        _scrolling?.Stop();
        bool Shown() => Editor.ViewportHeight > 0 && Editor.TextArea.TextView.GetVisualTopByDocumentLine(line) is var top && top >= Editor.VerticalOffset && top < Editor.VerticalOffset + Editor.ViewportHeight;
        Editor.ScrollToLine(line);
        if (Shown()) return;
        var tries = 0;
        var timer = _scrolling = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) =>
        {
            if (Shown() || ++tries > 40) timer.Stop();
            else Editor.ScrollToLine(line);
        };
        timer.Start();
    }

    /// <summary>
    /// Selects a whole-word match of the name, so a double click in the Usages panel lands on the use itself: on the line when one is given, else the match
    /// after <paramref name="skip"/> others from the top (the first one when there are fewer). Only the line is shown when the name is not found.
    /// </summary>
    public void SelectUse(string name, int skip, int line)
    {
        var document = Editor.Document;
        var (from, to) = line > 0 && line <= document.LineCount ? (document.GetLineByNumber(line).Offset, document.GetLineByNumber(line).EndOffset) : (0, document.TextLength);
        var start = UsageSearch.FindUse(document.Text, name, skip, from, to);
        if (start < 0) return;
        Editor.Select(start, name.Length);
        ScrollToLineShown(document.GetLineByOffset(start).LineNumber);
    }

    /// <summary>Drops the typed text and shows the model's code again.</summary>
    public void Revert()
    {
        _workspace.Problems.Clear(Target);
        Ready = LoadAsync();
    }

    private string Target => Entry;

    /// <summary>The entry (or its owner) is locked in Studio: the text cannot be typed into.</summary>
    private bool Locked => _workspace.Locks.Covers(Entry) || _workspace.Locks.Covers(Target);

    private void OnLocksChanged()
    {
        if (!_loading) Editor.IsReadOnly = Locked;
    }

    private void OnHistoryChanged()
    {
        // An undo, redo or rename reached the model: show it, unless the user is in the middle of typing.
        if (!_applying && !HasPending) Ready = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var id = ++_loadId;
        var version = _workspace.History.Version;
        var revision = _workspace.DecompilerRevision;
        _loading = true;
        Editor.IsReadOnly = true;
        Show(Loc.Current["code.decompiling"], false);
        try
        {
            var load = await Task.Run(Load);
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
            Editor.IsReadOnly = Locked;
            Show("", false);
            ShowFallback(load);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (id == _loadId)
            {
                Editor.IsReadOnly = _shown is null || Locked; // typed text stays editable (and can be discarded) after a failed reload; a tab that never loaded has nothing to edit
                Show(Loc.Current.Format("code.showFailed", ("entry", _workspace.Nicknames.CodeLabel(Entry)), ("reason", e.Message)), true);
            }
        }
        finally
        {
            if (id == _loadId) _loading = false;
            UpdateState(false);
        }
    }

    private CodeLoad Load()
    {
        if (_loader is not null) return _loader();
        using (_workspace.Lock.Read())
        {
            var data = _workspace.Data;
            var context = _workspace.DecompileContext();
            var parentName = data.Code.ByName(Entry)?.ParentEntry?.Name?.Content;
            if (ShowAssembly) return new CodeLoad(CodeText.Disassemble(data, Entry), CodeMode.Assembly, null, parentName);
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
