using Avalonia.Controls;

namespace G3MStudio.Theme;

/// <summary>
/// Small vector marks for icon-only buttons. A text glyph (a chevron, a cross) sits low or high in its line box and never looks centred; a filled shape
/// is centred by the button, and it takes the colour the button gives its content, so it changes on hover and when disabled.
/// </summary>
public static class GlyphIcons
{
    private const string Down = "M1.5,3.5 L6,8 L10.5,3.5 L9.4,2.4 L6,5.8 L2.6,2.4 Z";
    private const string Up = "M1.5,8.5 L6,4 L10.5,8.5 L9.4,9.6 L6,6.2 L2.6,9.6 Z";
    private const string Cross = "M2,3.4 L3.4,2 L6,4.6 L8.6,2 L10,3.4 L7.4,6 L10,8.6 L8.6,10 L6,7.4 L3.4,10 L2,8.6 L4.6,6 Z";
    private const string Plus = "M5.2,1.5 L6.8,1.5 L6.8,5.2 L10.5,5.2 L10.5,6.8 L6.8,6.8 L6.8,10.5 L5.2,10.5 L5.2,6.8 L1.5,6.8 L1.5,5.2 L5.2,5.2 Z";
    private const string Minus = "M1.5,5.2 L10.5,5.2 L10.5,6.8 L1.5,6.8 Z";

    private const string Left = "M8.5,1.5 L4,6 L8.5,10.5 L9.6,9.4 L6.2,6 L9.6,2.6 Z";
    private const string Right = "M3.5,1.5 L8,6 L3.5,10.5 L2.4,9.4 L5.8,6 L2.4,2.6 Z";

    public static Control Chevron(Avalonia.Layout.Orientation axis, bool towardsEnd) => Make(axis == Avalonia.Layout.Orientation.Vertical ? (towardsEnd ? Down : Up) : (towardsEnd ? Right : Left));

    public static Control Close() => Make(Cross);

    public static Control Add() => Make(Plus);

    public static Control Remove() => Make(Minus);

    private static PathIcon Make(string data) => new()
    {
        Data = Avalonia.Media.Geometry.Parse(data),
        Width = 12,
        Height = 12,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
}
