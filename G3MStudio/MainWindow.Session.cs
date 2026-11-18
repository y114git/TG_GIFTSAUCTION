using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// Start behaviour and remembered data files. A start reopens the file the last run ended on (its tabs, layout, floating docks and window place come back
/// with it), shows Home, or asks, as Settings say. A file that is lost, unreadable or no longer the same is handled by <see cref="OpenRememberedAsync"/>.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the lost-file dialog. Tests set it.</summary>
    public Func<LostRequest, Task<LostChoice>>? LostFileAsker { get; set; }

    /// <summary>Replaces the question of the Ask start mode: true opens the last file. Tests set it.</summary>
    public Func<string, Task<bool>>? StartAsker { get; set; }

    /// <summary>The start of the program without a file or link on the command line: the Startup setting decides.</summary>
    public async Task StartSessionAsync()
    {
        var settings = Workspaces.Settings.Current;
        if (settings.LastSession is not { } last || settings.Startup == StartupMode.Home || Workspaces.Current is not null) return;
        if (settings.Startup == StartupMode.Ask)
        {
            var name = Path.GetFileName(last.Path);
            var open = StartAsker is { } test ? await test(name) : await ProjectDialogs.AskEitherAsync(this, Loc.Current["startup.askTitle"], Loc.Current.Format("startup.askBody", ("name", name)), "startup.open", "startup.home");
            if (!open) return;
        }

        await OpenRememberedAsync(last.Path, last.Size, last.Md5);
    }

    /// <summary>Opens a data file Studio remembers (the last session or a recent entry), checking it first. False when the user ended up at Home.</summary>
    public async Task<bool> OpenRememberedAsync(string path, long size, string? md5)
    {
        var settings = Workspaces.Settings;
        string detail = "";
        // A bounded loop: every round is a question the user answered, and Try again is the only answer that stays in it.
        for (var round = 0; round < 20; round++)
        {
            var now = settings.Current;
            LostReason reason;
            try
            {
                reason = await LostFile.CheckAsync(path, size, md5);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (reason == LostReason.None)
            {
                await OpenAsync(path);
                return Workspaces.Current is { } opened && PathRules.Same(opened.Path, path);
            }

            if (reason == LostReason.Unreadable) detail = await Task.Run(() => Unreadable(path));
            IReadOnlyList<Candidate> found = now.LostFileSearch && md5 is { Length: > 0 } hash ? await Task.Run(() => LostFile.FindAsync(path, size, hash, RecentFolders(path))) : [];
            if (reason == LostReason.Missing && now.LostFileAutoUse && LostFile.Unique(found) is { } sure)
            {
                Notify(NotificationLevel.Info, Loc.Current.Format("lostFile.found", ("name", Path.GetFileName(sure.Path)), ("path", sure.Path)));
                return await OpenReplacementAsync(path, sure.Path, true, size, md5);
            }

            // A changed file under a policy that decides needs no question: the file is there and the rule says what to do with it.
            if (reason == LostReason.Changed && LostFile.KeepLayout(now.HashMismatch) is { } keepIt && LostFile.Unique(found) is null) return await OpenReplacementAsync(path, path, keepIt, size, md5);
            var entries = reason == LostReason.Changed && now.HashMismatch != MismatchPolicy.Refuse ? [new Candidate(path, false), .. found] : found.Where(c => c.SameHash || now.HashMismatch != MismatchPolicy.Refuse).ToList();
            var request = new LostRequest(path, reason, detail, entries, now.HashMismatch == MismatchPolicy.Ask, now.HashMismatch != MismatchPolicy.FreshLayout, Workspaces.Recent.Find(path) is not null);
            var choice = LostFileAsker is { } test ? await test(request) : await new LostFileWindow(request).AskAsync(this);
            switch (choice.Kind)
            {
                case LostChoiceKind.Retry:
                    continue;
                case LostChoiceKind.RemoveRecent:
                    Workspaces.Recent.Remove(path);
                    return false;
                case LostChoiceKind.Open when choice.Path is { } picked:
                    if (await OpenReplacementAsync(path, picked, choice.KeepLayout, size, md5)) return true;
                    continue;
                case LostChoiceKind.Browse:
                    if (await PickPatchFile(Loc.Current["lostFile.pickTitle"], false) is not { } chosen) continue;
                    if (await OpenReplacementAsync(path, chosen, choice.KeepLayout, size, md5)) return true;
                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>A recent entry or Home card was clicked: a file that is there and unchanged opens at once, anything else goes through the lost-file steps.</summary>
    private Task<bool> OpenRecentAsync(string path)
    {
        var entry = Workspaces.Recent.Find(path);
        return OpenRememberedAsync(path, entry?.Size ?? 0, entry?.Md5);
    }

    private static string Unreadable(string path)
    {
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>The folders of the other recent files, where a moved game folder may have gone.</summary>
    private IEnumerable<string> RecentFolders(string lost) => Workspaces.Recent.Entries.Select(e => Path.GetDirectoryName(e.Path)).OfType<string>().Where(f => !string.Equals(f, Path.GetDirectoryName(lost), PathRules.Comparison));

    /// <summary>
    /// Opens <paramref name="picked"/> in place of the lost <paramref name="lost"/> (the same path when the file is there but changed). A file with the remembered
    /// content takes over the layout, sizes and folders of the lost one; any other follows <paramref name="keepLayout"/> or the setting, and Refuse stops here.
    /// </summary>
    private async Task<bool> OpenReplacementAsync(string lost, string picked, bool keepLayout, long size, string? md5)
    {
        var settings = Workspaces.Settings.Current;
        DataFileIdentity identity;
        try
        {
            identity = await DataFileIdentity.ComputeAsync(picked);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("app.openFailed", ("name", Path.GetFileName(picked)), ("reason", e.Message)));
            return false;
        }

        var same = md5 is not { Length: > 0 } || LostFile.Same(identity, size, md5);
        var keep = same || (LostFile.KeepLayout(settings.HashMismatch) ?? keepLayout);
        if (!same && settings.HashMismatch == MismatchPolicy.Refuse)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("lostFile.refused", ("name", Path.GetFileName(picked))));
            return false;
        }

        var samePlace = PathRules.Same(lost, identity.Path);
        // The unsaved-changes question comes first, also when the picked file is the open one (a changed file is read anew and the journal of the old content cannot
        // bring the edits back): answering No must leave the layout of the file as it is. The picked file keeps whatever it has of its own.
        if (Workspaces.Current is { IsDirty: true } && !await ConfirmCloseAsync()) return false;
        // The layout, sizes and run profile move before the open, which reads them; an open that fails puts them back.
        var restoreData = Workspaces.UserData.SnapshotFileData(identity.Path);
        var profiles = settings.FileRunProfiles;
        try
        {
            if (!samePlace && keep)
            {
                Workspaces.UserData.AdoptFileData(lost, picked);
                Workspaces.MoveRunProfile(lost, identity.Path);
            }
            else if (!keep)
            {
                Workspaces.UserData.ForgetLayout(picked);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            restoreData();
            Workspaces.Settings.Update(x => x with { FileRunProfiles = profiles });
            Notify(NotificationLevel.Warning, Loc.Current.Format("app.openFailed", ("name", Path.GetFileName(picked)), ("reason", e.Message)));
            return false;
        }

        var before = Workspaces.Current;
        await OpenAsync(picked, askAboutUnsaved: false);
        var opened = Workspaces.Current is { } now && !ReferenceEquals(now, before) && PathRules.Same(now.Path, identity.Path);
        if (!opened)
        {
            restoreData();
            Workspaces.Settings.Update(x => x with { FileRunProfiles = profiles });
        }
        else if (!samePlace && settings.LostFileForgetOld) Workspaces.Recent.Remove(lost);
        return opened;
    }
}
