namespace G3MStudio.Core.Commands;

public static partial class AppCommands
{
    /// <summary>Set while the image editor tab is in front; its commands run only then.</summary>
    public const string ImageTab = "imageTab";

    /// <summary>
    /// Every tool and action of the image editor with its default shortcut (GameMaker's keys where it has one). They run on the image tab in front and can be rebound
    /// in Settings like any command. The id after "img." is also what the editor itself dispatches on.
    /// </summary>
    /// <summary>The Image menu groups its flat commands into submenus: id to the group part of the language key menu.sub.img.&lt;group&gt;. Tools group by their own id (img.tool.*).</summary>
    private static readonly Dictionary<string, string> ImageGroups = BuildImageGroups();

    private static Dictionary<string, string> BuildImageGroups()
    {
        var groups = new Dictionary<string, string[]>
        {
            ["brush"] = ["brushBigger", "brushSmaller", "swapColours", "smooth"],
            ["view"] = ["zoomIn", "zoomOut", "zoomFit", "zoomActual", "grid", "symmetryH", "symmetryV", "tiles", "onion"],
            ["frames"] = ["previousFrame", "nextFrame", "duplicateFrame", "removeFrame", "play"],
            ["selection"] = ["selectAll", "selectNone", "invertSelection", "expandSelection", "copy", "cut", "paste", "clear", "fillSelection", "strokeSelection"],
            ["transform"] = ["flipH", "flipV", "rotateCw", "rotateCcw", "rotate", "scale", "skew"],
            ["adjust"] = ["adjust", "invertColours", "posterize", "greyscale", "dither", "outline", "blur"],
            ["canvas"] = ["resize", "crop", "trim"],
            ["layer"] = ["layerAdd", "layerDelete", "layerDuplicate", "layerUp", "layerDown", "layerMergeDown", "layerVisible", "layerLock"],
        };
        return groups.SelectMany(g => g.Value.Select(name => (Id: "img." + name, Group: "img." + g.Key))).ToDictionary(x => x.Id, x => x.Group);
    }


    /// <summary>Second gestures that GameMaker offers for the same tool (its hotkey files list Plus and Equals, Minus and the keypad minus), plus B and I for the pencil and the picker.</summary>
    public static IReadOnlyDictionary<string, string[]> ImageAliases { get; } = new Dictionary<string, string[]>
    {
        ["img.tool.pencil"] = ["B", "OemPeriod"],
        ["img.tool.picker"] = ["I"],
        ["img.brushBigger"] = ["Add"],
        ["img.brushSmaller"] = ["Subtract"],
    };

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
