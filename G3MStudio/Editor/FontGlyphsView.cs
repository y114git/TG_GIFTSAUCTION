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
    private readonly ListBox _list = new() { Name = "FontGlyphs", MaxHeight = 150 };
    private readonly TextBlock _count = new() { Classes = { "muted" } };
    private readonly ContentControl _glyph = new() { Name = "GlyphProperties", MaxHeight = 260 };
    private readonly List<(ushort Character, string Text, int Index)> _rows = [];
    private readonly List<int> _shown = [];
    private readonly TextBlock _sampleDrawn = new TextBlock { Name = "GlyphSampleDrawn", TextWrapping = Avalonia.Media.TextWrapping.Wrap }.TextSize("FontSize.Display");
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
            _replace.IsEnabled = value is not null;
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
        }
    }

    public FontGlyphsView(GameMakerFont font, Workspace? workspace = null)
    {
        (_font, _workspace) = (font, workspace);
        Name = "FontGlyphsView";
        Spacing = 4;
        _filter.PlaceholderText = Loc.Current["fonts.glyphFilter"];
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Refresh();
        };
        Children.Add(new TextBlock { Name = "FontSettings", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Settings(font) });
        if (Warning(font) is { } warning) Children.Add(new TextBlock { Name = "FontMissing", Text = warning, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Children.Add(_count);
        Children.Add(_filter);
        Children.Add(_list);
        Children.Add(_glyph);
        if (workspace is not null)
        {
            // Picking a glyph opens a property list of its fields; Add makes an empty glyph for a character and Remove deletes the picked one (one undo step each).
            _list.SelectionChanged += (_, _) => ShowOnSheet();
            _list.SelectionChanged += (_, _) =>
                _glyph.Content = _list.SelectedIndex is >= 0 and var row && row < _shown.Count
                    ? new StackPanel { Spacing = 6, Children = { new KerningView(font, _shown[row], workspace), new PropertyGridTab(workspace, "fonts", font.Name.Content ?? "", null, $"Glyphs[{_shown[row]}]") } }
                    : null;
            _newCharacter.PlaceholderText = Loc.Current["fonts.glyphNew"];
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
                    && !await ProjectDialogs.AskConfirmAsync(owner, Loc.Current["fonts.deleteRange"], Loc.Current.Format("fonts.deleteRangeConfirm", ("count", MatchingGlyphs(text)), ("font", font.Name.Content ?? "")))) return;
                DeleteRange(text);
            };
            _replace.Content = Loc.Current["fonts.replace"];
            _replace.IsEnabled = ReplaceRequested is not null;
            Avalonia.Controls.ToolTip.SetTip(_replace, Loc.Current["fonts.replaceTip"]);
            _replace.Click += (_, _) => ReplaceRequested?.Invoke(font.Name.Content ?? "");
            var editGlyph = new Button { Name = "GlyphEditImage", Content = Loc.Current["fonts.editGlyphImage"], IsEnabled = false };
            editGlyph.Click += (_, _) =>
            {
                if (_list.SelectedIndex is >= 0 and var row && row < _shown.Count) EditGlyphRequested?.Invoke(ResourceTypes.KeyOf("fontGlyphs", $"{font.Name.Content}#{_shown[row]}"));
            };
            _list.SelectionChanged += (_, _) => editGlyph.IsEnabled = EditGlyphRequested is not null && _list.SelectedIndex is >= 0 and var picked && picked < _shown.Count;
            Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { _newCharacter, add, remove, editGlyph, deleteRange, _replace } });
            var presets = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
            foreach (var (name, key) in new[] { ("Normal", "fonts.presetNormal"), ("Digits", "fonts.presetDigits"), ("Letters", "fonts.presetLetters") })
            {
                var button = new Button { Name = "GlyphPreset" + name, Content = Loc.Current[key] };
                button.Click += (_, _) => AddCodes(Preset(name));
                presets.Children.Add(button);
            }

            Children.Add(presets);
            _sample.PlaceholderText = Loc.Current["fonts.sampleText"];
            _sampleDrawn.Text = Loc.Current["fonts.sampleDefault"];
            _sample.Text = _sampleDrawn.Text;
            if (IsInstalled(font.DisplayName?.Content)) _sampleDrawn.FontFamily = new Avalonia.Media.FontFamily(font.DisplayName?.Content ?? "");
            _sample.TextChanged += (_, _) =>
            {
                _sampleMissing.Text = MissingText(_sample.Text ?? "");
                _sampleDrawn.Text = _sample.Text;
            };
            Children.Add(_sampleDrawn);
            Children.Add(_sample);
            Children.Add(_sampleMissing);
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        }

        Reload();
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

    /// <summary>The character a code such as <c>U+0041</c>, a number or a single character stands for, or null.</summary>
    internal static int? ParseCharacter(string text)
    {
        text = text.Trim();
        if (text.StartsWith("U+", StringComparison.OrdinalIgnoreCase) && int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex)) return hex;
        if (text.Length == 1) return text[0];
        return int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>The characters of <c>A-Z</c> or <c>U+0020-U+007E</c> (at most 2048), or just the one character a single code stands for; null when the text is neither.</summary>
    public static IReadOnlyList<int>? ParseRange(string text)
    {
        var parts = text.Trim().Split('-', 2);
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && ParseCharacter(parts[0]) is { } from && ParseCharacter(parts[1]) is { } to)
            return from <= to && to <= ushort.MaxValue && to - from < 2048 && from >= 0 ? [.. Enumerable.Range(from, to - from + 1)] : null;
        return ParseCharacter(text) is { } one and >= 0 and <= ushort.MaxValue ? [one] : null;
    }

    /// <summary>True when the system has a font family of this name.</summary>
    public static bool IsInstalled(string? family) => !string.IsNullOrWhiteSpace(family) && Avalonia.Media.FontManager.Current.SystemFonts.Any(f => string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));

    /// <summary>The warning for a font whose family is not installed (GameMaker draws it again from that family), or null when it is.</summary>
    public static string? Warning(GameMakerFont font) => IsInstalled(font.DisplayName?.Content) ? null : Loc.Current.Format("fonts.notInstalled", ("family", font.DisplayName?.Content ?? ""));

    /// <summary>A line with the family, size, style, anti-aliasing and SDF spread the font was made with.</summary>
    public static string Settings(GameMakerFont font) => Loc.Current.Format("fonts.settings", ("family", font.DisplayName?.Content ?? ""), ("size", font.EmSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
        ("style", (font.Bold ? "B" : "") + (font.Italic ? "I" : "")), ("aa", font.AntiAliasing), ("sdf", font.SDFSpread));

    /// <summary>The characters of a range button: Normal is space to tilde, Digits 0-9, Letters A-Z and a-z.</summary>
    public static IReadOnlyList<int> Preset(string name) => name switch
    {
        "Digits" => [.. Enumerable.Range('0', 10)],
        "Letters" => [.. Enumerable.Range('A', 26), .. Enumerable.Range('a', 26)],
        _ => [.. Enumerable.Range(32, 95)],
    };

    /// <summary>The distinct characters of a sample text that the font has no glyph for, as a short line (empty when none).</summary>
    public string MissingText(string sample)
    {
        var missing = sample.Where(c => !char.IsControl(c)).Distinct().Where(c => _font.Glyphs.All(g => g.Character != c)).ToList();
        return missing.Count == 0 ? "" : Loc.Current.Format("fonts.sampleMissing", ("characters", string.Concat(missing)));
    }

    public const int ConfirmAbove = 20;

    /// <summary>How many glyphs of the font a range such as <c>A-Z</c> would remove; 0 when the text is no range.</summary>
    public int MatchingGlyphs(string text) => ParseRange(text) is { } codes ? codes.Count(code => _font.Glyphs.Any(g => g.Character == code)) : 0;

    /// <summary>Removes every glyph of a range such as <c>A-Z</c> in one undo step; false when the text is no range or nothing matched.</summary>
    public bool DeleteRange(string text)
    {
        if (_workspace is null || ParseRange(text) is not { } codes) return false;
        var font = _font.Name.Content ?? "";
        var removed = codes.Select(code => _font.Glyphs.FindIndex(g => g.Character == code)).Where(i => i >= 0).OrderDescending()
            .Select(i => (EditCommand)new FontGlyphCommand(font, FontGlyphCommand.Remove, i)).ToList();
        if (removed.Count == 0) return false;
        _workspace.Edit(removed.Count == 1 ? removed[0] : new GroupCommand(removed));
        return true;
    }

    private void Add()
    {
        if (ParseRange(_newCharacter.Text ?? "") is { } codes) AddCodes(codes);
    }

    /// <summary>Adds an empty glyph for each character the font lacks, as one undo step.</summary>
    public void AddCodes(IReadOnlyList<int> codes)
    {
        if (_workspace is null) return;
        var font = _font.Name.Content ?? "";
        var added = codes.Where(code => _font.Glyphs.All(g => g.Character != code)).Select(code => (EditCommand)new FontGlyphCommand(font, FontGlyphCommand.Add, -1, (ushort)code)).ToList();
        if (added.Count == 1) _workspace.Edit(added[0]);
        else if (added.Count > 1) _workspace.Edit(new GroupCommand(added));
    }

    private void Reload()
    {
        var selected = SelectedCharacter();
        _rows.Clear();
        _rows.AddRange(_font.Glyphs.Select((g, i) => (g.Character, string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"U+{g.Character:X4}  {(char.IsControl((char)g.Character) ? ' ' : (char)g.Character)}   {g.SourceX},{g.SourceY}  {g.SourceWidth}x{g.SourceHeight}  shift {g.Shift}  offset {g.Offset}"), i)));
        _count.Text = Loc.Current.Format("fonts.glyphCount", ("count", _rows.Count));
        Refresh(selected);
    }

    // The list is rebuilt from the rows, which still describe what was shown until Reload replaces them.
    internal int? SelectedCharacter() => _list.SelectedIndex is >= 0 and var row && row < _shown.Count ? _rows.Where(r => r.Index == _shown[row]).Select(r => (int?)r.Character).FirstOrDefault() : null;

    private void Refresh() => Refresh(SelectedCharacter());

    private void Refresh(int? selected)
    {
        var text = (_filter.Text ?? "").Trim();
        var code = ParseCharacter(text);
        var matching = _rows.Where(r => text.Length == 0 || r.Character == code).ToList();
        _shown.Clear();
        _shown.AddRange(matching.Select(r => r.Index));
        _list.ItemsSource = matching.Select(r => r.Text).ToList();
        _list.SelectedIndex = selected is { } c ? matching.FindIndex(r => r.Character == c) : -1;
    }
}

