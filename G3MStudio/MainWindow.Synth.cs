using Avalonia.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The sound generator: a new sound, or the audio of the sound selected.</summary>
public sealed partial class MainWindow
{
    /// <summary>Raised with each generator dialog, so a test can drive it.</summary>
    public event Action<SynthWindow>? SynthOpened;

    // The audio editor passes its sound and the dialog only replaces; from a menu it can also make a new sound.
    private void OpenSynth(AudioEditorWindow? editor, (Workspace Workspace, string Name)? target = null)
    {
        if (Workspaces.Current is not { } workspace) return;
        var (create, replace, replaceName) = SoundTargets(workspace, editor, target);
        var dialog = new SynthWindow(create, replace, replaceName, () => Workspaces.Settings.Current.Audio, () => FileImport.NameFor(workspace.Data, "snd_generated.wav"));
        SynthOpened?.Invoke(dialog);
        _ = ShowSoundDialogAsync(dialog, editor);
    }

    // What the generator and the recorder hand their result to: a new sound (not from an editor) and the audio of the sound selected or edited.
    private (Func<string, byte[], string?>? Create, Func<byte[], Task<bool>>? Replace, string? ReplaceName) SoundTargets(Workspace workspace, AudioEditorWindow? editor, (Workspace Workspace, string Name)? target)
    {
        var sound = target ?? FrontSound(tell: false);
        Func<string, byte[], string?>? create = editor is null ? (name, wav) => CreateResource(workspace, "sounds", name, wav) : null;
        Func<byte[], Task<bool>>? replace = sound is { } own ? wav => ReplaceGeneratedAsync(own.Workspace, own.Name, wav) : null;
        return (create, replace, sound is { } shown ? Shown("sounds", shown.Name) : null);
    }

    private async Task ShowSoundDialogAsync(Window dialog, AudioEditorWindow? editor)
    {
        try
        {
            var done = await (editor is null ? dialog.ShowDialog<bool?>(this) : dialog.ShowDialog<bool?>(editor));
            if (done == true) editor?.Close(false); // the editor still shows the audio that was replaced
        }
        finally
        {
            _unusualName = null; // the warning about an unusual name belongs to one dialog
        }
    }

    private async Task<bool> ReplaceGeneratedAsync(Workspace workspace, string sound, byte[] wav)
    {
        try
        {
            return await ReplaceAudioWithAsync(workspace, sound, wav);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sounds.badAudio", ("name", Shown("sounds", sound)), ("reason", e.Message)));
            return false;
        }
    }
}
