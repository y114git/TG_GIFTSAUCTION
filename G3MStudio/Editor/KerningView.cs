using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

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
        Avalonia.Controls.ToolTip.SetTip(_character, Loc.Current["fonts.charTip"]);
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
        Children.Add(CharacterHint.Follow(_character, false, "KerningCharacterHint"));
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
        if (FontGlyphsView.ParseCharacter(character) is not (>= 0 and <= CharacterInput.FontMax and var code) || !short.TryParse(shift, System.Globalization.CultureInfo.InvariantCulture, out var amount)) return false;
        _workspace.Edit(new FontKerningCommand(Font, _glyph, FontKerningCommand.Set, unchecked((short)code), amount));
        return true;
    }

    private void Set() => Set(_character.Text ?? "", _shift.Text ?? "");

    private void Reload() => _list.ItemsSource = Pairs.Select(k => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"U+{(ushort)k.Character:X4}  {(char.IsControl((char)k.Character) ? ' ' : (char)k.Character)}   shift {k.ShiftModifier}")).ToList();
}
