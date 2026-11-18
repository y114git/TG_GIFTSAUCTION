using G3MStudio.Core.Workspace;

namespace G3MStudio;

/// <summary>The sound recorder: a new sound, or the audio of the sound selected.</summary>
public sealed partial class MainWindow
{
    /// <summary>Raised with each recorder dialog, so a test can drive it.</summary>
    public event Action<RecorderWindow>? RecorderOpened;

    public void RecordSound() => OpenRecorder(null);

    // Like the generator: the audio editor passes its sound and the dialog only replaces; from a menu it can also make a new sound.
    private void OpenRecorder(AudioEditorWindow? editor, (Workspace Workspace, string Name)? target = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        var (create, replace, replaceName) = SoundTargets(workspace, editor, target);
        var dialog = new RecorderWindow(create, replace, replaceName, () => Workspaces.Settings.Current.Audio, device => Workspaces.Settings.Update(x => x with { Audio = x.Audio with { CaptureDevice = device } }, notify: false), () => FileImport.NameFor(workspace.Data, "snd_recorded.wav")) { HasMemoryFor = Memory.Fits, OutOfMemory = () => NotifyOutOfMemory("memory.opRecord") };
        RecorderOpened?.Invoke(dialog);
        _ = ShowSoundDialogAsync(dialog, editor);
    }
}
