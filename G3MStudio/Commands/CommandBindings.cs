using System.Windows.Input;
using Avalonia.Input;
using G3MStudio.Core.Commands;

namespace G3MStudio.Commands;

/// <summary>Registry command as an ICommand for menu items; enablement follows the registry context.</summary>
public sealed class RegistryCommand : ICommand
{
    private readonly CommandRegistry _registry;
    private readonly Command _command;

    public RegistryCommand(CommandRegistry registry, Command command)
    {
        _registry = registry;
        _command = command;
        registry.ContextChanged += () => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _registry.IsEnabled(_command);

    public void Execute(object? parameter) => _registry.Execute(_command.Id);
}

public static partial class Shortcuts
{
    [System.Text.RegularExpressions.GeneratedRegex(@"(?<![A-Za-z])D(\d)(?![A-Za-z0-9])")]
    private static partial System.Text.RegularExpressions.Regex DigitKey();

    private static readonly Dictionary<string, KeyGesture?> Cache = [];

    private static readonly (string Key, string Shown)[] KeyNames =
    [("OemComma", ","), ("OemPeriod", "."), ("OemPlus", "+"), ("OemMinus", "-"), ("OemQuestion", "/"), ("OemSemicolon", ";"), ("OemOpenBrackets", "["), ("OemCloseBrackets", "]"), ("OemPipe", "\\"), ("OemQuotes", "'"), ("OemTilde", "`")];

    /// <summary>The text a person reads for a shortcut: punctuation keys as the character, not the key enum name, and Cmd where macOS reads Ctrl (see <see cref="Parse"/>).</summary>
    public static string Display(string? shortcut) => Display(shortcut, OperatingSystem.IsMacOS());

    internal static string Display(string? shortcut, bool macOS)
    {
        var text = KeyNames.Aggregate(shortcut ?? "", (text, k) => text.Replace(k.Key, k.Shown, StringComparison.Ordinal));
        text = DigitKey().Replace(text, "$1"); // Avalonia names the top-row digits D0 to D9
        return macOS ? text.Replace("Ctrl", "Cmd", StringComparison.Ordinal) : text;
    }

    /// <summary>Registry shortcuts are written with Ctrl; macOS uses Cmd for the same keys.</summary>
    public static KeyGesture? Parse(string? shortcut)
    {
        if (shortcut is null) return null;
        if (Cache.TryGetValue(shortcut, out var cached)) return cached;
        // A hand-edited alias such as "Ctrl+Foo" is not a gesture: it parses to null once instead of throwing on every key press.
        try
        {
            return Cache[shortcut] = KeyGesture.Parse(OperatingSystem.IsMacOS() ? shortcut.Replace("Ctrl", "Cmd", StringComparison.Ordinal) : shortcut);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return Cache[shortcut] = null;
        }
    }

    /// <summary>When two commands hold the same key, a gesture the user chose wins (as the primary shortcut of its command, not as an alias), then a primary shortcut over an alias, then the command id: never the registry's insertion order.</summary>
    public static Command? Match(CommandRegistry registry, KeyEventArgs e) =>
        registry.All.Where(c => c.Gestures.Any(g => Parse(g) is { } gesture && gesture.Matches(e)))
            .OrderBy(c => registry.IsOverridden(c.Id) && PrimaryMatches(c, e) ? 0 : 1)
            .ThenBy(c => PrimaryMatches(c, e) ? 0 : 1)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    private static bool PrimaryMatches(Command command, KeyEventArgs e) => command.Shortcut is { } primary && Parse(primary) is { } gesture && gesture.Matches(e);
}
