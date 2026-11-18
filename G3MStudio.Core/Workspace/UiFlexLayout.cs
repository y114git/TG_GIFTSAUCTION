using G3MLib.DataFile.Models;
using Dir = G3MLib.DataFile.Models.IGameMakerFlexProperties.FlexDirectionKind;
using Align = G3MLib.DataFile.Models.IGameMakerFlexProperties.AlignmentKind;
using Unit = G3MLib.DataFile.Models.GameMakerFlexValue.UnitKind;

namespace G3MStudio.Core.Workspace;

/// <summary>A node drawn as a box. <paramref name="Path"/> is the node path of <see cref="UiLayerCommand"/>.</summary>
public readonly record struct UiBox(string Path, string Name, string Kind, double X, double Y, double Width, double Height, int Depth);

/// <summary>
/// A simple flex layout for the preview of a UI layer: direction, reverse, wrap, gap, padding, margin, sizes in points and percent, grow and shrink, justify,
/// item alignment, relative and absolute offsets. shortcut: an auto size is a fixed 48 points (no content measuring) and right-to-left is ignored; upgrade
/// by measuring content when a preview needs to match the game exactly.
/// </summary>
public static class UiFlexLayout
{
    public const double AutoSize = 48, InstanceSize = 32;

    public static IReadOnlyList<UiBox> Compute(GameMakerUIRootNode root, double width, double height)
    {
        var boxes = new List<UiBox>();
        var name = UiLayerCommand.NameOf(root.Node) ?? "";
        boxes.Add(new UiBox(name, name, "Layer", 0, 0, width, height, 0));
        if (root.Node is GameMakerUIContainer layer) Place(layer, name, new Rect(0, 0, width, height), 1, boxes);
        return boxes;
    }

    private static double Size(GameMakerFlexValue v, double basis, double fallback) => v.Unit switch
    {
        Unit.Point => v.Value,
        Unit.Percent => basis * v.Value / 100,
        _ => fallback,
    };

    private static bool Fixed(GameMakerFlexValue v) => v.Unit is Unit.Point or Unit.Percent;

    private sealed class Item
    {
        public required GameMakerUINode Node;
        public string Name = "";
        public double Main, Cross, MarginMainStart, MarginMainEnd, MarginCrossStart, MarginCrossEnd, Grow, Shrink, Offset, CrossStart;
        public bool StretchCross;
        public Align AlignSelf;
    }

    private readonly record struct Rect(double X, double Y, double W, double H);

    private static void Place(GameMakerUIContainer box, string path, Rect area, int depth, List<UiBox> boxes)
    {
        var (x, y, w, h) = area;
        var row = box.FlexDirection is Dir.Row or Dir.RowReverse;
        var reverse = box.FlexDirection is Dir.RowReverse or Dir.ColumnReverse;
        double pl = Size(box.PaddingLeft, w, 0), pr = Size(box.PaddingRight, w, 0), pt = Size(box.PaddingTop, w, 0), pb = Size(box.PaddingBottom, w, 0);
        double ix = x + pl, iy = y + pt, iw = Math.Max(0, w - pl - pr), ih = Math.Max(0, h - pt - pb);
        var mainSpace = row ? iw : ih;
        var crossSpace = row ? ih : iw;
        var gapMain = row ? box.GapColumn : box.GapRow;
        var gapCross = row ? box.GapRow : box.GapColumn;

        var flow = new List<Item>();
        foreach (var node in box.Children)
        {
            var childName = UiLayerCommand.NameOf(node.Node) ?? "?";
            var panel = node.Node as GameMakerUIFlexPanel;
            if (panel is { PositionType: GameMakerUIFlexPanel.PositionTypeKind.Absolute })
            {
                var aw = Size(panel.Width, iw, AutoSize);
                var ah = Size(panel.Height, ih, AutoSize);
                var ax = Fixed(panel.OffsetLeft) ? Size(panel.OffsetLeft, iw, 0) : Fixed(panel.OffsetRight) ? iw - aw - Size(panel.OffsetRight, iw, 0) : 0;
                var ay = Fixed(panel.OffsetTop) ? Size(panel.OffsetTop, ih, 0) : Fixed(panel.OffsetBottom) ? ih - ah - Size(panel.OffsetBottom, ih, 0) : 0;
                Emit(node, path, childName, new Rect(ix + ax, iy + ay, aw, ah), depth, boxes);
                continue;
            }

            flow.Add(MakeItem(node, childName, panel, row, new Rect(0, 0, iw, ih)));
        }

        // Split into lines: one line unless wrapping.
        var lines = new List<List<Item>>();
        var wrap = box.FlexWrap != IGameMakerFlexProperties.WrapKind.NoWrap;
        double used = 0;
        foreach (var item in flow)
        {
            var need = item.Main + item.MarginMainStart + item.MarginMainEnd;
            if (lines.Count == 0 || wrap && lines[^1].Count > 0 && used + gapMain + need > mainSpace)
            {
                lines.Add([]);
                used = 0;
            }

            used += (lines[^1].Count > 0 ? gapMain : 0) + need;
            lines[^1].Add(item);
        }

        if (box.FlexWrap == IGameMakerFlexProperties.WrapKind.WrapReverse) lines.Reverse();
        var lineCross = wrap ? 0 : crossSpace;
        double crossAt = 0;
        foreach (var line in lines)
        {
            if (wrap) lineCross = line.Max(i => i.Cross + i.MarginCrossStart + i.MarginCrossEnd);
            LayLine(box, line, reverse, mainSpace, gapMain, lineCross);
            foreach (var item in line)
            {
                var cross = crossAt + item.CrossStart;
                var (cx, cy, cw, ch) = row ? (ix + item.Offset, iy + cross, item.Main, item.Cross) : (ix + cross, iy + item.Offset, item.Cross, item.Main);
                Emit(item.Node, path, item.Name, new Rect(cx, cy, cw, ch), depth, boxes);
            }

            crossAt += lineCross + gapCross;
        }
    }