/// <summary>The kerning pairs of one glyph: each is the next character and the shift added between the two; Set adds a pair or changes its shift, Remove deletes the picked one (UTMT-FNT-04).</summary>
public sealed class KerningView : StackPanel
{
    private readonly GameMakerFont _font;
    private readonly int _glyph;
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "KerningPairs", MaxHeight = 120 };
    private readonly TextBox _character = new() { Name = "KerningCharacter", MinWidth = 90 };
    private readonly TextBox _shift = new() { Name = "KerningShift", MinWidth = 70 };

    public KerningView(GameMakerFont font, int glyph, Workspace workspace)
    {
        (_font, _glyph, _workspace) = (font, glyph, workspace);
        Name = "KerningView";
        Spacing = 4;
        _character.PlaceholderText = Loc.Current["fonts.kernCharacter"];
        _shift.PlaceholderText = Loc.Current["fonts.kernShift"];
        var set = new Button { Name = "KerningSet", Content = Loc.Current["fonts.kernSet"] };
        set.Click += (_, _) => Set();
        var remove = new Button { Name = "KerningRemove", Content = Loc.Current["timelines.remove"] };
        remove.Click += (_, _) =>
        {
            if (_list.SelectedIndex is >= 0 and var row && row < Pairs.Count) workspace.Edit(new FontKerningCommand(Font, glyph, FontKerningCommand.Remove, Pairs[row].Character));
        };
        Children.Add(new TextBlock { Text = Loc.Current["fonts.kerning"], Classes = { "muted" } });
        Children.Add(_list);
        Children.Add(new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { _character, _shift, set, remove } });
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Reload;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Reload;
        Reload();
    }

    private string Font => _font.Name.Content ?? "";

    private IReadOnlyList<GameMakerFont.Glyph.GlyphKerning> Pairs => _glyph < _font.Glyphs.Count ? [.. _font.Glyphs[_glyph].Kerning] : [];

    public int Count => Pairs.Count;

    /// <summary>The same edit as the Set button with the two boxes filled in; false when the character or the shift is not a number in range.</summary>
    public bool Set(string character, string shift)
    {
        if (FontGlyphsView.ParseCharacter(character) is not (>= short.MinValue and <= ushort.MaxValue and var code) || !short.TryParse(shift, System.Globalization.CultureInfo.InvariantCulture, out var amount)) return false;
        _workspace.Edit(new FontKerningCommand(Font, _glyph, FontKerningCommand.Set, unchecked((short)code), amount));
        return true;
    }

    private void Set() => Set(_character.Text ?? "", _shift.Text ?? "");

    private void Reload() => _list.ItemsSource = Pairs.Select(k => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"U+{(ushort)k.Character:X4}  {(char.IsControl((char)k.Character) ? ' ' : (char)k.Character)}   shift {k.ShiftModifier}")).ToList();
}
