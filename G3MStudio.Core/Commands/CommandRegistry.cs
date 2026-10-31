namespace G3MStudio.Core.Commands;

/// <summary>One action. Its title is the localized key "cmd.&lt;Id&gt;" unless Title is set (plugin commands). When names a context key that must be set for the command to run.</summary>
public sealed record Command(string Id, string? Shortcut, Action Run, string? When = null, string? Title = null);

/// <summary>The single list that menus, the palette and shortcuts render from (design/shell.md section 3.1).</summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, Command> _commands = [];
    private readonly HashSet<string> _context = [];

    public event Action<string>? Executed;

    public event Action? ContextChanged;

    /// <summary>A command was added or removed after start-up (plugins).</summary>
    public event Action? CommandsChanged;

    public IEnumerable<Command> All => _commands.Values;

    public Command? Find(string id) => _commands.GetValueOrDefault(id);

    public Command Get(string id) => _commands[id];

    public void Add(Command command)
    {
        _commands.Add(command.Id, command);
        _defaults.Add(command.Id, command.Shortcut);
        CommandsChanged?.Invoke();
    }

    public void Remove(string id)
    {
        if (!_commands.Remove(id)) return;
        _defaults.Remove(id);
        CommandsChanged?.Invoke();
    }

    private readonly Dictionary<string, string?> _defaults = [];

    /// <summary>Raised with the command id when its shortcut changes.</summary>
    public event Action<string>? ShortcutChanged;

    public string? DefaultShortcut(string id) => _defaults.GetValueOrDefault(id);

    /// <summary>Gives a command another shortcut, or none with null. Unknown ids are ignored (a stored override of a removed command).</summary>
    public void SetShortcut(string id, string? shortcut)
    {
        if (Find(id) is not { } command || command.Shortcut == shortcut) return;
        _commands[id] = command with { Shortcut = shortcut };
        ShortcutChanged?.Invoke(id);
    }

    public bool IsEnabled(Command command) => command.When is null || _context.Contains(command.When);

    public void SetContext(string key, bool value)
    {
        if (value ? _context.Add(key) : _context.Remove(key)) ContextChanged?.Invoke();
    }

    public bool Execute(string id)
    {
        if (Find(id) is not { } command || !IsEnabled(command)) return false;
        command.Run();
        Executed?.Invoke(id);
        return true;
    }
}
