using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.ModPack;
using G3MStudio.Localization;

namespace G3MStudio.Mods;

/// <summary>Small helpers of the mod windows: theme sizes and colours by token, so they follow the density and colour settings.</summary>
internal static class ModsUi
{
    /// <summary>A step of the spacing scale (1 to 4) as a number.</summary>
    public static double Space(int step) => Application.Current?.FindResource("Space." + step) is double value ? value : step * 4;

    public static Thickness Pad(int step) => new(Space(step));

    private static Color ColorOf(string token) => Application.Current?.FindResource(token) is ISolidColorBrush brush ? brush.Color : Colors.Gray;

    /// <summary>The theme colour of a kind of change, as a text brush.</summary>
    public static IBrush Ink(ChangeKind kind) => new SolidColorBrush(ColorOf(KindToken(kind)));

    /// <summary>The same colour, faint, as the background of a row.</summary>
    public static IBrush Tint(ChangeKind kind) => new SolidColorBrush(ColorOf(KindToken(kind)), 0.28);

    private static string KindToken(ChangeKind kind) => kind switch { ChangeKind.Added => "Diff.Added", ChangeKind.Removed => "Diff.Removed", _ => "Diff.Changed" };

    public static string KindName(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => Loc.Current["mods.diff.kind.added"],
        ChangeKind.Removed => Loc.Current["mods.diff.kind.removed"],
        _ => Loc.Current["mods.diff.kind.changed"],
    };

    public static StackPanel Buttons(params Control[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space(2), HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.AddRange(buttons);
        return panel;
    }
}
