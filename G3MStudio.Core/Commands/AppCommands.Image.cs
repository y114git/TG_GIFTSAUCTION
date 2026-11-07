namespace G3MStudio.Core.Commands;

public static partial class AppCommands
{
    /// <summary>Set while the image editor tab is in front; its commands run only then.</summary>
    public const string ImageTab = "imageTab";

    /// <summary>
    /// Every tool and action of the image editor with its default shortcut (GameMaker's keys where it has one). They run on the image tab in front and can be rebound
    /// in Settings like any command. The id after "img." is also what the editor itself dispatches on.
    /// </summary>
    private static HashSet<string> ImageMenuBreaks => ["img.brushBigger", "img.zoomIn", "img.previousFrame", "img.selectAll", "img.flipH", "img.adjust", "img.resize", "img.layerAdd"];

    public static IReadOnlyList<(string Id, string? Shortcut)> ImageCommands =>
    [
        ("img.tool.pencil", "D"), ("img.tool.eraser", "E"), ("img.tool.replace", "V"), ("img.tool.remove", "H"), ("img.tool.fill", "F"),
        ("img.tool.line", "L"), ("img.tool.rectangle", "R"), ("img.tool.rectangleFilled", "Shift+R"), ("img.tool.ellipse", "C"), ("img.tool.ellipseFilled", "Shift+C"),
        ("img.tool.polygon", "P"), ("img.tool.polygonFilled", "Shift+P"), ("img.tool.curve", "A"), ("img.tool.text", "T"), ("img.tool.picker", "O"),
        ("img.tool.select", "S"), ("img.tool.lasso", "Q"), ("img.tool.wand", "W"), ("img.tool.move", "M"),
        ("img.brushBigger", "OemPlus"), ("img.brushSmaller", "OemMinus"), ("img.swapColours", "X"), ("img.smooth", null),
        ("img.grid", "G"), ("img.symmetryH", null), ("img.symmetryV", null), ("img.tiles", null), ("img.onion", null),
        ("img.zoomIn", null), ("img.zoomOut", null), ("img.zoomFit", "Alt+F"), ("img.zoomActual", "Alt+C"),
        ("img.previousFrame", "D1"), ("img.nextFrame", "D2"), ("img.duplicateFrame", "Ctrl+D"), ("img.removeFrame", null), ("img.play", null),
        ("img.selectAll", "Ctrl+Shift+A"), ("img.selectNone", "Escape"), ("img.invertSelection", "Ctrl+I"), ("img.expandSelection", null), ("img.copy", "Ctrl+C"), ("img.cut", "Ctrl+X"),
        ("img.paste", "Ctrl+V"), ("img.clear", "Delete"), ("img.fillSelection", null), ("img.strokeSelection", null),
        ("img.flipH", null), ("img.flipV", null), ("img.rotateCw", null), ("img.rotateCcw", null), ("img.rotate", null), ("img.scale", null), ("img.skew", null),
        ("img.adjust", null), ("img.invertColours", null), ("img.posterize", null), ("img.greyscale", null), ("img.dither", null), ("img.outline", null), ("img.blur", null),
        ("img.resize", null), ("img.crop", null), ("img.trim", null),
        ("img.layerAdd", "Ctrl+Shift+N"), ("img.layerDelete", null), ("img.layerDuplicate", null), ("img.layerUp", null), ("img.layerDown", null),
        ("img.layerMergeDown", null), ("img.layerVisible", null), ("img.layerLock", null),
    ];
}
