using Avalonia.Input;
using G3MStudio.Core.Commands;

namespace G3MStudio.Commands;

/// <summary>
/// Mouse buttons as shortcuts, written like the keyboard gestures so they share the registry, the settings and the shortcut editor: "Mouse1" (left), "Mouse2" (right),
/// "Mouse3" (middle), "Mouse4" (back) and "Mouse5" (forward), with Ctrl, Alt and Shift in front. A bare left or right click stays a click: those two count as a gesture
/// only with a modifier held.
/// </summary>
public static class MouseGestures
{
    private static readonly string[] Buttons = ["Mouse1", "Mouse2", "Mouse3", "Mouse4", "Mouse5"];

    /// <summary>True for a gesture text naming a mouse button, optionally behind Ctrl, Alt and Shift.</summary>
    public static bool Is(string? gesture)
    {
        if (gesture is null) return false;
        var parts = gesture.Split('+');
        // A bare left or right click is never produced by Describe, so such a text would be a binding that cannot fire.
        return Buttons.Contains(parts[^1], StringComparer.OrdinalIgnoreCase) && parts[..^1].All(m => m is "Ctrl" or "Alt" or "Shift")
            && (parts.Length > 1 || !parts[0].Equals(Buttons[0], StringComparison.OrdinalIgnoreCase) && !parts[0].Equals(Buttons[1], StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The gesture of a button press, or null for a bare left or right click.</summary>
    public static string? Describe(PointerPressedEventArgs e)
    {
        // A touch tap reads as a left press and a pen barrel as a right one: only a mouse makes a gesture.
        if (e.Pointer.Type != PointerType.Mouse) return null;
        var button = e.GetCurrentPoint(null).Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => Buttons[0],
            PointerUpdateKind.RightButtonPressed => Buttons[1],
            PointerUpdateKind.MiddleButtonPressed => Buttons[2],
            PointerUpdateKind.XButton1Pressed => Buttons[3],
            PointerUpdateKind.XButton2Pressed => Buttons[4],
            _ => null,
        };
        if (button is null) return null;
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (parts.Count == 0 && button is "Mouse1" or "Mouse2") return null;
        parts.Add(button);
        return string.Join('+', parts);
    }

    /// <summary>The command a button press runs. A gesture the user chose wins over a default, a primary one over an alias, then the command id (as for keys).</summary>
    public static Command? Match(CommandRegistry registry, PointerPressedEventArgs e) => Describe(e) is { } pressed ? Match(registry, pressed) : null;

    internal static Command? Match(CommandRegistry registry, string pressed)
    {
        bool Same(string gesture) => string.Equals(gesture, pressed, StringComparison.OrdinalIgnoreCase);
        bool Primary(Command c) => c.Shortcut is { } primary && Same(primary);
        var chosen = registry.All.Where(c => c.Gestures.Any(Same))
            .OrderBy(c => registry.IsOverridden(c.Id) && Primary(c) ? 0 : 1)
            .ThenBy(c => Primary(c) ? 0 : 1)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        // The back and forward buttons of the mouse always step through the resources, next to whatever keys the user gave those commands, unless a command claims the button.
        return chosen ?? (pressed.ToLowerInvariant() switch { "mouse4" => registry.Find("tabs.back"), "mouse5" => registry.Find("tabs.forward"), _ => null });
    }
}
