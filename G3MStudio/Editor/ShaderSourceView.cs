using G3MStudio.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;
/// <summary>The shader sources of a shader, one tab per language the shader holds, each in a code view with line numbers. With a workspace a changed source is saved with its Save button (Ctrl+S) as one undo step; Revert puts the stored source back and a drag bar under the source sets its height.</summary>
public sealed class ShaderSourceView : TabControl
{
    private readonly Dictionary<string, AvaloniaEdit.TextEditor> _boxes = [];
    private readonly List<Func<bool>> _savers = []; // one per source tab, in tab order
    private readonly GameMakerShader _shader;

    public ShaderSourceView(GameMakerShader shader, Workspace? workspace = null)
    {
        _shader = shader;
        Name = "ShaderSources";
        Margin = new Thickness(0, 0, 0, 8); // keeps the editor off the property filter below
        (string Title, string Property, string? Text)[] all =
        [
            ("GLSL ES vertex", nameof(GameMakerShader.GLSL_ES_Vertex), shader.GLSL_ES_Vertex?.Content), ("GLSL ES fragment", nameof(GameMakerShader.GLSL_ES_Fragment), shader.GLSL_ES_Fragment?.Content),
            ("GLSL vertex", nameof(GameMakerShader.GLSL_Vertex), shader.GLSL_Vertex?.Content), ("GLSL fragment", nameof(GameMakerShader.GLSL_Fragment), shader.GLSL_Fragment?.Content),
            ("HLSL9 vertex", nameof(GameMakerShader.HLSL9_Vertex), shader.HLSL9_Vertex?.Content), ("HLSL9 fragment", nameof(GameMakerShader.HLSL9_Fragment), shader.HLSL9_Fragment?.Content),
        ];
        var present = all.Where(s => !string.IsNullOrWhiteSpace(s.Text)).ToList();
        Sources = [.. present.Select(s => (s.Title, Text: s.Text ?? string.Empty))];
        foreach (var (title, property, text) in present)
        {
            // The GML colors also suit GLSL comments, numbers and the shared keywords; shader types stay plain.
            var box = GmlColorizer.CreateEditor(text ?? string.Empty, workspace is null, out var colorizer);
            box.Name = "Shader_" + property;
            _boxes[property] = box;
            var search = AvaloniaEdit.Search.SearchPanel.Install(box); // the same find and replace as the code tab
            search.IsReplaceMode = workspace is not null;
            box.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => SearchPanelTexts.Apply(search), Avalonia.Threading.DispatcherPriority.Loaded);
            box.TextChanged += (_, _) =>
            {
                colorizer.Tokenize(box.Text);
                box.TextArea.TextView.Redraw();
            };
            var page = new StackPanel { Spacing = 4 };
            var status = new TextBlock { Name = "ShaderCheck_" + property, Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            void Verify() => status.Text = CheckText(box.Text ?? string.Empty, property);
            box.TextChanged += (_, _) => Verify();
            Verify();
            box.Height = Math.Clamp(((text ?? "").Count(c => c == '\n') + 1) * 20 + 20, 180, 360); // the drag bar below changes it and the next shader opens as it was left
            box.ClipToBounds = true;
            if (workspace is not null)
            {
                // Save and Revert work like the code tab's: both wait for a change, Ctrl+S saves while the source has focus.
                var apply = new Button { Name = "ShaderApply_" + property, Content = Loc.Current["code.save"] };
                var revert = new Button { Name = "ShaderRevert_" + property, Content = Loc.Current["code.revert"] };
                bool Pending() => box.Text != (PropertyModel.Read(shader, property) ?? string.Empty);
                void Gate() => apply.IsEnabled = revert.IsEnabled = Pending();
                void Save()
                {
                    if (box.Text is { } changed && Pending()) workspace.Edit(new SetPropertyCommand("shaders", shader.Name.Content ?? "", property, changed));
                    Gate();
                }

                _savers.Add(() =>
                {
                    var pending = Pending();
                    if (pending) Save();
                    return pending;
                });
                apply.Click += (_, _) => Save();
                revert.Click += (_, _) =>
                {
                    box.Text = PropertyModel.Read(shader, property) ?? string.Empty;
                    Gate();
                };
                box.TextChanged += (_, _) => Gate();
                box.KeyDown += (_, e) =>
                {
                    if (e.Key != Key.S || e.KeyModifiers != Shortcuts.Command || !Pending()) return;
                    Save();
                    e.Handled = true;
                };
                Gate();
                page.Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { apply, revert } });
            }

