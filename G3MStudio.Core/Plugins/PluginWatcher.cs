using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Plugins;

/// <summary>
/// Watches the plugins folder and reports the name of each plugin folder that changed, once the writes have been quiet for a moment
/// (an editor saves several files). The callback runs on a pool thread.
/// </summary>
public sealed class PluginWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _timer;
    private readonly Action<string> _changed;
    private readonly HashSet<string> _pending = [];
    private readonly Lock _gate = new();

    /// <summary>The watcher, or null when the system cannot give one (the inotify limit, a folder that cannot be made): plugins then run without hot reload.</summary>
    public static PluginWatcher? TryStart(string folder, Action<string> changed)
    {
        try
        {
            return new PluginWatcher(folder, changed);
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            FailureReport.Report(e, "plugin-watcher");
            return null;
        }
    }

    public PluginWatcher(string folder, Action<string> changed, TimeSpan? quiet = null)
    {
        _changed = changed;
        var wait = quiet ?? TimeSpan.FromMilliseconds(500);
        Directory.CreateDirectory(folder);
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        try
        {
            _watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size };
        }
        catch
        {
            _timer.Dispose();
            throw;
        }

        void Seen(string path)
        {
            var name = Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar, 2)[0];
            if (name is "" or "." or "..") return;
            lock (_gate)
            {
                _pending.Add(name);
                _timer.Change(wait, Timeout.InfiniteTimeSpan);
            }
        }

        _watcher.Changed += (_, e) => Seen(e.FullPath);
        _watcher.Created += (_, e) => Seen(e.FullPath);
        _watcher.Deleted += (_, e) => Seen(e.FullPath);
        _watcher.Renamed += (_, e) => Seen(e.FullPath);
        try
        {
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void Flush()
    {
        string[] names;
        lock (_gate)
        {
            names = [.. _pending];
            _pending.Clear();
        }

        foreach (var name in names) _changed(name);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _timer.Dispose();
    }
}
