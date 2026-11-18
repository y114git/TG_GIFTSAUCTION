using System.Text.Json;
using System.Text.Json.Serialization;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Settings;

/// <summary>Reads and writes <c>settings.json</c> in the user data folder; every change is saved at once and announced.</summary>
public sealed class SettingsStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    /// <summary>The settings file, for a command that shows it to the user.</summary>
    public string FilePath { get; }

    /// <summary>Studio's own data folder, for files kept per data file (run options, the mod base cache).</summary>
    public UserData UserData { get; }

    public SettingsStore(UserData userData)
    {
        UserData = userData;
        FilePath = userData.Combine("settings.json");
        if (!File.Exists(FilePath)) return;
        try
        {
            var text = ReadText();
            Current = Parse(text) ?? Current;
            _written = text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Held by a virus scan or another instance: the file is valid as far as we know, so it stays where it is and no change overwrites it until it can be read.
            _unread = true;
        }
        catch (JsonException)
        {
            // A file that does not parse is kept for the user and the defaults load (design/settings.md 3.1).
            KeepInvalid();
        }
    }

    private bool _unread;

    /// <summary>Reads the file, trying again briefly: a scanner or a second instance often holds it for a moment.</summary>
    private string ReadText()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllText(FilePath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 2)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    /// The settings in <paramref name="text"/>. A value this build does not know (a newer build wrote it) falls back to the default
    /// of that one setting; the rest of the file still loads. Text that is not JSON throws <see cref="JsonException"/>.
    /// </summary>
    private static AppSettings? Parse(string text)
    {
        var node = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(text, Json);
        if (node is null) return null;
        // A file saved before usage statistics existed belongs to someone who was never asked: that person is not opted in.
        if (!node.Any(p => p.Key.Equals(nameof(AppSettings.UsageStats), StringComparison.OrdinalIgnoreCase))
            && node.FirstOrDefault(p => p.Key.Equals(nameof(AppSettings.OnboardingDone), StringComparison.OrdinalIgnoreCase)).Value is { } done
            && done.GetValueKind() == System.Text.Json.JsonValueKind.True)
            node["usageStats"] = false; // the file is written in camel case
        // Each round drops the one setting the reader choked on; a value this build does not know is the usual cause.
        for (var round = 0; round < 16; round++)
        {
            try
            {
                return node.Deserialize<AppSettings>(Json)?.Clamped();
            }
            catch (JsonException e) when (TopLevelName(e.Path) is { } name && node.Remove(name))
            {
            }
        }

        throw new JsonException("Too many settings are not valid.");
    }

    private static string? TopLevelName(string? path)
    {
        if (path is not { Length: > 2 } || !path.StartsWith("$.", StringComparison.Ordinal)) return null;
        var rest = path.AsSpan(2);
        var end = rest.IndexOfAny('.', '[');
        return (end < 0 ? rest : rest[..end]).ToString();
    }

    /// <summary>False when the file could not be moved aside; the next change then overwrites it.</summary>
    private bool KeepInvalid()
    {
        try
        {
            File.Move(FilePath, FilePath + ".invalid", true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A hand edit that does not parse is moved aside before a change replaces the file, so the user's text is not lost.</summary>
    private void KeepInvalidFile()
    {
        try
        {
            if (File.Exists(FilePath) && File.ReadAllText(FilePath) is var text && text != _written) Parse(text);
        }
        catch (JsonException)
        {
            KeepInvalid();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // unreadable now: the write below goes through the atomic writer as before
        }
    }

    public AppSettings Current { get; private set; } = new();

    public event Action? Changed;

    private string _written = "";
    private FileSystemWatcher? _watcher;
    private SynchronizationContext? _context;

    /// <param name="change">Makes the new settings from the current ones.</param>
    /// <param name="notify">False saves a setting only the next user reads (a remembered toggle) without telling the listeners, which re-apply every setting.</param>
    public void Update(Func<AppSettings, AppSettings> change, bool notify = true)
    {
        if (_unread) TryReadAgain();
        else if (!Reload()) KeepInvalidFile(); // another instance may have written since: its change is the base of this one
        var next = change(Current).Clamped();
        if (next == Current) return;
        if (!_unread)
        {
            _written = JsonSerializer.Serialize(next, Json) + "\n";
            UserData.WriteAllTextAtomic(FilePath, _written); // a failed write leaves the old settings in place
        }

        Current = next;
        if (notify) Changed?.Invoke();
    }

    private void TryReadAgain()
    {
        try
        {
            if (Parse(ReadText()) is { } stored) Current = stored;
            _unread = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // still held: the change stays in memory
        }
        catch (JsonException)
        {
            _unread = false; // it does not parse after all: the change replaces it, as for a file that was invalid at start
            KeepInvalid();
        }
    }

    /// <summary>Starts hot reload: an edit of settings.json from outside (an editor, a copied theme) is read and applied at once, on the thread that called this. A file that does not parse is ignored until it does.</summary>
    public void Watch()
    {
        if (_watcher is not null || Path.GetDirectoryName(FilePath) is not { } folder || !Directory.Exists(folder)) return;
        _context = SynchronizationContext.Current;
        _written = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
        try
        {
            _watcher = new FileSystemWatcher(folder, Path.GetFileName(FilePath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName };
            _watcher.Changed += Reloaded;
            _watcher.Created += Reloaded;
            _watcher.Renamed += Reloaded;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Unwatch(); // the system refused another watcher (Linux limits them): settings still work, only the live reload is off
        }
    }

    private void Reloaded(object? sender, FileSystemEventArgs e)
    {
        if (_context is { } context) context.Post(_ => Reload(), null);
        else Reload();
    }

    /// <summary>Stops hot reload and gives the watcher back to the system.</summary>
    public void Unwatch()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose() => Unwatch();

    /// <summary>Reads settings.json again; applies it when it differs from what this program last wrote.</summary>
    public bool Reload()
    {
        try
        {
            var text = File.ReadAllText(FilePath);
            if (text == _written) return false;
            var next = Parse(text);
            if (next is null) return false;
            _unread = false;
            _written = text;
            Current = next;
            Changed?.Invoke();
            return true;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return false; // half written or not valid yet: the next write of the file tries again
        }
    }
}