            page.Children.Add(box);
            page.Children.Add(DragHandle.For(box, "shader.source"));
            page.Children.Add(status);
            Items.Add(new TabItem { Header = title, Content = page, Padding = new Thickness(8, 4) }.TextSize("FontSize.Small")); // snug, so the six tabs stay on one row
        }
    }

    /// <summary>Saves every source with typed changes (the window's save key lands here, and saves the data file next); false when there was nothing to save.</summary>
    public bool SaveAll()
    {
        var saved = false;
        foreach (var save in _savers) saved |= save();
        return saved;
    }

    /// <summary>The sources whose text was typed but not saved yet, by property name.</summary>
    public IReadOnlyDictionary<string, string> Unsaved() => _boxes.Where(b => b.Value.Text != (PropertyModel.Read(_shader, b.Key) ?? string.Empty)).ToDictionary(b => b.Key, b => b.Value.Text ?? string.Empty);

    /// <summary>Puts unsaved text back into a view built again (a language change), with the source tab that was in front.</summary>
    public void Restore(IReadOnlyDictionary<string, string> unsaved, int selected)
    {
        foreach (var (property, text) in unsaved)
            if (_boxes.TryGetValue(property, out var box)) box.Text = text;
        if (selected >= 0 && selected < Items.Count) SelectedIndex = selected;
    }

    /// <summary>The shader fields these tabs edit; the property list leaves them out so a source is not shown twice.</summary>
    public static readonly string[] SourceProperties = [nameof(GameMakerShader.GLSL_ES_Vertex), nameof(GameMakerShader.GLSL_ES_Fragment), nameof(GameMakerShader.GLSL_Vertex), nameof(GameMakerShader.GLSL_Fragment), nameof(GameMakerShader.HLSL9_Vertex), nameof(GameMakerShader.HLSL9_Fragment)];

    /// <summary>The check line of a source: the problems found, or the all clear.</summary>
    public static string CheckText(string source, string property)
    {
        var problems = ShaderCheck.Check(source, property.EndsWith("Vertex", StringComparison.Ordinal), property.StartsWith("HLSL", StringComparison.Ordinal));
        return problems.Count == 0 ? Loc.Current["editors.shaderCheck.ok"] : string.Join("; ", problems.Select(p => Loc.Current[p]));
    }

    protected override Type StyleKeyOverride => typeof(TabControl);

    public IReadOnlyList<(string Title, string Text)> Sources { get; }
}

/// <summary>The code the game runs when it starts and when it ends, as two lists with a box to pick a code entry: Add puts it at the end, Remove takes the picked one out, a double click opens it (UTMT-AST-13, UTMT-AST-14).</summary>
public sealed class EntryScriptsView : StackPanel
{
    private readonly GameMakerData _data;
    private readonly Workspace _workspace;
    private readonly Action<string> _open;
    private readonly List<(string List, ListBox Box)> _lists = [];

    public EntryScriptsView(Workspace workspace, Action<string> open)
    {
        (_workspace, _data, _open) = (workspace, workspace.Data, open);
        Name = "EntryScriptsView";
        Spacing = 8;
        foreach (var (list, title) in new[] { (EntryScriptsCommand.GlobalInit, "entryScripts.globalInit"), (EntryScriptsCommand.GameEnd, "entryScripts.gameEnd") })
        {
            if (_data.GlobalInitScripts is null && list == EntryScriptsCommand.GlobalInit || _data.GameEndScripts is null && list == EntryScriptsCommand.GameEnd) continue;
            var box = new ListBox { Name = "Scripts_" + list, MaxHeight = 120 };
            box.DoubleTapped += (_, _) => { if (box.SelectedItem is string code) _open(ResourceTypes.KeyOf("code", code)); };
            var pick = new AutoCompleteBox { Name = "ScriptPick_" + list, Width = 240, FilterMode = AutoCompleteFilterMode.Contains, ItemsSource = _data.Code.Select(c => c.Name.Content ?? "").ToList() };
            var add = new Button { Name = "ScriptAdd_" + list, Content = Loc.Current["entryScripts.add"] };
            add.Click += (_, _) => Add(list, pick.Text ?? "");
            var remove = new Button { Name = "ScriptRemove_" + list, Content = Loc.Current["timelines.remove"] }.NamedFor("timelines.remove", Loc.Current[title]);
            remove.Click += (_, _) => { if (box.SelectedItem is string code) Remove(list, code); };
            Children.Add(new TextBlock { Text = Loc.Current[title], Classes = { "muted" } });
            Children.Add(box);
            Children.Add(new WrapPanel { ItemSpacing = 6, Children = { pick, add, remove } });
            _lists.Add((list, box));
        }

        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += Reload;
            Reload();
        };
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    public IReadOnlyList<string> Codes(string list) => [.. EntryScriptsCommand.ListOf(_data, list).Select(EntryScriptsCommand.Describe)];

    /// <summary>The same edit as the Add button; false when no code entry has that name or it is already listed.</summary>
    public bool Add(string list, string code)
    {
        if (!_data.Code.Any(c => c.Name.Content == code) || Codes(list).Contains(code)) return false;
        _workspace.Edit(new EntryScriptsCommand(list, EntryScriptsCommand.Add, code));
        return true;
    }

    public void Remove(string list, string code)
    {
        // The list can have changed since it was drawn (an undo elsewhere); a stale row is then just dropped from the view.
        if (Codes(list).Contains(code)) _workspace.Edit(new EntryScriptsCommand(list, EntryScriptsCommand.Remove, code));
        else Reload();
    }

