namespace G3MStudio.Core.Commands;

/// <summary>Tooltip text with the shortcut of a command from the live keymap, so a rebinding shows at once.</summary>
public static class KeyTips
{
    /// <summary>"Title (Ctrl+S)", or just the title when the command has no shortcut or does not exist.</summary>
    public static string For(CommandRegistry registry, string id, string title) =>
        registry.Find(id)?.Shortcut is { Length: > 0 } shortcut ? $"{title} ({shortcut})" : title;
}
