using System.Globalization;
using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The optional display names of resources, as Explorer keys (<c>sprites:spr_hero</c>). Studio only: the data file and mod packs never carry them.
/// The real name stays the identity of the resource (code, saving, undo, references); a nickname only changes what the interface shows.
/// Kept per data file in the user data and written at once, so it survives a reopen.
/// </summary>
public sealed class Nicknames
{
    public const int MaxLength = 64;

    // The prefix of a code entry's name and the type of the resource that owns it.
    private static readonly (string Prefix, string Type)[] OwnerPrefixes = [("gml_Object_", "objects"), ("gml_Script_", "scripts"), ("gml_Room_", "rooms"), ("gml_RoomCC_", "rooms"), ("gml_GlobalScript_", "scripts")];

    private readonly Dictionary<string, string> _map;
    private string? _file;

    public Nicknames(UserData? userData = null, string? dataFile = null)
    {
        _file = userData is null || dataFile is null ? null : userData.Combine("nicknames", UserData.KeyFor(dataFile) + ".json");
        _map = Read(_file);
    }

    public event Action? Changed;

    /// <summary>The list could not be written (the reason); the nicknames stay in memory until the file closes.</summary>
    public event Action<string>? SaveFailed;

    /// <summary>The data file moved: the nicknames are kept in the user data of the new path from now on (the stores of the old path were copied by <see cref="UserData.AdoptFileData"/>).</summary>
    public void MoveTo(UserData userData, string dataFile)
    {
        _file = userData.Combine("nicknames", UserData.KeyFor(dataFile) + ".json");
        if (File.Exists(_file)) Write(); // the new path had a list of its own that the one in use replaces
    }

    public bool Any => _map.Count > 0;

    /// <summary>Says whether an Explorer key (<c>objects:obj_enemy</c>) is a resource of the open file, so a code entry is labelled by its real owner; null when the resources are not known.</summary>
    public Func<string, bool>? IsOwner { get; set; }

    /// <summary>Counts every change, so a cache built from the nicknames knows when it is out of date.</summary>
    public int Version { get; private set; }

    public IEnumerable<KeyValuePair<string, string>> All => _map;

    /// <summary>The resource a typed word names by its nickname (an exact match of a nickname that is one word), as (type, name); null when no nickname is the word.</summary>
    public (string Type, string Name)? Find(string word)
    {
        foreach (var (key, nick) in _map)
            if (nick == word && ResourceTypes.SplitKey(key) is { } split) return split;
        return null;
    }

    /// <summary>True when the text can stand in code as a name: letters, digits and underscores, not starting with a digit.</summary>
    public static bool IsWord(string text) => text.Length > 0 && !char.IsDigit(text[0]) && text.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>The nickname of a resource, or null when it has none.</summary>
    public string? Of(string key) => _map.GetValueOrDefault(key);

    public string? Of(string type, string name) => _map.Count == 0 ? null : _map.GetValueOrDefault(ResourceTypes.KeyOf(type, name));

    /// <summary>What the interface shows for a resource: its nickname, else its name.</summary>
    public string Label(string type, string name) => Of(type, name) ?? name;

    /// <summary>The label of an Explorer key; a key that is not a resource key comes back as it is.</summary>
    public string Label(string key) => Of(key) ?? (ResourceTypes.SplitKey(key) is var (_, name) ? name : key);

    /// <summary>The label of an entry of any type: code entries get the owner's nickname inside their name; no type leaves the name.</summary>
    public string OwnerLabel(string? type, string name) => type is null ? name : type == "code" ? CodeLabel(name) : Label(type, name);

    /// <summary>For questions that must name the exact resource (delete, duplicate): the nickname with the real name behind it, or the name alone.</summary>
    public string Both(string type, string name) => Of(type, name) is { } nick ? $"{nick} ({name})" : name;

    /// <summary>Like <see cref="Both"/> for a caller that knows the name but not its type (a message, a list of names); the first resource of that name wins.</summary>
    public string BothNamed(string name)
    {
        foreach (var (key, nick) in _map)
            if (ResourceTypes.SplitKey(key) is var (_, real) && real == name) return $"{nick} ({name})";
        return name;
    }

    /// <summary>
    /// Free text that holds a resource name (a title, a list of names, "name #3"): a name with a nickname becomes the nickname, a code entry gets its owner's nickname.
    /// For callers that know the name but not its type; the first resource of that name wins.
    /// </summary>
    public string Shown(string text)
    {
        if (_map.Count == 0 || text.Length == 0) return text;
        if (text.Contains(", ", StringComparison.Ordinal)) return string.Join(", ", text.Split(", ").Select(Shown));
        var code = CodeLabel(text);
        if (code != text) return code;
        var cut = text.LastIndexOf(" #", StringComparison.Ordinal);
        var (name, tail) = cut > 0 && int.TryParse(text[(cut + 2)..], out _) ? (text[..cut], text[cut..]) : (text, "");
        foreach (var (key, nick) in _map)
            if (ResourceTypes.SplitKey(key) is var (_, real) && real == name) return nick + tail;
        return text;
    }

