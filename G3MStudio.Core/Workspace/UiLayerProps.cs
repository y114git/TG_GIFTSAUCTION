using System.Globalization;
using System.Reflection;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

public enum UiPropKind { Choice, Number, FlexValue, Toggle, Whole }

// Group: Which block of the editor shows it: layout, alignment, padding, size, position, margin or instance.
public sealed record UiProp(string Name, UiPropKind Kind, string Group, Type? Choices = null);

/// <summary>The typed properties of a UI node that the editor shows as fields, and the parse and format of their text.</summary>
public static class UiLayerProps
{
    private static UiProp Choice<T>(string name, string group) where T : struct, Enum => new(name, UiPropKind.Choice, group, typeof(T));

    private static readonly string[] SideNames = ["Left", "Right", "Top", "Bottom"];

    private static UiProp[] Sides(string stem, string group) => [.. SideNames.Select(s => new UiProp(stem + s, UiPropKind.FlexValue, group))];

    private static readonly UiProp[] Container =
    [
        Choice<IGameMakerFlexProperties.FlexDirectionKind>("FlexDirection", "layout"), Choice<IGameMakerFlexProperties.WrapKind>("FlexWrap", "layout"),
        new("GapRow", UiPropKind.Number, "layout"), new("GapColumn", UiPropKind.Number, "layout"),
        Choice<IGameMakerFlexProperties.JustifyKind>("JustifyContent", "alignment"), Choice<IGameMakerFlexProperties.AlignmentKind>("AlignItems", "alignment"),
        Choice<IGameMakerFlexProperties.AlignmentKind>("AlignContent", "alignment"), Choice<IGameMakerFlexProperties.LayoutDirectionKind>("LayoutDirection", "alignment"),
        .. Sides("Padding", "padding"),
    ];

    private static readonly UiProp[] Panel =
    [
        .. new[] { "Width", "Height", "MinimumWidth", "MinimumHeight", "MaximumWidth", "MaximumHeight" }.Select(n => new UiProp(n, UiPropKind.FlexValue, "size")),
        new("FlexGrow", UiPropKind.Number, "size"), new("FlexShrink", UiPropKind.Number, "size"),
        Choice<GameMakerUIFlexPanel.PositionTypeKind>("PositionType", "position"), Choice<IGameMakerFlexProperties.AlignmentKind>("AlignSelf", "position"),
        new("ClipsContents", UiPropKind.Toggle, "position"),
        .. Sides("Offset", "position"), .. Sides("Margin", "margin"),
    ];

    private static readonly UiProp[] Instance =
    [
        new("Visible", UiPropKind.Toggle, "instance"), new("Anchor", UiPropKind.Whole, "instance"),
        new("StretchWidth", UiPropKind.Toggle, "instance"), new("StretchHeight", UiPropKind.Toggle, "instance"),
        new("TileH", UiPropKind.Toggle, "instance"), new("TileV", UiPropKind.Toggle, "instance"), new("KeepAspect", UiPropKind.Toggle, "instance"),
    ];

    /// <summary>The fields to show for a node: the layer and panels hold flex properties, the other nodes the anchor and stretch flags.</summary>
    public static IReadOnlyList<UiProp> For(IGameMakerUINodeData node) => node switch
    {
        GameMakerUILayer => [new("Visible", UiPropKind.Toggle, "instance"), Choice<GameMakerUILayer.DrawSpaceKind>("DrawSpace", "instance"), .. Container],
        GameMakerUIFlexPanel => [.. Container, .. Panel],
        GameMakerUIGameObject or GameMakerUISpriteInstance or GameMakerUISequenceInstance or GameMakerUITextItemInstance => Instance,
        _ => [],
    };

    public static UiProp? Find(IGameMakerUINodeData node, string name) => For(node).FirstOrDefault(p => p.Name == name);

    private static PropertyInfo Info(IGameMakerUINodeData node, string name) =>
        Find(node, name) is not null && node.GetType().GetProperty(name) is { } info ? info : throw new InvalidOperationException($"A {node.GetType().Name} has no editable property {name}.");

    public static string Format(GameMakerFlexValue v) => v.Unit switch
    {
        GameMakerFlexValue.UnitKind.Point => v.Value.ToString("R", CultureInfo.InvariantCulture),
        GameMakerFlexValue.UnitKind.Percent => v.Value.ToString("R", CultureInfo.InvariantCulture) + "%",
        GameMakerFlexValue.UnitKind.Auto => "auto",
        _ => "",
    };

    /// <summary>"12" is points, "12%" a percent, "auto" is auto, and empty is undefined.</summary>
    public static GameMakerFlexValue ParseFlex(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return new GameMakerFlexValue(0, GameMakerFlexValue.UnitKind.Undefined);
        if (string.Equals(text, "auto", StringComparison.OrdinalIgnoreCase)) return new GameMakerFlexValue(0, GameMakerFlexValue.UnitKind.Auto);
        var percent = text.EndsWith('%');
        return new GameMakerFlexValue(Number(percent ? text[..^1] : text), percent ? GameMakerFlexValue.UnitKind.Percent : GameMakerFlexValue.UnitKind.Point);
    }

    private static float Number(string text) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && float.IsFinite(v) ? v : throw new InvalidOperationException($"{text} is not a number.");

    public static string Get(IGameMakerUINodeData node, string name) => Info(node, name).GetValue(node) switch
    {
        GameMakerFlexValue flex => Format(flex),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        { } other => Convert.ToString(other, CultureInfo.InvariantCulture) ?? "",
        _ => "",
    };

    /// <summary>Sets the property from text and returns the old value as stored, so a caller can put it back. Throws <see cref="InvalidOperationException"/> for a bad value.</summary>
    public static object? Set(IGameMakerUINodeData node, string name, string text)
    {
        var info = Info(node, name);
        var old = info.GetValue(node);
        var type = info.PropertyType;
        object value = type == typeof(GameMakerFlexValue) ? ParseFlex(text)
            : type == typeof(float) ? Number(text)
            : type == typeof(bool) ? bool.TryParse(text, out var b) ? b : throw new InvalidOperationException($"{text} is not true or false.")
            : type == typeof(int) ? int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : throw new InvalidOperationException($"{text} is not a whole number.")
            : Enum.TryParse(type, text, true, out var e) && Enum.IsDefined(type, e) ? e : throw new InvalidOperationException($"{text} is not a value of {name}.");
        info.SetValue(node, value);
        return old;
    }

    public static void Restore(IGameMakerUINodeData node, string name, object? old) => Info(node, name).SetValue(node, old);
}
