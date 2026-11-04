using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Replacing and saving the audio of a sound.</summary>
public sealed partial class MainWindow
{
    public void ReplaceAudio() => _ = ReplaceAudioAsync();

    public void EditAudio()
    {
        if (FrontSound() is not var (workspace, sound)) return;
        var audio = workspace.Data.Sounds.FirstOrDefault(x => x.Name.Content == sound)?.AudioFile?.Data;
        if (audio is null or { Length: 0 } || SoundAudio.Format(audio) is not ("wav" or "ogg"))
        {
            Notify(NotificationLevel.Info, Loc.Current["audio.notWav"]);
            return;
        }

        AudioEditorWindow window;
        try
        {
            window = new AudioEditorWindow(Loc.Current.Format("audio.title", ("sound", sound)), audio, wav =>
            {
                workspace.Edit(new ReplaceAudioCommand(sound, wav));
                Notify(NotificationLevel.Info, Loc.Current.Format("sounds.replaced", ("sound", sound), ("kb", wav.Length / 1024)));
            });
        }
        catch (InvalidOperationException e)
        {
            Notify(NotificationLevel.Warning, e.Message);
            return;
        }

        AudioEditorOpened?.Invoke(window);
        _ = window.ShowDialog<bool?>(this);
    }

    /// <summary>Raised with each audio editor window, so a test can drive it.</summary>
    public event Action<AudioEditorWindow>? AudioEditorOpened;

    public void ExportAudio() => _ = ExportAudioAsync();

    public void ExportAllAudio() => _ = ExportAllAudioAsync();

    public async Task ExportAllAudioAsync()
    {
        if (Workspaces.Current is not { } workspace || await PickFolder(Loc.Current["transfer.pickExport"]) is not { } folder) return;
        await RunOperationAsync(workspace.Name, (progress, token) => AudioExport.ExportAllAsync(workspace, folder, progress, token), folder, export: true);
    }

    /// <summary>The sound of the Explorer selection or the tab in front, or null with a message.</summary>
    private (Workspace Workspace, string Name)? FrontSound()
    {
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (Workspaces.Current is { } workspace && key is not null && ResourceTypes.SplitKey(key) is var (type, name) && type == "sounds") return (workspace, name);
        Notify(NotificationLevel.Info, Loc.Current["sounds.noSound"]);
        return null;
    }

    public async Task ReplaceAudioAsync()
    {
        if (FrontSound() is not var (workspace, sound)) return;
        if (await PickPatchFile(Loc.Current["sounds.pickAudio"], false) is not { } path) return;
        await ReplaceAudioFromAsync(workspace, sound, path);
    }

    /// <summary>Puts a WAV, OGG or MP3 file into a sound; one undoable step. Used by Replace Audio and by a file dropped on a sound tab.</summary>
    private async Task ReplaceAudioFromAsync(Workspace workspace, string sound, string path)
    {
        try
        {
            var file = await File.ReadAllBytesAsync(path);
            // A WAV, OGG or MP3 file goes in as the kind the sound stores (WAV or OGG): another kind is decoded and encoded again.
            var stored = workspace.Data.Sounds.FirstOrDefault(s => s.Name.Content == sound)?.AudioFile?.Data is { } current && SoundAudio.Format(current) == "wav" ? "wav" : "ogg";
            var bytes = await Task.Run(() => AudioCodec.Convert(file, stored));
            workspace.Edit(new ReplaceAudioCommand(sound, bytes));
            Notify(NotificationLevel.Info, Loc.Current.Format("sounds.replaced", ("sound", sound), ("kb", bytes.Length / 1024)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    public async Task ExportAudioAsync()
    {
        if (FrontSound() is not var (workspace, sound)) return;
        var audio = workspace.Data.Sounds.FirstOrDefault(s => s.Name.Content == sound)?.AudioFile?.Data;
        if (audio is null or { Length: 0 })
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("sounds.outside", ("file", sound)));
            return;
        }

        if (await PickPatchFile(Loc.Current["sounds.pickAudioSave"], true, "." + SoundAudio.Format(audio)) is not { } path) return;
        try
        {
            await File.WriteAllBytesAsync(path, audio);
            Notify(NotificationLevel.Info, Loc.Current.Format("sounds.exported", ("sound", sound), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }
}