    /// <summary>A message that quotes resource names ('obj_a'): each quoted name with a nickname reads 'nickname (obj_a)', so the message stays exact.</summary>
    public string Decorate(string message)
    {
        if (_map.Count == 0) return message;
        foreach (var (key, nick) in _map)
            if (ResourceTypes.SplitKey(key) is var (_, name) && message.Contains($"'{name}'", StringComparison.Ordinal)) message = message.Replace($"'{name}'", $"'{nick} ({name})'", StringComparison.Ordinal);
        return message;
    }

    /// <summary>
    /// The name of a code entry as shown: the part that names its owner (an object, script or room) is replaced by the owner's nickname.
    /// Entries of owners without a nickname, and every other text, come back unchanged.
    /// </summary>
    public string CodeLabel(string entry)
    {
        if (_map.Count == 0) return entry;
        if (_map.TryGetValue(ResourceTypes.KeyOf("code", entry), out var own)) return own; // a nickname given to the code entry itself (Rename on it) wins over its owner's
        foreach (var (prefix, type) in OwnerPrefixes)
        {
            if (!entry.StartsWith(prefix, StringComparison.Ordinal)) continue;
            // The owner is the longest name that ends the entry or ends before an underscore (obj_enemy_boss before obj_enemy). With the resources known, it is the
            // longest one that exists and it gets a label only when it has a nickname; an entry of no known resource falls back to the longest name with a nickname.
            var real = IsOwner is not null;
            for (var pass = real ? 0 : 1; pass < 2; pass++)
            {
                for (var end = entry.Length; end > prefix.Length; end = entry.LastIndexOf('_', end - 1))
                {
                    var key = string.Concat(type, ":", entry.AsSpan(prefix.Length, end - prefix.Length));
                    if (pass == 0 && !IsOwner!(key)) continue;
                    if (_map.TryGetValue(key, out var nick)) return prefix + nick + entry[end..];
                    if (pass == 0) return entry;
                }
            }
        }

        return entry;
    }

    /// <summary>True when the name or the nickname of the resource contains the text (ignoring case); an empty text matches everything.</summary>
    public bool Matches(string type, string name, string needle) =>
        needle.Length == 0 || name.Contains(needle, StringComparison.OrdinalIgnoreCase) || Of(type, name) is { } nick && nick.Contains(needle, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The key of another resource that already has this nickname or this name, so the nickname would be ambiguous in code and search; null when it is free.
    /// <paramref name="keyOfName"/> finds the resource by real name (null: only nicknames are compared).
    /// </summary>
    public string? Conflict(string key, string? nickname, Func<string, string?>? keyOfName = null)
    {
        var clean = Clean(nickname);
        if (clean.Length == 0 || ResourceTypes.SplitKey(key) is var (_, name) && clean == name) return null;
        foreach (var (other, nick) in _map)
            if (other != key && nick == clean) return other;
        return keyOfName?.Invoke(clean) is { } owner && owner != key ? owner : null;
    }

    /// <summary>
    /// Sets or clears (empty text) a nickname. A nickname equal to the name is the same as none; one that <see cref="Conflict"/> finds is refused.
    /// True when something changed.
    /// </summary>
    public bool Set(string key, string? nickname, Func<string, string?>? keyOfName = null)
    {
        var clean = Clean(nickname);
        if (ResourceTypes.SplitKey(key) is var (_, name) && clean == name) clean = "";
        if (clean.Length == 0)
        {
            if (!_map.Remove(key)) return false;
        }
        else
        {
            if (Conflict(key, clean, keyOfName) is not null) return false;
            if (_map.TryGetValue(key, out var was) && was == clean) return false;
            _map[key] = clean;
        }

        Save();
        return true;
    }

    /// <summary>
    /// The resource was renamed (<paramref name="to"/> is the new key) or deleted (null): the nickname follows or goes. Returns the nickname that moved.
    /// A name that already has a nickname of its own is not overwritten: both stay as they are and nothing moves (null).
    /// </summary>
    public string? Rekey(string from, string? to)
    {
        if (to is not null && from != to && _map.ContainsKey(to)) return null;
        if (!_map.Remove(from, out var nick)) return null;
        if (to is not null) _map[to] = nick;
        Save();
        return nick;
    }

    /// <summary>Puts a nickname back (undo of a delete) unless the resource got one meanwhile.</summary>
    public void Restore(string key, string? nickname)
    {
        if (string.IsNullOrEmpty(nickname) || _map.ContainsKey(key)) return;
        _map[key] = nickname;
        Save();
    }

    /// <summary>One line of at most <see cref="MaxLength"/> characters (whole emoji, never half of one): line breaks and control characters become spaces.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var line = string.Join(' ', new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var elements = StringInfo.ParseCombiningCharacters(line);
        return elements.Length <= MaxLength ? line : line[..elements[MaxLength]];
    }

    private void Save()
    {
        Version++;
        try
        {
            Write();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke(e.Message);
        }

        Changed?.Invoke();
    }

    private void Write()
    {
        if (_file is not null) UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(_map.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value)));
    }

    private static Dictionary<string, string> Read(string? file)
    {
        try
        {
            return file is not null && File.Exists(file)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))?.Select(p => (p.Key, Value: Clean(p.Value))).Where(p => p.Value.Length > 0).ToDictionary(p => p.Key, p => p.Value) ?? []
                : [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return []; // a damaged list is only a lost convenience
        }
    }
}
