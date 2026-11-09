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

    public PluginWatcher(string folder, Action<string> changed, TimeSpan? quiet = null)
    {
        _changed = changed;
        var wait = quiet ?? TimeSpan.FromMilliseconds(500);
        Directory.CreateDirectory(folder);
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size };
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
        _watcher.EnableRaisingEvents = true;
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
