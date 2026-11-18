using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>An edit was refused because it changes a resource the owner locked in Studio.</summary>
public sealed class ResourceLockedException : InvalidOperationException
{
    public ResourceLockedException(string name) : base($"'{name}' is locked (read only in G3MStudio). {Unlock}") => ResourceName = name;

    private ResourceLockedException() : base($"This change cannot be limited to unlocked resources, and some resources are locked (read only in G3MStudio). {Unlock}") => ResourceName = "";

    private const string Unlock = "Uncheck \"Read only\" in the Inspector to unlock.";

    public string ResourceName { get; }

    /// <summary>A bulk change (import, patch, repack) reaches resources it does not name, so any lock refuses it.</summary>
    public static ResourceLockedException ForBulk() => new();
}

/// <summary>
/// The resources the owner marked read only, as Explorer keys. Studio only: the data file never carries it. The list is kept per data file in the user data
/// and written at once, so it survives a reopen.
/// </summary>
public sealed class ResourceLocks
{
    private readonly HashSet<string> _keys;
    private string? _file;

    public ResourceLocks(UserData? userData = null, string? dataFile = null)
    {
        _file = userData is null || dataFile is null ? null : userData.Combine("locks", UserData.KeyFor(dataFile) + ".json");
        _keys = Read(_file);
    }

    public event Action? Changed;

    /// <summary>The data file moved: the list is kept in the user data of the new path from now on.</summary>
    public void MoveTo(UserData userData, string dataFile)
    {
        _file = userData.Combine("locks", UserData.KeyFor(dataFile) + ".json");
        if (File.Exists(_file)) Write(); // the new path had a list of its own that the one in use replaces
    }

    public bool Any => _keys.Count > 0;

    /// <summary>Throws for a bulk change while any resource is locked.</summary>
    public void CheckBulk()
    {
        if (Any) throw ResourceLockedException.ForBulk();
    }

    public bool IsLocked(string key) => _keys.Contains(key);

    public void Set(string key, bool locked)
    {
        if (!(locked ? _keys.Add(key) : _keys.Remove(key))) return;
        Write();
        Changed?.Invoke();
    }

    private void Write()
    {
        if (_file is not null) UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(_keys.Order(StringComparer.Ordinal)));
    }

    /// <summary>True when the name is a locked resource, or a code entry that belongs to one (an object event, a script body, a room code).</summary>
    public bool Covers(string name) => _keys.Any(k => ResourceTypes.SplitKey(k) is var (_, locked) && Owns(name, locked));

    private static bool Owns(string name, string locked) =>
        name == locked || name == "gml_Script_" + locked || name.StartsWith("gml_Object_" + locked + "_", StringComparison.Ordinal)
        || name.StartsWith("gml_Room_" + locked + "_", StringComparison.Ordinal) || name.StartsWith("gml_RoomCC_" + locked + "_", StringComparison.Ordinal);

    /// <summary>Throws when the command changes a locked resource. A command names its resources in its arguments; a group is read item by item.</summary>
    public void Check(EditCommand command)
    {
        if (!Any) return;
        if (command is GroupCommand group)
        {
            foreach (var item in group.Items) Check(item);
            return;
        }

        foreach (var (argument, value) in command.Args)
        {
            if (argument is "text" or "type" or "kind") continue;
            foreach (var part in value.Split([',', '\n']))
            {
                if (Covers(part)) throw new ResourceLockedException(part);
            }
        }
    }

    private static HashSet<string> Read(string? file)
    {
        try
        {
            return file is not null && File.Exists(file) ? [.. JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file))?.OfType<string>() ?? []] : [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return []; // a damaged list is only a lost convenience
        }
    }
}