    private void Reload()
    {
        foreach (var (list, box) in _lists) box.ItemsSource = Codes(list);
    }
}

/// <summary>
/// A tile set's texture beside the list of its tile ids: a click on a tile picks its row, a picked row outlines its tile (UTMT-BG-03, UTMT-BG-04). The ids themselves are edited in the property list.
/// </summary>
public sealed class TilePickerView : StackPanel
{
    private readonly GameMakerBackground _background;
    private readonly ListBox _list = new() { Name = "TileIds" };

    public TilePickerView(GameMakerBackground background, TextureBitmaps textures, Workspace workspace, AutoTileStore? autoTiles = null, Func<Task<bool>>? replace = null, Action? edit = null)
    {
        _background = background;
        Name = "TilePickerView";
        Spacing = 4;
        Texture = new TextureItemView(background.Texture, textures, replace, edit, workspace.History);
        Texture.Picked += (x, y) =>
        {
            var index = TileGeometry.IndexAt(_background, x, y);
            Pick(index);
            if (AutoTiles is { Armed: true } rules && index >= 0) rules.Assign(index);
        };
        _list.SelectionChanged += (_, _) => Texture.Highlight(_list.SelectedIndex is >= 0 and var i ? TileGeometry.RectOf(_background, i) : null);
        // Sections, top to bottom: the picture (what the tile set looks like), the tile ids, then the rarely used parts folded away.
        Children.Add(Heading("editors.tiles.sheetTitle"));
        Children.Add(new TextBlock { Text = Loc.Current["editors.tiles.sheetHint"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Children.Add(new TileSetInfoView(background, workspace));
        var overlay = new TileIndexOverlay(background, () => Texture.Zoom) { IsVisible = false };
        Texture.AddOverlay(overlay);
        var indices = new CheckBox { Name = "TileIndices", Content = Loc.Current["editors.tiles.indices"] };
        indices.IsCheckedChanged += (_, _) => overlay.IsVisible = indices.IsChecked == true;
        var gridLines = new TileGridOverlay(background, () => Texture.Zoom) { IsVisible = false };
        Texture.AddOverlay(gridLines);
        var grid = new CheckBox { Name = "TileGridLines", Content = Loc.Current["editors.tiles.gridLines"] };
        grid.IsCheckedChanged += (_, _) => gridLines.IsVisible = grid.IsChecked == true;
        Children.Add(new WrapPanel { ItemSpacing = 12, LineSpacing = 4, Children = { grid, indices } });
        Texture.Height = 260;
        Children.Add(Texture);
        Children.Add(DragHandle.For(Texture, "tiles.sheet"));
        var find = new Button { Name = "FindTile", Content = Loc.Current["tileSet.findRooms"] }.GateKey("editors.tiles.findFirst");
        find.Click += (_, _) => { if (Selected >= 0 || _picked >= 0) FindRequested?.Invoke(_picked); };
        _findButton = find;
        _list.MaxHeight = double.PositiveInfinity;
        _list.Height = 140;
        _ids = new StackPanel { Spacing = 4, Children = { Heading("editors.tiles.idsTitle"), new TextBlock { Text = Loc.Current["editors.tiles.idsHint"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, _list, DragHandle.For(_list, "tiles.ids") } };
        Children.Add(_ids);
        Children.Add(find);
        Children.Add(Folded("editors.tiles.animated", new TileAnimationView(background, workspace)));
        if (autoTiles is not null) Children.Add(Folded("autoTile.title", AutoTiles = new AutoTileView(background, autoTiles)));
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    public TextureItemView Texture { get; }

    /// <summary>The auto-tile rule set editor under the tile list; null when the window gave no store.</summary>
    public AutoTileView? AutoTiles { get; }

    private readonly Button _findButton;
    private readonly StackPanel _ids;
    private int _picked = -1;

    private static TextBlock Heading(string key) => new() { Text = Loc.Current[key], FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) };

    // Advanced parts start folded: the expander header names them and the part inside keeps its own controls.
    private static Expander Folded(string key, Control content) => new() { Header = Loc.Current[key], Content = content, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>Raised with the picked tile index when the user asks which rooms use that tile.</summary>
    public event Action<int>? FindRequested;

    /// <summary>Outlines a tile index (as a click on the picture does) and selects its row when the tile set lists ids; -1 clears both.</summary>
    public void Pick(int index)
    {
        _picked = index;
        _findButton.GateKey(index >= 0 ? null : "editors.tiles.findFirst");
        _list.SelectedIndex = index >= 0 && index < _background.GMS2TileIds.Count ? index : -1;
        // Many tile sets list no ids at all; the tile under the click is outlined either way.
        Texture.Highlight(index >= 0 ? TileGeometry.RectOf(_background, index) : null);
    }

    public int Selected => _list.SelectedIndex;

    private void Reload()
    {
        _ids.IsVisible = _background.GMS2TileIds.Count > 0;
        _list.ItemsSource = _background.GMS2TileIds.Select((t, i) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{i}: {t.ID}")).ToList();
    }
}