    private static Item MakeItem(GameMakerUINode node, string childName, GameMakerUIFlexPanel? panel, bool row, Rect inner)
    {
        var item = new Item { Node = node, Name = childName, AlignSelf = panel?.AlignSelf ?? Align.Auto };
        var (mainSpace, crossSpace) = row ? (inner.W, inner.H) : (inner.H, inner.W);
        if (panel is null)
        {
            item.Main = item.Cross = InstanceSize;
        }
        else
        {
            var mainValue = row ? panel.Width : panel.Height;
            var crossValue = row ? panel.Height : panel.Width;
            item.Main = Size(mainValue, mainSpace, 0);
            item.Cross = Size(crossValue, crossSpace, 0);
            if (!Fixed(mainValue) && panel.FlexGrow <= 0) item.Main = AutoSize;
            item.StretchCross = !Fixed(crossValue);
            if (!Fixed(crossValue)) item.Cross = AutoSize;
            item.Grow = Math.Max(0, panel.FlexGrow);
            item.Shrink = Math.Max(0, panel.FlexShrink);
            var (ms, me, cs, ce) = row
                ? (panel.MarginLeft, panel.MarginRight, panel.MarginTop, panel.MarginBottom)
                : (panel.MarginTop, panel.MarginBottom, panel.MarginLeft, panel.MarginRight);
            item.MarginMainStart = Size(ms, inner.W, 0);
            item.MarginMainEnd = Size(me, inner.W, 0);
            item.MarginCrossStart = Size(cs, inner.W, 0);
            item.MarginCrossEnd = Size(ce, inner.W, 0);
        }

        return item;
    }

    private static void LayLine(GameMakerUIContainer box, List<Item> line, bool reverse, double mainSpace, double gap, double lineCross)
    {
        double total = line.Sum(i => i.Main + i.MarginMainStart + i.MarginMainEnd) + gap * Math.Max(0, line.Count - 1);
        var free = mainSpace - total;
        if (free > 0 && line.Sum(i => i.Grow) is > 0 and var grow)
        {
            foreach (var item in line) item.Main += free * item.Grow / grow;
            free = 0;
        }
        else if (free < 0 && line.Sum(i => i.Shrink) is > 0 and var shrink)
        {
            foreach (var item in line) item.Main = Math.Max(0, item.Main + free * item.Shrink / shrink);
            free = 0;
        }

        free = Math.Max(0, free);
        double start = 0, between = gap;
        switch (box.JustifyContent)
        {
            case IGameMakerFlexProperties.JustifyKind.Center: start = free / 2; break;
            case IGameMakerFlexProperties.JustifyKind.FlexEnd: start = free; break;
            case IGameMakerFlexProperties.JustifyKind.SpaceBetween when line.Count > 1: between += free / (line.Count - 1); break;
            case IGameMakerFlexProperties.JustifyKind.SpaceAround: start = free / line.Count / 2; between += free / line.Count; break;
            case IGameMakerFlexProperties.JustifyKind.SpaceEvenly: start = free / (line.Count + 1); between += free / (line.Count + 1); break;
        }

        var at = start;
        foreach (var item in reverse ? [.. Enumerable.Reverse(line)] : line)
        {
            item.Offset = at + item.MarginMainStart;
            at += item.MarginMainStart + item.Main + item.MarginMainEnd + between;
        }

        if (reverse) foreach (var item in line) item.Offset = mainSpace - item.Offset - item.Main;

        foreach (var item in line)
        {
            var align = item.AlignSelf == Align.Auto ? box.AlignItems : item.AlignSelf;
            var room = lineCross - item.MarginCrossStart - item.MarginCrossEnd;
            if (align is Align.Auto or Align.Stretch && item.StretchCross) item.Cross = Math.Max(0, room);
            item.CrossStart = item.MarginCrossStart + align switch
            {
                Align.Center => Math.Max(0, room - item.Cross) / 2,
                Align.FlexEnd => Math.Max(0, room - item.Cross),
                _ => 0,
            };
        }
    }

    private static void Emit(GameMakerUINode node, string path, string name, Rect area, int depth, List<UiBox> boxes)
    {
        var (x, y, w, h) = area;
        var own = path + "/" + name;
        boxes.Add(new UiBox(own, name, node.Node.GetType().Name.Replace("GameMakerUI", "", StringComparison.Ordinal), x, y, w, h, depth));
        if (node.Node is GameMakerUIFlexPanel panel) Place(panel, own, area, depth + 1, boxes);
    }
}
