using System.Globalization;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
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
        timer.Tick += (_, _) =>
        {
            ShowMemory();
            if (++_pageTicks % 15 != 0) return;
            PagePixels.ForgetDecoded(); // every 30 seconds
            _ = EncodeIdlePagesAsync(PagePixels.IdleAfter, PagePixels.RawCap);
        };
        timer.Start();
        void libraryMissing(string file) => Notify(NotificationLevel.Warning, Loc.Current.Format("audio.noLibrary", ("file", file)));
        Audio.AudioDevices.LibraryMissing += libraryMissing;
        // With no tab left open nothing shows the decoded pages (about 100 MB on a big file): free them once the last tab has been closed for a while.
        var idle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        idle.Tick += (_, _) =>
        {
            idle.Stop();
            if (Docking.Tabs.Tabs.Count == 0) Memory.Relieve();
        };
        Docking.Changed += () =>
        {
            idle.Stop();
            if (Workspaces.Current is not null && Docking.Tabs.Tabs.Count == 0) idle.Start();
        };
        Closed += (_, _) => idle.Stop();
        Closed += (_, _) =>
        {
            Audio.AudioDevices.LibraryMissing -= libraryMissing;
            timer.Stop();
            Memory.Dispose();
            _layerAutosave?.Dispose();
            Audio.AudioDevices.Shutdown();
            Audio.AudioCaptureDevices.Shutdown();
        };
        Memory.Poll();
        Memory.Start();
    }

    private void ReleaseCaches()
    {
        _ = EncodeIdlePagesAsync(TimeSpan.Zero, 0);
        PagePixels.ForgetDecoded();
        _textures.Trim();
        _thumbs?.Clear();
        _strings?.DropIndex();
        if (Workspaces.Current is not { } workspace) return;
        ReferenceCache.Of(workspace.Data).Invalidate();
        workspace.ReleaseDecompileContext();
    }

    private int _pageTicks;
    private bool _encodingPages;

    /// <summary>
    /// Encodes the texture pages the image editor keeps as raw pixels back to their format when nobody wrote to them for <paramref name="idle"/>, or the oldest
    /// ones while all raw pages pass <paramref name="cap"/> bytes, so a long editing session does not keep growing. The encode runs off the UI thread; the swap
    /// skips a page an edit wrote in the meantime.
    /// </summary>
    private async Task EncodeIdlePagesAsync(TimeSpan idle, long cap)
    {
        if (_encodingPages || Workspaces.Current is not { } workspace) return;
        _encodingPages = true;
        try
        {
            await EncodeIdlePagesAsync(workspace, idle, cap);
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException or NotSupportedException)
        {
            if (e is not ObjectDisposedException) FailureReport.Report(e, "pages-encode");
            // The workspace closed or the format cannot be written here: the page stays raw, and the save encodes it as before.
        }
        finally
        {
            _encodingPages = false;
        }
    }

    internal static async Task EncodeIdlePagesAsync(Workspace workspace, TimeSpan idle, long cap)
    {
        IReadOnlyList<RawPage> pages;
        using (workspace.Lock.Read()) pages = PagePixels.Idle(workspace.Data, idle, cap);
        foreach (var page in pages)
        {
            var encoded = await Task.Run(() => PagePixels.Encode(page));
            // The write lock is awaited off the UI thread so a long background read cannot freeze the window; the swap keeps the same pixels in another format.
            await Task.Run(() =>
            {
                using (workspace.Lock.Write()) PagePixels.PutBack(page, encoded);
            });
        }
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
