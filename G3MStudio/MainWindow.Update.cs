using System.Reflection;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Updates;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Check for a newer release on GitHub, download it, and hand over to the update script (ADR-0005).</summary>
public sealed partial class MainWindow
{
    private const string Repository = "y114git/G3MStudio";

    /// <summary>Replaces the network check (tests set it). Gets the running version.</summary>
    public Func<SemVer, Task<UpdateOffer?>>? UpdateCheck { get; set; }

    /// <summary>Replaces the download and restart once the user agrees (tests set it).</summary>
    public Func<UpdateOffer, Task>? UpdateRunner { get; set; }

    public void CheckForUpdates() => _ = CheckForUpdatesAsync();

    public async Task CheckForUpdatesAsync()
    {
        var info = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        if (!SemVer.TryParse(info, out var current)) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var offer = await (UpdateCheck?.Invoke(current) ?? UpdateFeed.GitHub(Repository).CheckAsync(http, current, current.Pre.Length > 0, UpdateFeed.CurrentOs, UpdateFeed.CurrentArch));
            if (offer is null)
            {
                Notify(NotificationLevel.Info, Loc.Current.Format("update.none", ("version", current)));
                return;
            }

            if (!await Confirm(Loc.Current["update.title"], Loc.Current.Format("update.ask", ("version", offer.Version), ("current", current)))) return;
            await (UpdateRunner?.Invoke(offer) ?? InstallAsync(offer, http));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or UpdateException or IOException or System.Text.Json.JsonException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("update.failed", ("reason", e is UpdateException { Error: UpdateError.NotWritable } ? Loc.Current["update.notWritable"] : e.Message)));
        }
    }

    private async Task InstallAsync(UpdateOffer offer, HttpClient http)
    {
        var program = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        UpdateInstaller.EnsureWritable(program);
        var work = Path.Combine(program, UpdateInstaller.WorkFolder);
        Directory.CreateDirectory(work);
        Notify(NotificationLevel.Info, Loc.Current.Format("update.downloading", ("version", offer.Version)));
        var zip = await UpdateDownloader.DownloadAsync(http, offer, work);
        UpdateInstaller.Stage(zip, program);
        var self = Environment.ProcessPath ?? throw new InvalidOperationException("The running program has no path.");
        var script = UpdateInstaller.WriteScript(program, Path.GetRelativePath(program, self), Environment.ProcessId, Directory.CreateTempSubdirectory("g3mstudio-update-").FullName);
        if (!await ConfirmCloseAsync()) return;
        UpdateInstaller.Launch(script);
        Close();
    }
}
