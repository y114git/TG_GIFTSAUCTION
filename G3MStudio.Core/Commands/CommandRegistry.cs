namespace G3MStudio.Core.Commands;

/// <summary>One action. Its title is the localized key "cmd.&lt;Id&gt;" unless Title is set (plugin commands). When names a context key that must be set for the command to run.</summary>
/// <remarks>Shortcut is the primary gesture (menus show it); Aliases are further gestures that run the same command.</remarks>
public sealed record Command(string Id, string? Shortcut, Action Run, string? When = null, string? Title = null, IReadOnlyList<string>? Aliases = null)
{
    /// <summary>The primary gesture first, then the aliases.</summary>
    public IEnumerable<string> Gestures => Shortcut is null ? Aliases ?? [] : (Aliases ?? []).Prepend(Shortcut);
}

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
        _defaultAliases.Add(command.Id, command.Aliases ?? []);
        CommandsChanged?.Invoke();
    }

    public void Remove(string id)
    {
        if (!_commands.Remove(id)) return;
        _defaults.Remove(id);
        _defaultAliases.Remove(id);
        CommandsChanged?.Invoke();
    }

    private readonly Dictionary<string, string?> _defaults = [];
    private readonly Dictionary<string, IReadOnlyList<string>> _defaultAliases = [];

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

    public IReadOnlyList<string> DefaultAliases(string id) => _defaultAliases.GetValueOrDefault(id) ?? [];

    /// <summary>Replaces the aliases of a command (null or empty for none). Unknown ids are ignored.</summary>
    public void SetAliases(string id, IReadOnlyList<string>? aliases)
    {
        if (Find(id) is not { } command) return;
        var next = aliases is null ? [] : aliases.Where(a => a.Length > 0).Distinct().ToList();
        if ((command.Aliases ?? []).SequenceEqual(next)) return;
        _commands[id] = command with { Aliases = next };
        ShortcutChanged?.Invoke(id);
    }

    /// <summary>The other commands that already use a gesture, as a primary or an alias.</summary>
    public IEnumerable<Command> UsersOf(string gesture, string exceptId) =>
        _commands.Values.Where(c => c.Id != exceptId && c.Gestures.Contains(gesture, StringComparer.OrdinalIgnoreCase));

    /// <summary>True when the primary gesture is one the user chose, not the default.</summary>
    public bool IsOverridden(string id) => Find(id) is { Shortcut: not null } command && !string.Equals(command.Shortcut, DefaultShortcut(id), StringComparison.OrdinalIgnoreCase);

    /// <summary>The gestures a user's own primary shortcut shares with another command (for example a new default that landed on an old saved key). The user's command wins when a key is pressed.</summary>
    public IEnumerable<(Command User, Command Other, string Gesture)> OverrideClashes() =>
        _commands.Values.OrderBy(c => c.Id, StringComparer.Ordinal)
            .SelectMany(c => c.Shortcut is { } gesture && IsOverridden(c.Id)
                ? UsersOf(gesture, c.Id).OrderBy(o => o.Id, StringComparer.Ordinal).Select(o => (c, o, gesture))
                : Enumerable.Empty<(Command User, Command Other, string Gesture)>());

    public bool HasContext(string key) => _context.Contains(key);

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
