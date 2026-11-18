using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Replacing and saving the audio of a sound.</summary>
public sealed partial class MainWindow
{
    public void ReplaceAudio() => _ = ReplaceAudioAsync();

    public void EditAudio() => _ = EditAudioAsync();

    // The controls of the sound editor in front, from the Sound menu: the transport runs the same code as its buttons.
    private AudioTransport? FrontTransport()
    {
        if (Docking.ActiveTabId is { } id && _soundViews.TryGetValue(id, out var view) && view.Transport is { } transport) return transport;
        Notify(NotificationLevel.Info, Loc.Current["sounds.noSound"]);
        return null;
    }

    public void PlaySound() => _ = FrontTransport()?.PlayPauseAsync();

    public void StopSound() => FrontTransport()?.Stop();

    public void LoopSound() => FrontTransport()?.ToggleLoop();

    public void ReplaceFont()
    {
        var tabKey = Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null;
        var key = Explorer.SelectedKey is { } row && IsOfType(row, "fonts") ? row : tabKey;
        if (key is not null && ResourceTypes.SplitKey(key) is var (type, name) && type == "fonts") _ = ReplaceFontAsync(name);
        else Notify(NotificationLevel.Info, Loc.Current["fonts.noFont"]);
    }

    public void GenerateSound() => OpenSynth(null);

    // The audio editors being prepared (null while the sound decodes) or open, by sound.
    private readonly Dictionary<(Workspace, string), AudioEditorWindow?> _audioEditors = [];

    private async Task EditAudioAsync()
    {
        if (FrontSound() is not var (workspace, sound)) return;
        var audio = workspace.Data.Sounds.FirstOrDefault(x => x.Name.Content == sound)?.AudioFile?.Data;
        if (audio is null or { Length: 0 } || SoundAudio.Format(audio) is not ("wav" or "ogg" or "mp3"))
        {
            Notify(NotificationLevel.Info, Loc.Current["audio.notWav"]);
            return;
        }

        var key = (workspace, sound);
        if (_audioEditors.TryGetValue(key, out var open)) // one session per sound: a second request waits for the first, or brings its window forward
        {
            open?.Activate();
            return;
        }

        _audioEditors[key] = null;
        AudioEditorWindow? window = null;
        try
        {
            var decoded = await Task.Run(() => AudioCodec.Decode(audio)); // a long sound does not freeze the window while it is decoded
            // The project can be closed or the sound deleted meanwhile; the window would write into a workspace that is gone.
            if (Workspaces.Current == workspace && workspace.Data.Sounds.Any(x => x.Name.Content == sound))
                window = new AudioEditorWindow(Loc.Current.Format("audio.title", ("sound", Shown("sounds", sound))), audio, wav => PutAudioAsync(workspace, sound, wav), () => Workspaces.Settings.Current.Audio, decoded, editor => OpenSynth(editor, (workspace, sound))) { Record = editor => OpenRecorder(editor, (workspace, sound)) };
        }
        catch (OutOfMemoryException)
        {
            NotifyOutOfMemory("memory.opOpen");
        }
        catch (Exception e)
        {
            Notify(NotificationLevel.Warning, e.Message);
        }

        if (window is null)
        {
            _audioEditors.Remove(key);
            return;
        }

        _audioEditors[key] = window;
        window.Closed += (_, _) => _audioEditors.Remove(key);
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

    /// <summary>The sound of the Explorer selection, else of the tab in front (the same two places that turn the sound commands on), or null with a message.</summary>
    private (Workspace Workspace, string Name)? FrontSound(bool tell = true)
    {
        var tabKey = Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null;
        var key = Explorer.SelectedKey is { } row && IsSound(row) ? row : tabKey;
        if (Workspaces.Current is { } workspace && key is not null && ResourceTypes.SplitKey(key) is var (type, name) && type == "sounds") return (workspace, name);
        if (tell) Notify(NotificationLevel.Info, Loc.Current["sounds.noSound"]);
        return null;
    }

    public async Task ReplaceAudioAsync()
    {
        if (FrontSound() is not var (workspace, sound)) return;
        if (await PickPatchFile(Loc.Current["sounds.pickAudio"], false) is not { } path) return;
        await ReplaceAudioFromAsync(workspace, sound, path);
    }

    /// <summary>One undoable replace of the audio of a sound; a file over the size limit or of another kind goes in only after a warning.</summary>
    private async Task<bool> PutAudioAsync(Workspace workspace, string sound, byte[] bytes)
    {
        if (!await EditOrForceAsync(workspace, force => new ReplaceAudioCommand(sound, bytes, force: force), sound)) return false;
        Notify(NotificationLevel.Info, Loc.Current.Format("sounds.replaced", ("sound", Shown("sounds", sound)), ("kb", bytes.Length / 1024)));
        return true;
    }

    /// <summary>Puts a WAV, OGG or MP3 file into a sound; one undoable step. Used by Replace Audio and by a file dropped on a sound tab.</summary>
    private async Task ReplaceAudioFromAsync(Workspace workspace, string sound, string path)
    {
        try
        {
            await ReplaceAudioWithAsync(workspace, sound, await File.ReadAllBytesAsync(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    /// <summary>Puts WAV, OGG or MP3 bytes into a sound as the kind the sound stores (WAV or OGG): another kind is decoded and encoded again. One undoable step; false when nothing changed.</summary>
    internal async Task<bool> ReplaceAudioWithAsync(Workspace workspace, string sound, byte[] file)
    {
        var stored = workspace.Data.Sounds.FirstOrDefault(s => s.Name.Content == sound)?.AudioFile?.Data is { } current && SoundAudio.Format(current) == "wav" ? "wav" : "ogg";
        var bytes = await Task.Run(() => AudioCodec.Convert(file, stored));
        var done = await PutAudioAsync(workspace, sound, bytes);
        if (done && stored == "wav") TellDepthConverted(file, sound); // an OGG sound keeps its format, so no 16 bit is stored
        return done;
    }

    /// <summary>Says so when a WAV deeper than 16 bit was stored as 16 bit.</summary>
    private void TellDepthConverted(byte[] file, string name)
    {
        if (WavEdit.IsDeeperThan16(file)) Notify(NotificationLevel.Warning, Loc.Current.Format("sounds.convertedDepth", ("sound", Shown("sounds", name))));
    }

    public async Task ExportAudioAsync()
    {
        if (FrontSound() is not var (workspace, sound)) return;
        var audio = workspace.Data.Sounds.FirstOrDefault(s => s.Name.Content == sound)?.AudioFile?.Data;
        if (audio is null or { Length: 0 })
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("sounds.outside", ("file", Shown("sounds", sound))));
            return;
        }

        if (await PickPatchFile(Loc.Current["sounds.pickAudioSave"], true, "." + SoundAudio.Format(audio)) is not { } path) return;
        try
        {
            await File.WriteAllBytesAsync(path, audio);
            Notify(NotificationLevel.Info, Loc.Current.Format("sounds.exported", ("sound", Shown("sounds", sound)), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }
}
