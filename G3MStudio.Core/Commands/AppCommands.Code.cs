namespace G3MStudio.Core.Commands;

public static partial class AppCommands
{
    /// <summary>The commands of the code editor with their default shortcuts. They run on the code tab in front and can be rebound like any command.</summary>
    public static IReadOnlyList<(string Id, string? Shortcut)> CodeCommands =>
    [
        ("code.goToDefinition", "F12"),
        ("code.peekDefinition", "Alt+F12"),
        ("code.parameterHints", "Ctrl+Shift+Space"),
        ("code.quickFix", "Ctrl+OemPeriod"),
        ("code.insertSnippet", "Ctrl+J"),
        ("code.toggleComment", "Ctrl+OemQuestion"),
        ("code.blockComment", "Shift+Alt+A"),
        ("code.indent", null),
        ("code.outdent", null),
        ("code.duplicateLine", "Shift+Alt+Down"),
        ("code.moveLineUp", "Alt+Up"),
        ("code.moveLineDown", "Alt+Down"),
        ("code.toggleBookmark", "Ctrl+F2"),
        ("code.nextBookmark", "Alt+F2"),
        ("code.previousBookmark", "Alt+Shift+F2"),
        ("code.nextProblem", "F8"),
        ("code.previousProblem", "Shift+F8"),
        ("code.outline", "Ctrl+Shift+O"),
        ("code.toggleDiff", null),
        ("code.helpOnWord", "Ctrl+F1"),
        ("code.openInManual", null),
        ("code.goToLine", "Ctrl+G"),
        ("code.goToBracket", "Ctrl+Shift+OemPipe"),
        ("code.expandSelection", "Shift+Alt+Right"),
        ("code.shrinkSelection", "Shift+Alt+Left"),
        ("code.deleteLine", "Ctrl+Shift+K"),
        ("code.joinLines", "Ctrl+Alt+J"),
        ("code.sortLines", null),
        ("code.trimWhitespace", null),
        ("code.upperCase", null),
        ("code.lowerCase", null),
        ("code.titleCase", null),
    ];

    public const string CodeTab = "codeTab";

    /// <summary>Palette-only extra search text (language key) for commands that took over the job of removed duplicates: they ask where the result goes or where the script is.</summary>
    public static readonly IReadOnlyDictionary<string, string> PaletteAlt = new Dictionary<string, string>
    {
        ["mods.applyPatch"] = "palette.altApplyPatch",
        ["mods.applyMod"] = "palette.altApplyMod",
        ["tools.runScript"] = "palette.altRunScript",
    };
}
