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

public static class Shortcuts
{
    private static readonly Dictionary<string, KeyGesture> Cache = [];

    /// <summary>Registry shortcuts are written with Ctrl; macOS uses Cmd for the same keys.</summary>
    public static KeyGesture? Parse(string? shortcut)
    {
        if (shortcut is null) return null;
        if (Cache.TryGetValue(shortcut, out var cached)) return cached;
        return Cache[shortcut] = KeyGesture.Parse(OperatingSystem.IsMacOS() ? shortcut.Replace("Ctrl", "Cmd", StringComparison.Ordinal) : shortcut);
    }

    public static Command? Match(CommandRegistry registry, KeyEventArgs e) =>
        registry.All.FirstOrDefault(c => Parse(c.Shortcut) is { } gesture && gesture.Matches(e));
}
