using System.Globalization;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The low-memory system: the memory limit from Settings, the governor, what it releases, and the report when an operation still runs out of memory.</summary>
public sealed partial class MainWindow
{
    private int _appliedMemoryMb;
    private StringsWindow? _strings;

    public MemoryGovernor Memory { get; } = new(new GcMemorySource(), marshal: action => Dispatcher.UIThread.Post(action));

    private void InitMemory()
    {
        // Rebuildable caches go at High. Edits, the undo history and typed code are never touched, and neither are the views of tabs.
        Memory.OnPressure(MemoryLevel.High, () =>
        {
            if (Dispatcher.UIThread.CheckAccess()) ReleaseCaches();
            else Dispatcher.UIThread.Post(ReleaseCaches);
        });
        _ = new LowMemoryNotice(Memory, Notifications, () => Loc.Current["memory.low"]);
        _search.IsLowMemory = () => Memory.IsLow;
        Memory.LevelChanged += (_, _) => ShowMemory();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => ShowMemory();
        timer.Start();
        void libraryMissing(string file) => Notify(NotificationLevel.Warning, Loc.Current.Format("audio.noLibrary", ("file", file)));
        Audio.AudioDevices.LibraryMissing += libraryMissing;
        Closed += (_, _) =>
        {
            Audio.AudioDevices.LibraryMissing -= libraryMissing;
            timer.Stop();
            Memory.Dispose();
            _layerAutosave?.Dispose();
            Audio.AudioDevices.Shutdown();
        };
        Memory.Poll();
        Memory.Start();
    }

    private void ReleaseCaches()
    {
        _textures.Trim();
        _strings?.DropIndex();
        if (Workspaces.Current is not { } workspace) return;
        ReferenceCache.Of(workspace.Data).Invalidate();
        workspace.ReleaseDecompileContext();
    }

    private void ApplyMemoryLimit(int megabytes)
    {
        if (megabytes == _appliedMemoryMb) return;
        _appliedMemoryMb = megabytes;
        Memory.LimitBytes = megabytes * 1048576L;
        if (MemoryLimit.Apply(megabytes) is { } reason) Notify(NotificationLevel.Warning, Loc.Current.Format("memory.limitRefused", ("reason", reason)));
    }

    private void ShowMemory()
    {
        var bytes = Memory.Last.WorkingSetBytes;
        var used = bytes >= 1L << 30 ? (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : (bytes >> 20) + " MB";
        MemoryText.Text = Loc.Current.Format(Memory.IsLow ? "memory.statusLow" : "memory.status", ("used", used));
    }

    /// <summary>Tells the user an operation ran out of memory, after the caches were freed and the operation was tried once more (where it can be).</summary>
    private void NotifyOutOfMemory(string operationKey)
    {
        Memory.Relieve();
        Notify(NotificationLevel.Error, Loc.Current.Format("memory.failed", ("name", Loc.Current[operationKey])));
    }
}
