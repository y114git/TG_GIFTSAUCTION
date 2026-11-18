using Avalonia.Controls;
using Avalonia.Input;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class PropertyGridTab
{
    /// <summary>A glyph or kerning "Character" number gets a box beside it that shows the character and takes one typed character (or U+code) in its place.</summary>
    private Control CharacterRow(PropertyRow row, TextBox number)
    {
        if (row.Kind != PropertyKind.Whole || !row.Path.EndsWith(".Character", StringComparison.Ordinal) || !int.TryParse(row.Value, out var code) || code is < 0 or > CharacterInput.FontMax) return number;
        var shown = CharacterInput.Display(code);
        var box = new TextBox { Name = "Char_" + row.Path, Text = shown, IsReadOnly = row.ReadOnly, MinWidth = 60, MaxWidth = 90, Margin = new Avalonia.Thickness(8, 0, 0, 0) };
        ToolTip.SetTip(box, Loc.Current["fonts.charBoxTip"]);
        ToolTip.SetTip(number, Loc.Current["fonts.charNumberTip"]);
        void Done()
        {
            if (row.ReadOnly || box.Text == shown) return;
            // The typed text is turned into the number here, so a digit stays a digit instead of being read as a number.
            if (CharacterInput.ParseCharacter(box.Text ?? "") is { } typed && typed <= CharacterInput.FontMax) Commit(row.Path, typed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else
            {
                box.Text = shown;
                _message.Text = Loc.Current["fonts.charInvalid"];
            }
        }

        box.LostFocus += (_, _) => Done();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Done(); };
        return new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { number, box } };
    }
}
