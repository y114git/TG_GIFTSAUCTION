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
        ("code.duplicateLine", "Shift+Alt+Down"),
        ("code.moveLineUp", "Alt+Up"),
        ("code.moveLineDown", "Alt+Down"),
        ("code.toggleBookmark", "Ctrl+F2"),
        ("code.nextBookmark", "Alt+F2"),
        ("code.previousBookmark", "Ctrl+Shift+F2"),
        ("code.nextProblem", "F8"),
        ("code.previousProblem", "Shift+F8"),
        ("code.outline", "Ctrl+Shift+O"),
        ("code.toggleDiff", null),
    ];

    public const string CodeTab = "codeTab";
}
