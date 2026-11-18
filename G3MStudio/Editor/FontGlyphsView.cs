using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;
/// <summary>The glyphs of a font as a list (code point, character, position on the sheet, size, shift, offset) with a filter that takes a character, a code like U+0041 or a number.</summary>
public sealed class FontGlyphsView : StackPanel
{
    private readonly GameMakerFont _font;
    private readonly Workspace? _workspace;
    private readonly TextBox _filter = new() { Name = "GlyphFilter" };
    private readonly TextBox _newCharacter = new() { Name = "GlyphNew", MinWidth = 70 };
    private readonly ListBox _list = new() { Name = "FontGlyphs", Height = 150 };
    private readonly TextBlock _count = new() { Classes = { "muted" } };
    private readonly ContentControl _glyph = new() { Name = "GlyphProperties", ClipToBounds = true };
    private readonly List<(ushort Character, string Text, int Index)> _rows = [];
    private readonly List<int> _shown = [];
    private static readonly string[] BackdropKeys = ["fonts.backChequer", "fonts.backDark", "fonts.backLight", "fonts.backGreen"];
    private readonly FontPreview _preview;
    private readonly TextBlock _previewNote = new() { Name = "GlyphPreviewNote", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _sample = new() { Name = "GlyphSample" };
    private readonly TextBlock _sampleMissing = new() { Name = "GlyphSampleMissing", Classes = { "muted" } };
    private readonly Button _replace = new() { Name = "FontReplace" };

    /// <summary>Called with the font's name when the user asks to draw it again from a font file; the window picks the file. Without it the button stays disabled.</summary>
    public Action<string>? ReplaceRequested
    {
        get;
        set
        {
            field = value;
            _replace.GateKey(value is null ? "why.readOnly" : null);
        }
    }

    /// <summary>Called with the Explorer key of the picked glyph (type fontGlyphs, name font#index) when the user asks to edit its picture; without it the button stays disabled.</summary>
    public Action<string>? EditGlyphRequested { get; init; }

    /// <summary>Reports a refused edit; without it the refusal is silent.</summary>
    public Action<NotificationLevel, string>? Notify { get; init; }

    /// <summary>The picture of the font's texture, when the tab shows one: a picked glyph is outlined on it and a click on it picks a glyph.</summary>
    public TextureItemView? Sheet
    {
        get;
        init
        {
            field = value;
            if (value is not { } sheet) return;
            sheet.Picked += (x, y) => PickAt(x, y);
            sheet.MarkEditable = _workspace is not null;
            sheet.MarkEdited += SetRectangle;
            sheet.PictureChanged += ShowPreview;
            ShowPreview(); // the picture may already be there
        }
    }

    public FontGlyphsView(GameMakerFont font, Workspace? workspace = null)
    {
        (_font, _workspace) = (font, workspace);
        _preview = new FontPreview(font, () => Sheet?.Picture);
        Name = "FontGlyphsView";
        Spacing = 4;
        _filter.PlaceholderText = Loc.Current["fonts.glyphFilter"];
        Avalonia.Controls.ToolTip.SetTip(_filter, Loc.Current["fonts.charTip"]);
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Refresh();
        };
        Children.Add(new TextBlock { Name = "FontSettings", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Settings(font) });
        if (Warning(font) is { } warning) Children.Add(new TextBlock { Name = "FontMissing", Text = warning, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Children.Add(_count);
        Children.Add(_filter);
        Children.Add(CharacterHint.Follow(_filter, true, "GlyphFilterHint"));
        Children.Add(_list);
        Children.Add(DragHandle.For(_list, "font.glyphList"));
        Children.Add(_glyph);
        if (workspace is not null) BuildEditing(font, workspace);

        Reload();
    }

    private void BuildEditing(GameMakerFont font, Workspace workspace)
    {
        // Picking a glyph opens a property list of its fields; Add makes an empty glyph for a character and Remove deletes the picked one (one undo step each).
        _list.SelectionChanged += (_, _) => ShowOnSheet();
        _list.SelectionChanged += (_, _) =>
            _glyph.Content = _list.SelectedIndex is >= 0 and var row && row < _shown.Count
                ? new ScrollViewer { MaxHeight = 260, Content = new StackPanel { Spacing = 6, Children = { new KerningView(font, _shown[row], workspace), new PropertyGridTab(workspace, "fonts", font.Name.Content ?? "", null, $"Glyphs[{_shown[row]}]") } } }
                : null;
        _newCharacter.PlaceholderText = Loc.Current["fonts.glyphNew"];
        Avalonia.Controls.ToolTip.SetTip(_newCharacter, Loc.Current["fonts.charTip"]);
        var add = new Button { Name = "GlyphAdd", Content = Loc.Current["fonts.glyphAdd"] };
        add.Click += (_, _) => Add();
        var remove = new Button { Name = "GlyphRemove", Content = Loc.Current["timelines.remove"] };
        remove.Click += (_, _) =>
        {
            if (_list.SelectedIndex is >= 0 and var row && row < _shown.Count) workspace.Edit(new FontGlyphCommand(font.Name.Content ?? "", FontGlyphCommand.Remove, _shown[row]));
        };
        var deleteRange = new Button { Name = "GlyphDeleteRange", Content = Loc.Current["fonts.deleteRange"] };
        deleteRange.Click += async (_, _) =>
        {
            var text = _newCharacter.Text ?? "";
            // A long range is hard to undo by eye, so it is confirmed first.
            if (MatchingGlyphs(text) > ConfirmAbove && TopLevel.GetTopLevel(this) is Window owner
                && !await ProjectDialogs.AskConfirmAsync(owner, Loc.Current["fonts.deleteRange"], Loc.Current.Format("fonts.deleteRangeConfirm", ("count", MatchingGlyphs(text)), ("font", workspace.Nicknames.Label("fonts", font.Name.Content ?? ""))))) return;
            DeleteRange(text);
        };
        _replace.Content = Loc.Current["fonts.replace"];
        Avalonia.Controls.ToolTip.SetTip(_replace, Loc.Current["fonts.replaceTip"]);
        _replace.GateKey(ReplaceRequested is null ? "why.readOnly" : null);
        _replace.Click += (_, _) => ReplaceRequested?.Invoke(font.Name.Content ?? "");
        var editGlyph = new Button { Name = "GlyphEditImage", Content = Loc.Current["fonts.editGlyphImage"] };
        string? WhyNoGlyph() => EditGlyphRequested is null ? "why.readOnly" : _list.SelectedIndex is >= 0 and var picked && picked < _shown.Count ? null : "why.selectGlyph";
        editGlyph.GateKey(WhyNoGlyph());
        editGlyph.Click += (_, _) =>
        {
            if (_list.SelectedIndex is >= 0 and var row && row < _shown.Count) EditGlyphRequested?.Invoke(ResourceTypes.KeyOf("fontGlyphs", $"{font.Name.Content}#{_shown[row]}"));
        };
        _list.SelectionChanged += (_, _) => editGlyph.GateKey(WhyNoGlyph());
        Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { _newCharacter, add, remove, editGlyph, deleteRange, _replace } });
        Children.Add(CharacterHint.Follow(_newCharacter, true, "GlyphNewHint"));
        var presets = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        foreach (var (name, key) in PresetKeys)
        {
            var button = new Button { Name = "GlyphPreset" + name, Content = Loc.Current[key] };
            Avalonia.Controls.ToolTip.SetTip(button, Loc.Current["fonts.presetTip"]);
            button.Click += async (_, _) => await AddConfirmedAsync(Preset(name));
            presets.Children.Add(button);
        }

        Children.Add(presets);
        var fromText = FromTextPanel(out var fromTextToggle);
        presets.Children.Add(fromTextToggle);
        Children.Add(fromText);
        BuildPreview();
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += Reload;
            Reload(); // a tab that was hidden missed the changes made meanwhile
        };
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
    }

    private void BuildPreview()
    {
        // The preview: a label, the text field with a button after it, the look options, then the picture drawn from the font's own sheet.
        Children.Add(new TextBlock { Text = Loc.Current["fonts.previewTitle"], FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Avalonia.Thickness(0, 8, 0, 0) });
        _sample.PlaceholderText = Loc.Current["fonts.sampleText"];
        _sample.Text = Loc.Current["fonts.sampleDefault"];
        _preview.Text = _sample.Text;
        _sample.TextChanged += (_, _) =>
        {
            _sampleMissing.Text = MissingText(_sample.Text ?? "");
            _preview.Text = _sample.Text ?? "";
            _preview.InvalidateMeasure();
            _preview.InvalidateVisual();
        };
        var allGlyphs = new Button { Name = "GlyphPreviewAll", Content = Loc.Current["fonts.previewAll"] };
        Avalonia.Controls.ToolTip.SetTip(allGlyphs, Loc.Current["fonts.previewAllTip"]);
        allGlyphs.Click += (_, _) => _sample.Text = string.Concat(_font.Glyphs.Select(g => g.Character).Where(c => !char.IsControl((char)c)).Distinct().Select(c => (char)c));
        var field = new DockPanel { Children = { allGlyphs, _sample } };
        DockPanel.SetDock(allGlyphs, Avalonia.Controls.Dock.Right);
        allGlyphs.Margin = new Avalonia.Thickness(6, 0, 0, 0);
        Children.Add(field);
        var backdrop = new ComboBox { Name = "GlyphPreviewBackdrop", ItemsSource = BackdropKeys.Select(k => Loc.Current[k]).ToList(), SelectedIndex = 0 };
        backdrop.SelectionChanged += (_, _) => _preview.Background = (FontBackdrop)Math.Max(0, backdrop.SelectedIndex);
        var zoom = new ComboBox { Name = "GlyphPreviewZoom", ItemsSource = new[] { "1x", "2x", "3x", "4x", "6x", "8x" }, SelectedIndex = 1 };
        zoom.SelectionChanged += (_, _) => _preview.Scale = new[] { 1, 2, 3, 4, 6, 8 }[Math.Max(0, zoom.SelectedIndex)];
        var grid = new CheckBox { Name = "GlyphPreviewGrid", Content = Loc.Current["fonts.previewGrid"] };
        grid.IsCheckedChanged += (_, _) => _preview.Grid = grid.IsChecked == true;
        Children.Add(new WrapPanel { ItemSpacing = 8, LineSpacing = 6, Children = { new TextBlock { Text = Loc.Current["fonts.previewBack"], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, backdrop, new TextBlock { Text = Loc.Current["fonts.previewZoom"], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, zoom, grid } });
        var sampleHost = new ScrollViewer { Name = "GlyphSampleHost", Height = 100, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = _preview };
        Children.Add(sampleHost);
        Children.Add(DragHandle.For(sampleHost, "font.sample"));
        Children.Add(_previewNote);
        Children.Add(_sampleMissing);
        _sampleMissing.Text = MissingText(_sample.Text ?? "");
        ShowPreview();
    }

    public int Shown => _list.ItemCount;

    // A dragged, resized or nudged outline is the glyph's new place on the sheet: four numbers set as one undo step.
    private void SetRectangle((int X, int Y, int W, int H) r)
    {
        if (_workspace is null || _list.SelectedIndex is not (>= 0 and var row) || row >= _shown.Count) return;
        var path = $"Glyphs[{_shown[row]}].";
        var name = _font.Name.Content ?? "";
        static string Text(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            _workspace.Edit(new GroupCommand([new SetPropertyCommand("fonts", name, path + "SourceX", Text(r.X)), new SetPropertyCommand("fonts", name, path + "SourceY", Text(r.Y)),
                new SetPropertyCommand("fonts", name, path + "SourceWidth", Text(r.W)), new SetPropertyCommand("fonts", name, path + "SourceHeight", Text(r.H))]));
        }
        catch (InvalidOperationException e)
        {
            Notify?.Invoke(NotificationLevel.Error, e.Message);
            ShowOnSheet(); // the outline goes back to the stored rectangle
        }
    }

    private void ShowOnSheet()
    {
        var glyph = _list.SelectedIndex is >= 0 and var row && row < _shown.Count ? _font.Glyphs[_shown[row]] : null;
        Sheet?.Highlight(glyph is null ? null : (glyph.SourceX, glyph.SourceY, glyph.SourceWidth, glyph.SourceHeight));
    }

    /// <summary>Picks the listed glyph whose rectangle holds a pixel of the sheet (as a click on the sheet does); false when none does.</summary>
    public bool PickAt(int x, int y)
    {
        for (var row = 0; row < _shown.Count; row++)
        {
            var g = _font.Glyphs[_shown[row]];
            if (x < g.SourceX || y < g.SourceY || x >= g.SourceX + g.SourceWidth || y >= g.SourceY + g.SourceHeight) continue;
            _list.SelectedIndex = row;
            return true;
        }

        return false;
    }

    /// <summary>The character a literal, <c>U+0041</c>, <c>0x41</c> or a number stands for, or null.</summary>
    internal static int? ParseCharacter(string text) => CharacterInput.ParseCharacter(text);

    /// <summary>The characters of <c>A-Z</c>, <c>a..z</c> or <c>U+0020-U+007E</c> (at most 2048), or just the one character a single code stands for; null when the text is neither.</summary>
    public static IReadOnlyList<int>? ParseRange(string text) => CharacterInput.ParseRange(text);

    /// <summary>True when the system has a font family of this name.</summary>
    public static bool IsInstalled(string? family) => !string.IsNullOrWhiteSpace(family) && Avalonia.Media.FontManager.Current.SystemFonts.Any(f => string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));

    /// <summary>The warning for a font whose family is not installed (GameMaker draws it again from that family), or null when it is.</summary>
    public static string? Warning(GameMakerFont font) => IsInstalled(font.DisplayName?.Content) ? null : Loc.Current.Format("fonts.notInstalled", ("family", font.DisplayName?.Content ?? ""));

    /// <summary>A line with the family, size, style, anti-aliasing and SDF spread the font was made with.</summary>
    public static string Settings(GameMakerFont font) => Loc.Current.Format("fonts.settings", ("family", font.DisplayName?.Content ?? ""), ("size", font.EmSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
        ("style", (font.Bold ? "B" : "") + (font.Italic ? "I" : "")), ("aa", font.AntiAliasing), ("sdf", font.SDFSpread));

    /// <summary>The characters of a range button (see <see cref="CharacterInput.PresetNames"/>).</summary>
    public static IReadOnlyList<int> Preset(string name) => CharacterInput.Preset(name);

    /// <summary>The distinct characters of a sample text that the font has no glyph for, as a short line (empty when none).</summary>
    public string MissingText(string sample)
    {
        // Per code point against a set; a long sample is read only up to SampleCap characters so typing stays fast.
        var have = _font.Glyphs.Select(g => (int)g.Character).ToHashSet();
        var missing = (sample.Length > SampleCap ? sample[..(char.IsHighSurrogate(sample[SampleCap - 1]) ? SampleCap - 1 : SampleCap)] : sample).EnumerateRunes().Where(r => !System.Text.Rune.IsControl(r) && (r.Value > CharacterInput.FontMax || !have.Contains(r.Value))).Distinct().ToList();
        return missing.Count == 0 ? "" : Loc.Current.Format("fonts.sampleMissing", ("characters", string.Concat(missing)));
    }

    private const int SampleCap = 5000;

    private static readonly (string Name, string Key)[] PresetKeys =
    [
        ("Normal", "fonts.presetNormal"), ("Digits", "fonts.presetDigits"), ("Letters", "fonts.presetLetters"), ("Cyrillic", "fonts.presetCyrillic"), ("Greek", "fonts.presetGreek"),
        ("LatinExtended", "fonts.presetLatinExtended"), ("Hiragana", "fonts.presetHiragana"), ("Katakana", "fonts.presetKatakana"), ("Cjk", "fonts.presetCjk"), ("Hangul", "fonts.presetHangul"), ("Symbols", "fonts.presetSymbols"),
    ];

    public const int ConfirmAbove = 20;

    /// <summary>How many glyphs of the font a range such as <c>A-Z</c> would remove; 0 when the text is no range.</summary>
    public int MatchingGlyphs(string text)
    {
        if (ParseRange(text) is not { } codes) return 0;
        var have = _font.Glyphs.Select(g => (int)g.Character).ToHashSet();
        return codes.Count(have.Contains);
    }

    /// <summary>Removes every glyph of a range such as <c>A-Z</c> in one undo step; false when the text is no range or nothing matched. A range that is too long is refused with a message.</summary>
    public bool DeleteRange(string text)
    {
        if (_workspace is null) return false;
        if (CharacterInput.IsTooLong(text)) return TooLong();
        if (ParseRange(text) is not { } codes) return false;
        var font = _font.Name.Content ?? "";
        var at = new Dictionary<int, int>();
        for (var i = 0; i < _font.Glyphs.Count; i++) at.TryAdd(_font.Glyphs[i].Character, i); // the first glyph of a character, as FindIndex gave
        var removed = codes.Where(at.ContainsKey).Select(code => at[code]).OrderDescending()
            .Select(i => (EditCommand)new FontGlyphCommand(font, FontGlyphCommand.Remove, i)).ToList();
        if (removed.Count == 0) return false;
        _workspace.Edit(removed.Count == 1 ? removed[0] : new GroupCommand(removed));
        return true;
    }

    private bool TooLong()
    {
        Notify?.Invoke(NotificationLevel.Warning, CharacterHint.TooLong());
        return false;
    }

    private void Add()
    {
        var text = _newCharacter.Text ?? "";
        if (CharacterInput.IsTooLong(text)) TooLong();
        else if (ParseRange(text) is { } codes) _ = AddConfirmedAsync(codes);
    }

    /// <summary>Adds like <see cref="AddCodes"/>, after asking when the glyphs to add are more than <see cref="ConfirmAbove"/>.</summary>
    private async Task AddConfirmedAsync(IReadOnlyList<int> codes)
    {
        var count = NewCodes(codes).Count;
        if (count > ConfirmAbove && TopLevel.GetTopLevel(this) is Window owner
            && !await ProjectDialogs.AskConfirmAsync(owner, Loc.Current["fonts.glyphAdd"], Loc.Current.Format("fonts.glyphAddConfirm", ("count", count), ("font", _workspace?.Nicknames.Label("fonts", _font.Name.Content ?? "") ?? _font.Name.Content ?? "")))) return;
        AddCodes(codes);
    }

    // Sorted, distinct, and not in the font yet: one pass over the codes against a set of the font's characters.
    private List<int> NewCodes(IReadOnlyList<int> codes)
    {
        var have = _font.Glyphs.Select(g => (int)g.Character).ToHashSet();
        return [.. codes.Where(code => code <= CharacterInput.FontMax && !have.Contains(code)).Distinct().Order()];
    }

    /// <summary>Adds an empty glyph for each character the font lacks, as one undo step. A glyph holds only U+0000 to U+FFFF: higher characters are skipped and reported.</summary>
    public void AddCodes(IReadOnlyList<int> codes)
    {
        if (_workspace is null) return;
        var font = _font.Name.Content ?? "";
        if (codes.Any(c => c > CharacterInput.FontMax)) Notify?.Invoke(NotificationLevel.Warning, Loc.Current.Format("fonts.charSkipped", ("count", codes.Count(c => c > CharacterInput.FontMax))));
        var added = NewCodes(codes).Select(code => (EditCommand)new FontGlyphCommand(font, FontGlyphCommand.Add, -1, (ushort)code)).ToList();
        if (added.Count == 1) _workspace.Edit(added[0]);
        else if (added.Count > 1) _workspace.Edit(new GroupCommand(added));
    }

    /// <summary>Adds an empty glyph for every distinct character of a text (a pasted line, a translation file).</summary>
    public void AddFromText(string text) => AddCodes(CharacterInput.DistinctCharacters(text));

    // A toggle button in the row of the range buttons opens the paste box under it; the box stays in the tab so it can be typed into at once.
    private StackPanel FromTextPanel(out ToggleButton toggle)
    {
        var box = new TextBox { Name = "GlyphFromText", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 60, PlaceholderText = Loc.Current["fonts.fromTextHint"] };
        var go = new Button { Name = "GlyphFromTextAdd", Content = Loc.Current["fonts.glyphAdd"] };
        go.Click += async (_, _) => await AddConfirmedAsync(CharacterInput.DistinctCharacters(box.Text ?? ""));
        var panel = new StackPanel { Name = "GlyphFromTextPanel", Spacing = 6, IsVisible = false, Children = { box, go } };
        toggle = new ToggleButton { Name = "GlyphFromTextToggle", Content = Loc.Current["fonts.fromText"] };
        var shown = toggle;
        toggle.IsCheckedChanged += (_, _) => panel.IsVisible = shown.IsChecked == true;
        return panel;
    }

    private void ShowPreview()
    {
        _previewNote.Text = Sheet?.Picture is null ? Loc.Current["fonts.previewNoSheet"] : "";
        _previewNote.IsVisible = Sheet?.Picture is null;
        _preview.Refresh();
    }

    private void Reload()
    {
        var selected = SelectedCharacter();
        _rows.Clear();
        _rows.AddRange(_font.Glyphs.Select((g, i) => (g.Character, string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"U+{g.Character:X4}  {(char.IsControl((char)g.Character) ? ' ' : (char)g.Character)}   {g.SourceX},{g.SourceY}  {g.SourceWidth}x{g.SourceHeight}  {Loc.Current["fonts.glyphShift"]} {g.Shift}  {Loc.Current["fonts.glyphOffset"]} {g.Offset}"), i)));
        _count.Text = Loc.Current.Format("fonts.glyphCount", ("count", _rows.Count));
        _preview.Refresh();
        Refresh(selected);
    }

    // The list is rebuilt from the rows, which still describe what was shown until Reload replaces them.
    internal int? SelectedCharacter() => _list.SelectedIndex is >= 0 and var row && row < _shown.Count ? _rows.Where(r => r.Index == _shown[row]).Select(r => (int?)r.Character).FirstOrDefault() : null;

    private void Refresh() => Refresh(SelectedCharacter());

    private void Refresh(int? selected)
    {
        var text = (_filter.Text ?? "").Trim();
        var codes = text.Length == 0 ? null : CharacterInput.ParseRange(text, 0x10000)?.ToHashSet();
        var matching = _rows.Where(r => text.Length == 0 || codes?.Contains(r.Character) == true).ToList();
        _shown.Clear();
        _shown.AddRange(matching.Select(r => r.Index));
        _list.ItemsSource = matching.Select(r => r.Text).ToList();
        _list.SelectedIndex = selected is { } c ? matching.FindIndex(r => r.Character == c) : -1;
    }
}
