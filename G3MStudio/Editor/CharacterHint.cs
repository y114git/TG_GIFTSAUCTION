using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>The live line under a box that takes a character or a range: what the typed text resolves to (character, U+code, number), or why it does not.</summary>
public static class CharacterHint
{
    /// <summary>The line for a typed text; empty for empty text. A range gives its ends and its length.</summary>
    public static string Text(string input, bool range)
    {
        if (input.Trim().Length == 0) return "";
        var codes = range ? CharacterInput.ParseRange(input) : CharacterInput.ParseCharacter(input) is { } one ? [one] : null;
        if (range && CharacterInput.IsTooLong(input)) return TooLong();
        if (codes is not { Count: > 0 }) return Loc.Current["fonts.charInvalid"];
        var line = codes.Count == 1
            ? CharacterInput.Describe(codes[0])
            : Loc.Current.Format("fonts.charRange", ("from", CharacterInput.Describe(codes[0])), ("to", CharacterInput.Describe(codes[^1])), ("count", codes.Count));
        return codes.Any(c => c > CharacterInput.FontMax) ? line + "  " + Loc.Current["fonts.charTooHigh"] : line;
    }

    /// <summary>The refusal for a range above <see cref="CharacterInput.DefaultRangeLimit"/>.</summary>
    public static string TooLong() => Loc.Current.Format("fonts.charTooLong", ("count", CharacterInput.DefaultRangeLimit));

    /// <summary>A muted line that follows the box's text; add it under the box.</summary>
    public static TextBlock Follow(TextBox box, bool range, string name)
    {
        var line = new TextBlock { Name = name, Classes = { "muted" } };
        box.TextChanged += (_, _) => line.Text = Text(box.Text ?? "", range);
        return line;
    }
}
